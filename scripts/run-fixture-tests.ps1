param(
  [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Generating fixtures..."
Push-Location $repoRoot
node scripts/generate-test-labels.mjs
Pop-Location

Write-Host "Running fixture tests..."
Push-Location (Join-Path $repoRoot "backend")
dotnet test LabelVerification.Tests/LabelVerification.Tests.csproj --configuration $Configuration --filter "FullyQualifiedName~VerificationFixtureTests"
Pop-Location
