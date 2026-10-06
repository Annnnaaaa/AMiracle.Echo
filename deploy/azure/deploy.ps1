<#
.SYNOPSIS
    Deploys AMiracle.Echo Host to Azure Container Apps (standard environment) with Neon + Azure Files.

.DESCRIPTION
    Run in Azure Cloud Shell (PowerShell). Safe to re-run.

    - Reuses the admin token and Neon connection string from an existing app named -AppName
      (e.g. a previous Express deployment), so nothing has to be pasted. Prompts only if none exist.
    - Deletes that app and its environment if the environment is Express (asks first).
    - Creates a standard (workload profiles, Consumption) environment, mounts the Azure Files share,
      and creates the app (min 0 / max 1 replicas, external HTTPS ingress on port 8080).
    - Prints the DNS records for your custom domain. Then run add-domain.ps1.

.EXAMPLE
    ./deploy.ps1 -Domain echo.example.com
#>
param(
    [string]$Domain,
    [string]$ResourceGroup = 'rg-echo',
    [string]$AppName = 'echo-host',
    [string]$EnvironmentName = 'echo-env',
    [string]$Image = 'ghcr.io/annnnaaaa/amiracle-echo:latest',
    [string]$FileShareName = 'echo-blobs',
    [string]$Location
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

function Write-Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }

function Invoke-AzJson {
    $out = & az @args -o json --only-show-errors
    if ($out) { ($out -join "`n") | ConvertFrom-Json -Depth 50 }
}

function Invoke-AzTryJson {
    $PSNativeCommandUseErrorActionPreference = $false
    $out = & az @args -o json --only-show-errors 2>$null
    if ($LASTEXITCODE -eq 0 -and $out) { ($out -join "`n") | ConvertFrom-Json -Depth 50 } else { $null }
}

function Test-IsExpress($Resource) {
    if (-not $Resource) { return $false }
    if ("$($Resource.properties.environmentMode)" -eq 'Express') { return $true }
    return (($Resource | ConvertTo-Json -Depth 50 -Compress) -match '"express"')
}

function ConvertTo-YamlString([string]$Value) { "'" + $Value.Replace("'", "''") + "'" }

function Get-AppSetting($App, $Secrets, [string]$EnvVarName, [string]$FallbackSecretName) {
    $entry = $App.properties.template.containers |
        ForEach-Object { $_.env } |
        Where-Object { $_ -and $_.name -eq $EnvVarName } |
        Select-Object -First 1
    if ($entry -and -not $entry.secretRef) { return $entry.value }
    $secretName = if ($entry) { $entry.secretRef } else { $FallbackSecretName }
    ($Secrets | Where-Object { $_.name -eq $secretName } | Select-Object -First 1).value
}

function Write-DnsInstructions($App, [string]$DomainName) {
    $fqdn = $App.properties.configuration.ingress.fqdn
    $verificationId = $App.properties.customDomainVerificationId
    $name = if ($DomainName) { $DomainName } else { 'echo.<your-domain>' }
    Write-Host ''
    Write-Host 'Add these two DNS records at your DNS provider:' -ForegroundColor Yellow
    Write-Host "  CNAME  $name  ->  $fqdn"
    Write-Host "  TXT    asuid.$name  ->  $verificationId"
    Write-Host '  (Most DNS providers want only the part before your root domain as "host", e.g. "echo" and "asuid.echo".)'
    Write-Host '  Cloudflare: set the CNAME to "DNS only" (grey cloud).'
    Write-Host '  If your domain has CAA records, also add: CAA 0 issue "digicert.com"'
    Write-Host ''
    Write-Host "Then run:  ./add-domain.ps1 -Domain $name" -ForegroundColor Yellow
}

Write-Step 'Preparing Azure CLI'
az extension add --name containerapp --upgrade --only-show-errors
az provider register --namespace Microsoft.App --wait --only-show-errors
az provider register --namespace Microsoft.Storage --wait --only-show-errors

$rg = Invoke-AzTryJson group show --name $ResourceGroup
if (-not $rg) {
    if (-not $Location) { throw "Resource group '$ResourceGroup' doesn't exist. Re-run with -Location (e.g. -Location germanywestcentral)." }
    Write-Step "Creating resource group $ResourceGroup"
    az group create --name $ResourceGroup --location $Location --only-show-errors -o none
    $rg = Invoke-AzJson group show --name $ResourceGroup
}

$adminToken = $null
$neonConn = $null

$existingApp = Invoke-AzTryJson containerapp show --resource-group $ResourceGroup --name $AppName
if ($existingApp) {
    $existingEnvId = if ($existingApp.properties.environmentId) { $existingApp.properties.environmentId } else { $existingApp.properties.managedEnvironmentId }
    $existingEnv = Invoke-AzTryJson resource show --ids $existingEnvId
    if (-not $Location) { $Location = $existingApp.location }

    if (-not (Test-IsExpress $existingEnv)) {
        Write-Step "'$AppName' already runs in a standard environment - nothing to recreate"
        Write-Host "App URL: https://$($existingApp.properties.configuration.ingress.fqdn)/echo/admin"
        Write-DnsInstructions $existingApp $Domain
        return
    }

    Write-Step "Reading secrets from existing Express app '$AppName'"
    $secrets = Invoke-AzTryJson containerapp secret list --resource-group $ResourceGroup --name $AppName --show-values
    $adminToken = Get-AppSetting $existingApp $secrets 'AMiracle__Echo__AdminToken' 'admin-token'
    $neonConn = Get-AppSetting $existingApp $secrets 'AMiracle__Echo__Database__ConnectionString' 'neon-conn'
    Write-Host ("  admin token:       " + $(if ($adminToken) { 'found' } else { 'NOT found' }))
    Write-Host ("  Neon connection:   " + $(if ($neonConn) { 'found' } else { 'NOT found' }))
}

if (-not $adminToken) { $adminToken = Read-Host 'Admin token (paste, input hidden)' -MaskInput }
if (-not $neonConn) { $neonConn = Read-Host 'Neon .NET connection string (paste, input hidden)' -MaskInput }
if (-not $adminToken -or -not $neonConn) { throw 'Admin token and Neon connection string are required.' }
if ($neonConn -notmatch 'Check Certificate Revocation') {
    $neonConn = $neonConn.TrimEnd(';', ' ') + ';Check Certificate Revocation=true'
}

if (-not $Location) { $Location = $rg.location }
$Location = ($Location -replace '\s', '').ToLowerInvariant()

if ($existingApp) {
    $existingEnvName = ($existingEnvId -split '/')[-1]
    Write-Host ''
    Write-Host "About to DELETE container app '$AppName' and its Express environment '$existingEnvName'." -ForegroundColor Yellow
    Write-Host 'Neon data, the storage account and the file share are NOT touched.'
    if ((Read-Host "Type 'yes' to continue") -ne 'yes') { throw 'Cancelled.' }

    Write-Step "Deleting app '$AppName'"
    az containerapp delete --resource-group $ResourceGroup --name $AppName --yes --only-show-errors -o none
    Write-Step "Deleting Express environment '$existingEnvName' (takes a few minutes)"
    az resource delete --ids $existingEnvId --only-show-errors -o none
}

$envObj = Invoke-AzTryJson containerapp env show --resource-group $ResourceGroup --name $EnvironmentName
if ($envObj -and (Test-IsExpress $envObj)) {
    Write-Host "Environment '$EnvironmentName' is an Express environment and must be replaced." -ForegroundColor Yellow
    if ((Read-Host "Type 'yes' to delete it") -ne 'yes') { throw 'Cancelled.' }
    az resource delete --ids $envObj.id --only-show-errors -o none
    $envObj = $null
}
if (-not $envObj) {
    Write-Step "Creating standard environment '$EnvironmentName' in $Location (takes a few minutes)"
    az containerapp env create --resource-group $ResourceGroup --name $EnvironmentName --location $Location --logs-destination none --only-show-errors -o none
    $envObj = Invoke-AzJson containerapp env show --resource-group $ResourceGroup --name $EnvironmentName
}
if (Test-IsExpress $envObj) { throw "Environment '$EnvironmentName' is still Express - aborting." }
$useConsumptionProfile = [bool]($envObj.properties.workloadProfiles | Where-Object { $_.name -eq 'Consumption' })

Write-Step 'Finding storage account and file share'
$storageAccount = $null
$accounts = @(Invoke-AzJson storage account list --resource-group $ResourceGroup)
foreach ($acct in $accounts) {
    if (Invoke-AzTryJson storage share-rm show --resource-group $ResourceGroup --storage-account $acct.name --name $FileShareName) {
        $storageAccount = $acct.name
        break
    }
}
if (-not $storageAccount) {
    if ($accounts.Count -gt 0) {
        $storageAccount = $accounts[0].name
    } else {
        $storageAccount = 'echostorage' + -join ((48..57) + (97..122) | Get-Random -Count 8 | ForEach-Object { [char]$_ })
        Write-Step "Creating storage account $storageAccount"
        az storage account create --resource-group $ResourceGroup --name $storageAccount --location $Location --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 --only-show-errors -o none
    }
    Write-Step "Creating file share '$FileShareName' in $storageAccount"
    az storage share-rm create --resource-group $ResourceGroup --storage-account $storageAccount --name $FileShareName --quota 5 --only-show-errors -o none
}
Write-Host "  using $storageAccount / $FileShareName"
$storageKey = (Invoke-AzJson storage account keys list --resource-group $ResourceGroup --account-name $storageAccount)[0].value

Write-Step 'Connecting the file share to the environment'
az containerapp env storage set --resource-group $ResourceGroup --name $EnvironmentName `
    --storage-name $FileShareName `
    --azure-file-account-name $storageAccount `
    --azure-file-account-key $storageKey `
    --azure-file-share-name $FileShareName `
    --access-mode ReadWrite `
    --only-show-errors -o none

Write-Step "Creating container app '$AppName'"
$profileLine = if ($useConsumptionProfile) { '  workloadProfileName: Consumption' } else { '' }
$yaml = @"
location: $Location
properties:
  environmentId: $($envObj.id)
$profileLine
  configuration:
    activeRevisionsMode: Single
    ingress:
      external: true
      targetPort: 8080
      allowInsecure: false
    secrets:
      - name: admin-token
        value: $(ConvertTo-YamlString $adminToken)
      - name: neon-conn
        value: $(ConvertTo-YamlString $neonConn)
  template:
    containers:
      - name: $AppName
        image: $Image
        resources:
          cpu: 0.25
          memory: 0.5Gi
        env:
          - name: AMiracle__Echo__AdminToken
            secretRef: admin-token
          - name: AMiracle__Echo__Database__Provider
            value: postgres
          - name: AMiracle__Echo__Database__ConnectionString
            secretRef: neon-conn
        volumeMounts:
          - volumeName: blobs
            mountPath: /data/blobs
    scale:
      minReplicas: 0
      maxReplicas: 1
    volumes:
      - name: blobs
        storageType: AzureFile
        storageName: $FileShareName
"@
$yamlPath = Join-Path ([IO.Path]::GetTempPath()) ("echo-app-" + [guid]::NewGuid() + '.yaml')
try {
    Set-Content -Path $yamlPath -Value $yaml -Encoding utf8NoBOM
    az containerapp create --resource-group $ResourceGroup --name $AppName --yaml $yamlPath --only-show-errors -o none
} finally {
    Remove-Item -Path $yamlPath -Force -ErrorAction SilentlyContinue
}

$app = Invoke-AzJson containerapp show --resource-group $ResourceGroup --name $AppName
$url = "https://$($app.properties.configuration.ingress.fqdn)/echo/admin"

Write-Step 'Waiting for the app to answer (first start can take a minute)'
$ok = $false
for ($i = 0; $i -lt 18 -and -not $ok; $i++) {
    try {
        $status = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 20).StatusCode
        $ok = $status -eq 200
    } catch {
        Start-Sleep -Seconds 10
    }
}
if ($ok) {
    Write-Host "  OK: $url" -ForegroundColor Green
} else {
    Write-Host "  App didn't answer yet. Check logs:  az containerapp logs show -g $ResourceGroup -n $AppName --follow" -ForegroundColor Yellow
}

Write-DnsInstructions $app $Domain
