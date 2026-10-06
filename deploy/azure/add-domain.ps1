<#
.SYNOPSIS
    Binds a custom domain with a free managed certificate to the Echo container app.

.DESCRIPTION
    Run in Azure Cloud Shell (PowerShell) after deploy.ps1, once the CNAME and asuid TXT records it
    printed exist at your DNS provider. Safe to re-run.

.EXAMPLE
    ./add-domain.ps1 -Domain echo.example.com
#>
param(
    [Parameter(Mandatory)][string]$Domain,
    [string]$ResourceGroup = 'rg-echo',
    [string]$AppName = 'echo-host',
    [string]$EnvironmentName = 'echo-env'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

function Resolve-PublicDns([string]$Name, [string]$Type) {
    $typeCode = @{ CNAME = 5; TXT = 16 }[$Type]
    try {
        $r = Invoke-RestMethod -Uri "https://dns.google/resolve?name=$Name&type=$Type" -TimeoutSec 15
        @($r.Answer | Where-Object { $_.type -eq $typeCode } | ForEach-Object { $_.data.Trim('"').TrimEnd('.') })
    } catch { @() }
}

$Domain = $Domain.Trim().TrimEnd('.').ToLowerInvariant()
$app = (az containerapp show --resource-group $ResourceGroup --name $AppName -o json --only-show-errors) -join "`n" | ConvertFrom-Json -Depth 50
$fqdn = $app.properties.configuration.ingress.fqdn

Write-Host "`n==> Checking DNS for $Domain" -ForegroundColor Cyan
$cname = Resolve-PublicDns $Domain 'CNAME' | Select-Object -First 1
$txt = (Resolve-PublicDns "asuid.$Domain" 'TXT') -join ' '
Write-Host "  CNAME $Domain -> $(if ($cname) { $cname } else { '(not found yet)' })   expected: $fqdn"
Write-Host "  TXT   asuid.$Domain -> $(if ($txt) { $txt } else { '(not found yet)' })"
if (-not $cname -or -not $txt) {
    Write-Host '  DNS records not visible yet. Wait a few minutes after adding them and re-run.' -ForegroundColor Yellow
    return
}

$bound = @((az containerapp hostname list --resource-group $ResourceGroup --name $AppName -o json --only-show-errors) -join "`n" | ConvertFrom-Json -Depth 50) |
    Where-Object { $_.name -eq $Domain }

if (-not $bound) {
    Write-Host "`n==> Adding hostname" -ForegroundColor Cyan
    az containerapp hostname add --resource-group $ResourceGroup --name $AppName --hostname $Domain --only-show-errors -o none
}

if (-not $bound -or $bound.bindingType -ne 'SniEnabled') {
    Write-Host "`n==> Issuing free managed certificate and binding (several minutes)" -ForegroundColor Cyan
    az containerapp hostname bind --resource-group $ResourceGroup --name $AppName --environment $EnvironmentName `
        --hostname $Domain --validation-method CNAME --only-show-errors -o none
}

Write-Host "`nDone: https://$Domain/echo/admin" -ForegroundColor Green
