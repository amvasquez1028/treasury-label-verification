param(
  [string]$BaseUrl = "http://localhost:8082",
  [switch]$FullProbe
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:DEMO_AGENT_PASSWORD)) {
  Write-Error "DEMO_AGENT_PASSWORD environment variable is not set. Export it before running smoke tests."
  exit 1
}

function Invoke-Curl {
  param(
    [string]$Uri,
    [string]$Method = "GET",
    [string]$Body = $null,
    [string]$ContentType = $null,
    [string]$CookieJar = $null,
    [int]$TimeoutSec = 30
  )

  $args = @(
    "-sS",
    "--max-time", "$TimeoutSec",
    "-X", $Method,
    "-w", "`n%{http_code}"
  )

  if ($CookieJar) {
    $args += @("-b", $CookieJar, "-c", $CookieJar)
  }

  if ($ContentType) {
    $args += @("-H", "Content-Type: $ContentType")
  }

  $bodyFile = $null
  if ($Body) {
    $bodyFile = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($bodyFile, $Body, [System.Text.UTF8Encoding]::new($false))
    $args += @("--data-binary", "@$bodyFile")
  }

  $args += $Uri

  try {
    $raw = & curl.exe @args 2>&1
  }
  finally {
    if ($bodyFile -and (Test-Path $bodyFile)) {
      Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
    }
  }

  if ($LASTEXITCODE -ne 0) {
    throw "curl failed ($Uri): $raw"
  }

  $lines = @($raw)
  if ($lines.Count -lt 2) {
    throw "Unexpected curl output for $Uri"
  }

  $statusCode = [int]$lines[-1]
  $content = ($lines[0..($lines.Count - 2)] -join "`n").Trim()

  return @{ StatusCode = $statusCode; Content = $content }
}

Write-Host "Smoke testing $BaseUrl"

$live = Invoke-Curl -Uri "$BaseUrl/health/live" -TimeoutSec 10
if ($live.StatusCode -ne 200) {
  throw "health/live returned $($live.StatusCode)"
}
Write-Host "Live: $($live.Content)"

$ready = Invoke-Curl -Uri "$BaseUrl/health/ready" -TimeoutSec 45
if ($ready.StatusCode -eq 200) {
  Write-Host "Ready: 200"
}
elseif ($ready.StatusCode -eq 503) {
  Write-Host "Ready: 503 (OCR still warming)"
}
else {
  throw "health/ready returned $($ready.StatusCode)"
}

$cookieJar = Join-Path $env:TEMP "label-verify-smoke-cookies.txt"
if (Test-Path $cookieJar) {
  Remove-Item $cookieJar -Force
}

$loginBody = (@{ email = "demo.agent@label-verify.demo"; password = $env:DEMO_AGENT_PASSWORD } | ConvertTo-Json -Compress)
$login = Invoke-Curl -Uri "$BaseUrl/api/v1/auth/login" -Method POST -Body $loginBody -ContentType "application/json" -CookieJar $cookieJar -TimeoutSec 30
if ($login.StatusCode -ne 200) {
  throw "login returned $($login.StatusCode): $($login.Content)"
}
Write-Host "Login: $($login.StatusCode)"

$me = Invoke-Curl -Uri "$BaseUrl/api/v1/auth/me" -CookieJar $cookieJar -TimeoutSec 15
if ($me.StatusCode -ne 200) {
  throw "auth/me returned $($me.StatusCode)"
}
Write-Host "Me: $($me.Content)"

if ($FullProbe) {
  Write-Host "Running full reviewer probe (strict)..."
  $repoRoot = Split-Path -Parent $PSScriptRoot
  python (Join-Path $repoRoot "scripts\_probe_production.py") --strict
  if ($LASTEXITCODE -ne 0) {
    throw "Strict probe failed (exit $LASTEXITCODE)"
  }
}
else {
  Write-Host "Quick smoke complete. For 5/5 walkthrough gate: python scripts/_probe_production.py --strict"
  Write-Host "Or: .\scripts\smoke-production.ps1 -BaseUrl $BaseUrl -FullProbe"
}

Write-Host "Smoke test complete."
