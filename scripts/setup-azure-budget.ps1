param(
    [double]$MonthlyLimitUsd = 30,
    [string]$BudgetName = "trackc-monthly-cap",
    [Parameter(Mandatory = $true)]
    [string]$ContactEmail,
    [string]$SubscriptionId = ""
)

$ErrorActionPreference = "Stop"

function Get-AzCli {
    $cmd = Get-Command az -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $default = "${env:ProgramFiles}\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
    if (Test-Path $default) { return $default }
    throw "Azure CLI not found."
}

$az = Get-AzCli
if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $SubscriptionId = & $az account show --query id -o tsv
}

$startDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-01")
$endDate = (Get-Date).ToUniversalTime().AddYears(2).ToString("yyyy-MM-01")

$body = @{
    properties = @{
        category = "Cost"
        amount = $MonthlyLimitUsd
        timeGrain = "Monthly"
        timePeriod = @{
            startDate = $startDate
            endDate = $endDate
        }
        notifications = @{
            Actual_GreaterThan_50_Percent = @{
                enabled = $true
                operator = "GreaterThan"
                threshold = 50
                contactEmails = @($ContactEmail)
                contactRoles = @("Owner")
            }
            Actual_GreaterThan_80_Percent = @{
                enabled = $true
                operator = "GreaterThan"
                threshold = 80
                contactEmails = @($ContactEmail)
                contactRoles = @("Owner")
            }
            Actual_GreaterThan_100_Percent = @{
                enabled = $true
                operator = "GreaterThan"
                threshold = 100
                contactEmails = @($ContactEmail)
                contactRoles = @("Owner")
            }
            Forecasted_GreaterThan_100_Percent = @{
                enabled = $true
                operator = "GreaterThan"
                threshold = 100
                contactEmails = @($ContactEmail)
                contactRoles = @("Owner")
            }
        }
    }
} | ConvertTo-Json -Depth 8

Write-Host "Creating monthly cost budget '$BudgetName' at `$$MonthlyLimitUsd for subscription $SubscriptionId"
$uri = "https://management.azure.com/subscriptions/$SubscriptionId/providers/Microsoft.CostManagement/budgets/${BudgetName}?api-version=2023-11-01"
& $az rest --method put --uri $uri --body $body | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Budget creation failed (exit $LASTEXITCODE)"
}

Write-Host "Budget configured with email alerts at 50%, 80%, 100% actual and 100% forecast."
Write-Host "Note: Azure budgets alert on spend; they do not hard-stop billing. Review Cost Management in the portal to add action groups if needed."
