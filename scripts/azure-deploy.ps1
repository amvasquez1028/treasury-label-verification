param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [string]$Location = "westus2",

    [string]$AppName = "label-verify-trackc",
    [string]$AcrName = "",
    [string]$PlanName = "",
    [string]$AppServiceSku = "P2v3",
    [string]$ImageTag = "latest",
    [switch]$WhatIf,
    [switch]$SkipProbeGate,
    [switch]$UseAcrBuild,
    [switch]$LocalBuildOnly,
    [switch]$ImportBaseImages
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

# Azure CLI on Windows can crash (UnicodeEncodeError) when streaming colored build logs.
$env:AZURE_CORE_NO_COLOR = "true"
$env:PYTHONIOENCODING = "utf-8"
$env:PYTHONUTF8 = "1"
if ($env:OS -match "Windows") {
    try { chcp 65001 | Out-Null } catch {}
    $OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
}

function Invoke-AzStep {
    param([string]$Label, [scriptblock]$Action)
    Write-Host "==> $Label"
    if ($WhatIf) { return }
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed (exit $LASTEXITCODE)"
    }
}

function Invoke-DockerBuildLocal {
    param([string]$Image)
    Invoke-ExternalStep "Docker build (local)" { docker build -t $Image $repoRoot }
}

function New-AcrBuildStagingContext {
    $staging = Join-Path $env:TEMP "label-verify-acr-$([Guid]::NewGuid().ToString('n'))"
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Write-Host "Staging ACR build context at $staging (excluding node_modules/.next/.git)..."
    & robocopy $repoRoot $staging /MIR /XD node_modules .next .git /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "Failed to stage ACR build context (robocopy exit $LASTEXITCODE)"
    }
    return $staging
}

function Test-AcrRepositoryTag {
    param(
        [string]$RegistryName,
        [string]$Repository,
        [string]$Tag
    )

    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $tags = & $az acr repository show-tags --name $RegistryName --repository $Repository -o tsv 2>$null
    $ok = ($LASTEXITCODE -eq 0) -and ($tags | Where-Object { $_ -eq $Tag })
    $ErrorActionPreference = $prev
    return $ok
}

function Import-AcrDotnetBaseImages {
    param([string]$RegistryName)

    $pairs = @(
        @{ Source = "mcr.microsoft.com/dotnet/sdk:8.0-jammy"; Repo = "base/dotnet-sdk"; Tag = "8.0-jammy" },
        @{ Source = "mcr.microsoft.com/dotnet/aspnet:8.0-jammy"; Repo = "base/dotnet-aspnet"; Tag = "8.0-jammy" }
    )

    $cached = $true
    foreach ($pair in $pairs) {
        if (-not (Test-AcrRepositoryTag -RegistryName $RegistryName -Repository $pair.Repo -Tag $pair.Tag)) {
            $cached = $false
            break
        }
    }

    if ($cached) {
        Write-Host "Using existing ACR-cached .NET base images."
        return $true
    }

    if (-not $ImportBaseImages) {
        Write-Host "Skipping base image import (MCR/ACR rate limits). Build will pull from MCR directly."
        Write-Host "To cache bases in ACR first, rerun with -ImportBaseImages after rate limits cool down."
        return $false
    }

    $allReady = $true
    foreach ($pair in $pairs) {
        $target = "$($pair.Repo):$($pair.Tag)"
        if (Test-AcrRepositoryTag -RegistryName $RegistryName -Repository $pair.Repo -Tag $pair.Tag) {
            Write-Host "ACR already has $target"
            continue
        }

        Write-Host "Importing $($pair.Source) into ACR as $target..."
        $imported = $false
        for ($attempt = 1; $attempt -le 2; $attempt++) {
            $prev = $ErrorActionPreference
            $ErrorActionPreference = "Continue"
            $output = & $az acr import `
                --name $RegistryName `
                --source $pair.Source `
                --image $target `
                --force 2>&1
            $exitCode = $LASTEXITCODE
            $ErrorActionPreference = $prev
            $output | ForEach-Object { Write-Host $_ }
            if ($exitCode -eq 0) {
                $imported = $true
                break
            }
            Write-Host "Import attempt $attempt failed (429/401 = rate limit); waiting 90s..."
            Start-Sleep -Seconds 90
        }

        if (-not $imported) {
            Write-Host "Could not import $target - build will pull from MCR directly."
            $allReady = $false
        }
    }

    return $allReady
}

function Write-SafeHost {
    param([string]$Message)
    try {
        Write-Host $Message
    }
    catch {
        $ascii = [regex]::Replace($Message, "[^\x09\x0A\x0D\x20-\x7E]", "?")
        Write-Host $ascii
    }
}

function Wait-AcrBuildRun {
    param(
        [string]$RegistryName,
        [string]$RunId
    )

    $deadline = (Get-Date).AddMinutes(45)
    Write-SafeHost "Queued ACR build run $RunId. Polling status (logs suppressed to avoid Windows encoding errors)..."
    while ((Get-Date) -lt $deadline) {
        $prev = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $status = & $az acr task show-run --registry $RegistryName --run-id $RunId --query "status" -o tsv 2>$null
        $ErrorActionPreference = $prev
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($status)) {
            Start-Sleep -Seconds 20
            continue
        }

        $status = $status.Trim()
        if ($status -eq "Succeeded") {
            Write-SafeHost "ACR build run $RunId succeeded."
            return $true
        }

        if ($status -in @("Failed", "Canceled", "Timeout", "Error")) {
            Write-SafeHost "ACR build run $RunId ended with status: $status"
            $logPath = Join-Path $env:TEMP "label-verify-acr-$RunId.log"
            $ErrorActionPreference = "Continue"
            & $az acr task logs --registry $RegistryName --run-id $RunId 2>&1 |
                Out-File -FilePath $logPath -Encoding utf8
            $ErrorActionPreference = $prev
            if (Test-Path $logPath) {
                Write-SafeHost "Last lines of ACR build log ($logPath):"
                Get-Content $logPath -Tail 40 -ErrorAction SilentlyContinue | ForEach-Object { Write-SafeHost $_ }
            }
            return $false
        }

        Write-SafeHost "  Run $RunId status: $status ..."
        Start-Sleep -Seconds 25
    }

    throw "ACR build run $RunId did not finish within 45 minutes."
}

function ConvertFrom-AcrBuildOutput {
    param([object[]]$Output)

    $text = ($Output | ForEach-Object { "$_" }) -join [Environment]::NewLine
    $jsonStart = $text.IndexOf("{")
    if ($jsonStart -lt 0) {
        return $null
    }

    try {
        return ($text.Substring($jsonStart) | ConvertFrom-Json)
    }
    catch {
        return $null
    }
}

function Invoke-AcrCloudBuild {
    param(
        [string]$RegistryName,
        [string]$Image,
        [string]$Tag,
        [string]$StagingDir,
        [string[]]$BuildArgs
    )

    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $buildOutput = & $az acr build `
        --registry $RegistryName `
        --image "${Image}:${Tag}" `
        @BuildArgs `
        --file (Join-Path $StagingDir "Dockerfile") `
        --no-logs `
        --no-format `
        -o json `
        $StagingDir 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $prev

    $payload = ConvertFrom-AcrBuildOutput -Output $buildOutput
    if ($null -eq $payload) {
        $buildOutput | ForEach-Object { Write-SafeHost $_ }
        if ($exitCode -ne 0) {
            return $false
        }

        throw "Could not parse ACR build JSON from az acr build output."
    }

    $runId = [string]$payload.runId
    $status = [string]$payload.status
    if ($status -eq "Succeeded") {
        Write-SafeHost "ACR build run $runId succeeded."
        return $true
    }

    if ($status -in @("Failed", "Canceled", "Timeout", "Error")) {
        Write-SafeHost "ACR build run $runId ended with status: $status"
        return $false
    }

    if ($exitCode -ne 0) {
        $buildOutput | ForEach-Object { Write-SafeHost $_ }
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($runId)) {
        throw "ACR build did not return a run id."
    }

    return Wait-AcrBuildRun -RegistryName $RegistryName -RunId $runId
}

function Invoke-DockerBuildAcr {
    param(
        [string]$RegistryName,
        [string]$Image,
        [string]$Tag
    )
    $imageRef = "$RegistryName.azurecr.io/$Image`:$Tag"
    Write-SafeHost "Building in Azure Container Registry (avoids local MCR rate limits)..."

    $useAcrBases = Import-AcrDotnetBaseImages -RegistryName $RegistryName
    $buildArgs = @()
    if ($useAcrBases) {
        $buildArgs += "--build-arg", "DOTNET_SDK_IMAGE=$RegistryName.azurecr.io/base/dotnet-sdk:8.0-jammy"
        $buildArgs += "--build-arg", "DOTNET_RUNTIME_IMAGE=$RegistryName.azurecr.io/base/dotnet-aspnet:8.0-jammy"
        Write-SafeHost "Using ACR-cached .NET base images."
    }
    else {
        Write-SafeHost "Using public MCR .NET base images (import cache unavailable)."
    }

    $staging = New-AcrBuildStagingContext
    try {
        $built = $false
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            Write-SafeHost "==> ACR cloud build ($imageRef) attempt $attempt of 3"
            if (Invoke-AcrCloudBuild -RegistryName $RegistryName -Image $Image -Tag $Tag -StagingDir $staging -BuildArgs $buildArgs) {
                $built = $true
                break
            }

            if ($attempt -ge 3) {
                throw "ACR cloud build failed after 3 attempts."
            }

            Write-SafeHost "ACR build attempt $attempt failed; waiting 120s before retry (MCR throttling)..."
            Start-Sleep -Seconds 120
        }

        if (-not $built) {
            throw "ACR cloud build failed after 3 attempts."
        }
    }
    finally {
        Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-ImageBuild {
    param(
        [string]$RegistryName,
        [string]$Image,
        [string]$Tag
    )

    $fullImage = "$RegistryName.azurecr.io/${Image}:${Tag}"
    $script:ImageBuiltInAcr = $false

    if ($UseAcrBuild) {
        Invoke-DockerBuildAcr -RegistryName $RegistryName -Image $Image -Tag $Tag
        $script:ImageBuiltInAcr = $true
        return $fullImage
    }

    if ($LocalBuildOnly) {
        Invoke-DockerBuildLocal -Image $fullImage
        return $fullImage
    }

    $prevErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        Invoke-DockerBuildLocal -Image $fullImage
    }
    catch {
        Write-Host ""
        Write-Host "Local Docker build failed (often MCR 401/429). Retrying with ACR cloud build..."
        Write-Host ""
        Invoke-DockerBuildAcr -RegistryName $RegistryName -Image $Image -Tag $Tag
        $script:ImageBuiltInAcr = $true
    }
    finally {
        $ErrorActionPreference = $prevErrorAction
    }

    return $fullImage
}

function Invoke-ExternalStep {
    param([string]$Label, [scriptblock]$Action)
    Write-Host "==> $Label"
    if ($WhatIf) { return }
    $prevErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $Action 2>&1 | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) {
            throw "$Label failed (exit $LASTEXITCODE)"
        }
    }
    finally {
        $ErrorActionPreference = $prevErrorAction
    }
}

function Get-AzCli {
    $cmd = Get-Command az -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $default = "${env:ProgramFiles}\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
    if (Test-Path $default) { return $default }
    throw "Azure CLI not found. Install: winget install -e --id Microsoft.AzureCLI"
}

$az = Get-AzCli
Write-Host "Using Azure CLI: $az"

if ([string]::IsNullOrWhiteSpace($env:DEMO_AGENT_PASSWORD)) {
    throw "Set DEMO_AGENT_PASSWORD before deploy (see Azure Demo Login.txt)."
}

$account = & $az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Logging in to Azure..."
    & $az login | Out-Null
}

$ErrorActionPreference = "Continue"
$existingRgJson = & $az group show --name $ResourceGroup 2>$null
$rgExists = $LASTEXITCODE -eq 0 -and $existingRgJson
$ErrorActionPreference = "Stop"

if ($rgExists) {
    $Location = ($existingRgJson | ConvertFrom-Json).location
    Write-Host "Reusing resource group '$ResourceGroup' in $Location"
}
else {
    Invoke-AzStep "Resource group ($Location)" {
        & $az group create --name $ResourceGroup --location $Location | Out-Null
    }
}

if ([string]::IsNullOrWhiteSpace($PlanName)) {
    $ErrorActionPreference = "Continue"
    $serverFarmId = & $az webapp show --name $AppName --resource-group $ResourceGroup --query "serverFarmId" -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($serverFarmId)) {
        $serverFarmId = & $az webapp show --name $AppName --resource-group $ResourceGroup --query "appServicePlanId" -o tsv 2>$null
    }
    $plansInRg = @(
        & $az appservice plan list --resource-group $ResourceGroup --query "[].name" -o tsv 2>$null
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $ErrorActionPreference = "Stop"

    if ($LASTEXITCODE -eq 0 -and $serverFarmId) {
        $PlanName = ($serverFarmId -split "/")[-1]
        Write-Host "Reusing App Service plan from web app: $PlanName"
    }
    elseif ($plansInRg.Count -gt 0) {
        $PlanName = $plansInRg[0].Trim()
        Write-Host "Reusing App Service plan from resource group: $PlanName"
    }
    else {
        $PlanName = "label-verify-plan"
        Write-Host "No existing plan found; will create: $PlanName"
    }
}

$ErrorActionPreference = "Continue"
$planJson = & $az appservice plan show --name $PlanName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
$ErrorActionPreference = "Stop"
if ($planJson) {
    $Location = $planJson.location
    Write-Host "App Service plan location: $Location"
}

function Set-AppServicePlanSku {
    param(
        [string[]]$SkusToTry
    )

    $ErrorActionPreference = "Continue"
    & $az appservice plan show --name $PlanName --resource-group $ResourceGroup 1>$null 2>$null
    $planExists = $LASTEXITCODE -eq 0
    $ErrorActionPreference = "Stop"

    foreach ($trySku in $SkusToTry) {
        if ($planExists) {
            Write-Host "Updating App Service plan '$PlanName' to SKU $trySku..."
            & $az appservice plan update --name $PlanName --resource-group $ResourceGroup --sku $trySku 2>&1 | Out-Null
        }
        else {
            Write-Host "Creating App Service plan '$PlanName' ($trySku Linux) in $Location..."
            & $az appservice plan create `
                --name $PlanName `
                --resource-group $ResourceGroup `
                --location $Location `
                --is-linux `
                --sku $trySku 2>&1 | Out-Null
        }

        if ($LASTEXITCODE -eq 0) {
            $script:AppServiceSku = $trySku
            Write-Host "Using App Service SKU: $trySku"
            return
        }

        Write-Host "SKU $trySku unavailable (quota or subscription limit)."
    }

    throw @"
Could not create or update App Service plan. Your subscription reports Total VMs quota = 0 in $Location.

Options:
  1. Request quota: Azure Portal -> Subscriptions -> Usage + quotas -> search 'Total Regional vCPUs' for $Location -> Request increase (minimum 1).
  2. Upgrade subscription from free trial to Pay-As-You-Go, then retry.
  3. Deploy to an existing plan without SKU change:
       -AppServiceSku B1 -PlanName label-verify-plan-westus2
"@
}

if ([string]::IsNullOrWhiteSpace($AcrName)) {
    $existingAcr = & $az acr list --resource-group $ResourceGroup --query "[0].name" -o tsv 2>$null
    if ($LASTEXITCODE -eq 0 -and $existingAcr) {
        $AcrName = $existingAcr.Trim()
        Write-Host "Reusing existing ACR: $AcrName"
    }
    else {
        $suffix = (Get-Random -Maximum 99999).ToString("00000")
        $AcrName = ("labelverify$suffix").ToLower().Substring(0, [Math]::Min(16, ("labelverify$suffix").Length))
    }
}

Write-Host "==> ACR ($AcrName)"
if (-not $WhatIf) {
    $ErrorActionPreference = "Continue"
    & $az acr show --name $AcrName --resource-group $ResourceGroup 1>$null 2>$null
    $acrMissing = $LASTEXITCODE -ne 0
    $ErrorActionPreference = "Stop"
    if ($acrMissing) {
        & $az acr create --resource-group $ResourceGroup --name $AcrName --sku Basic --admin-enabled true | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "ACR create failed (exit $LASTEXITCODE)" }
    }
}

Write-Host "==> App Service plan ($AppServiceSku Linux)"
if (-not $WhatIf) {
    $skuFallbacks = switch -Regex ($AppServiceSku) {
        "^P2v3$" { @("P2v3", "P1v3", "B2", "B1") }
        "^P1v3$" { @("P1v3", "B2", "B1") }
        "^B2$"    { @("B2", "B1") }
        "^B1$"    { @("B1") }
        default   { @($AppServiceSku, "P1v3", "B1") }
    }
    Set-AppServicePlanSku -SkusToTry $skuFallbacks
}

Write-Host "==> Web app ($AppName)"
if (-not $WhatIf) {
    $ErrorActionPreference = "Continue"
    & $az webapp show --name $AppName --resource-group $ResourceGroup 1>$null 2>$null
    $appMissing = $LASTEXITCODE -ne 0
    $ErrorActionPreference = "Stop"
    if ($appMissing) {
        & $az webapp create --resource-group $ResourceGroup --plan $PlanName --name $AppName --deployment-container-image-name "mcr.microsoft.com/appsvc/staticsite:latest" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Web app create failed (exit $LASTEXITCODE)" }
    }
}

$imageName = "$AcrName.azurecr.io/label-verify-trackc:$ImageTag"
Write-Host "Building Docker image..."
if (-not $WhatIf) {
    $tessSrc = Join-Path (Split-Path -Parent $repoRoot) "treasury-label-verification\tessdata"
    $tessDst = Join-Path $repoRoot "tessdata"
    if (Test-Path $tessSrc) {
        Copy-Item -Path $tessSrc -Destination $tessDst -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path $tessDst -Force | Out-Null
        $engPath = Join-Path $tessDst "eng.traineddata"
        if (-not (Test-Path $engPath)) {
            Write-Host "Downloading tessdata/eng.traineddata..."
            Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata" -OutFile $engPath
        }
    }
    $script:ImageBuiltInAcr = $false
    $imageName = Invoke-ImageBuild -RegistryName $AcrName -Image "label-verify-trackc" -Tag $ImageTag
    if (-not $script:ImageBuiltInAcr) {
        Invoke-AzStep "ACR login" { & $az acr login --name $AcrName | Out-Null }
        Invoke-ExternalStep "Docker push" { docker push $imageName }
    }
    else {
        Write-Host "Image built in ACR (no local push required)."
    }
}

Write-Host "Configuring Web App for container..."
if (-not $WhatIf) {
    $acrUser = & $az acr credential show --name $AcrName --query username -o tsv
    $acrPass = & $az acr credential show --name $AcrName --query "passwords[0].value" -o tsv

    $prevErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & $az webapp config container set `
        --resource-group $ResourceGroup `
        --name $AppName `
        --docker-custom-image-name $imageName `
        --docker-registry-server-url "https://$AcrName.azurecr.io" `
        --docker-registry-server-user $acrUser `
        --docker-registry-server-password $acrPass 2>&1 | ForEach-Object { Write-Host $_ }

    & $az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $AppName `
        --settings `
            "WEBSITES_PORT=8082" `
            "ASPNETCORE_URLS=http://0.0.0.0:8082" `
            "ASPNETCORE_ENVIRONMENT=Production" `
            "Ocr__TessDataPath=/app/tessdata" `
            "Ocr__TimeoutSeconds=30" `
            "Ocr__FlatArtworkMaxOcrSide=1600" `
            "Ocr__UseFieldBandTargetedOcr=true" `
            "Ocr__SubmissionGradeTargetMs=6000" `
            "Ocr__PerLabelWallClockMs=15000" `
            "Ocr__SubmissionGradeSupplementWallClockMs=8000" `
            "Ocr__FlatArtworkEnginePoolSize=6" `
            "Ocr__MaxParallel=6" `
            "Layout__AnnotationsDir=/app/testdata/layout-annotations" `
            "Layout__ModelPath=/app/testdata/layout-models/label-layout-v1.onnx" `
            "Layout__PreferOnnx=false" `
            "Layout__RoiOcrBudgetMs=0" `
            "Layout__EnableGuidedRoiOcr=false" `
            "Cola__ColasDir=/app/testdata/colas" `
            "OCR_MAX_PARALLEL=6" `
            "OCR_MAX_PER_USER=6" `
            "SEED_DEMO_USERS=true" `
            "DISABLE_PUBLIC_REGISTRATION=true" `
            "Auth__AllowGetApprovalAction=false" `
            "Auth__ApprovalTokenTtlHours=1" `
            "DEMO_AGENT_PASSWORD=$($env:DEMO_AGENT_PASSWORD)" `
            "DEMO_PARALLEL_PASSWORD=$($env:DEMO_PARALLEL_PASSWORD)" `
            "SendGrid__PublicBaseUrl=https://$AppName.azurewebsites.net" 2>&1 | Out-Null

    & $az webapp update --resource-group $ResourceGroup --name $AppName --https-only true 2>&1 | Out-Null
    & $az webapp restart --resource-group $ResourceGroup --name $AppName 2>&1 | Out-Null
    $ErrorActionPreference = $prevErrorAction
}

$url = "https://$AppName.azurewebsites.net"
Write-Host ""
Write-Host "Deployed URL: $url"
Write-Host "Health: $url/health/live"
Write-Host "Login: demo.agent@label-verify.demo"
Write-Host ""

if (-not $WhatIf -and -not $SkipProbeGate) {
    Write-Host "Waiting 120s for container warm-up..."
    Start-Sleep -Seconds 120

    Write-Host "Running production reviewer probe (strict gate)..."
    $prevErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    python (Join-Path $repoRoot "scripts\_probe_production.py") --strict
    $probeExit = $LASTEXITCODE
    $ErrorActionPreference = $prevErrorAction

    if ($probeExit -ne 0) {
        throw "Strict probe gate failed (need 5/5 STD single + UI sequential outcomes; see table above). The container image was deployed successfully - re-run: python scripts/_probe_production.py --strict"
    }

    Write-Host "Production reviewer probe passed (5/5 STD single + UI sequential outcomes)."
}

Write-Host ""
Write-Host "Manual checks (optional):"
Write-Host "  .\scripts\smoke-production.ps1 -BaseUrl $url"
Write-Host "  .\scripts\run-benchmarks.ps1 -BaseUrl $url"
