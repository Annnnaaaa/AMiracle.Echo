<#
.SYNOPSIS
    Deploys AMiracle.Echo Host to Azure Container Apps (standard environment) with Neon + Azure Files.

.DESCRIPTION
    Run in Azure Cloud Shell (PowerShell). Safe to re-run.

    - Reuses the admin token and Neon connection string from an existing app named -AppName
      (e.g. a previous Express deployment), so nothing has to be pasted. Prompts only if none exist.
    - Creates a standard (workload profiles, Consumption) environment and mounts the Azure Files share
      BEFORE touching the old app; secrets are kept in ~/.echo-deploy-secrets.json until the new app exists.
    - Replaces an Express app (asks first), creates the app (min 0 / max 1 replicas, external HTTPS
      ingress on port 8080), then deletes the old Express environment.
    - Creates a monthly cost budget with email alerts at 80% and 100% (skip with -SkipBudget).
    - Prints the DNS records for your custom domain. Then run add-domain.ps1.

.EXAMPLE
    ./deploy.ps1 -Domain echo.example.com
.EXAMPLE
    ./deploy.ps1 -Domain echo.example.com -BudgetAmount 5 -AlertEmail me@example.com
#>
param(
    [string]$Domain,
    [decimal]$BudgetAmount = 10,
    [string]$AlertEmail,
    [switch]$SkipBudget,
    [string]$ResourceGroup = 'AMiracle',
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
    return [bool]$Resource -and "$($Resource.properties.environmentMode)" -eq 'Express'
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

function Set-MonthlyBudget([decimal]$Amount, [string]$Email) {
    $account = Invoke-AzJson account show
    if (-not $Email) { $Email = $account.user.name }
    if ($Email -notmatch '@') {
        Write-Host "  Skipped: couldn't find your email. Re-run with -AlertEmail you@example.com" -ForegroundColor Yellow
        return
    }
    $url = "https://management.azure.com/subscriptions/$($account.id)/providers/Microsoft.Consumption/budgets/echo-monthly?api-version=2023-05-01"
    if (Invoke-AzTryJson rest --method get --url $url) {
        Write-Host '  Budget "echo-monthly" already exists - left unchanged.'
        return
    }
    $notification = { param($threshold) @{ enabled = $true; operator = 'GreaterThanOrEqualTo'; threshold = $threshold; thresholdType = 'Actual'; contactEmails = @($Email) } }
    $body = @{
        properties = @{
            category      = 'Cost'
            amount        = $Amount
            timeGrain     = 'Monthly'
            timePeriod    = @{ startDate = ('{0:yyyy-MM}-01T00:00:00Z' -f (Get-Date).ToUniversalTime()) }
            notifications = @{ actual80 = (& $notification 80); actual100 = (& $notification 100) }
        }
    } | ConvertTo-Json -Depth 10
    $bodyPath = Join-Path ([IO.Path]::GetTempPath()) ("echo-budget-" + [guid]::NewGuid() + '.json')
    try {
        Set-Content -Path $bodyPath -Value $body -Encoding utf8NoBOM
        $PSNativeCommandUseErrorActionPreference = $false
        az rest --method put --url $url --body "@$bodyPath" --only-show-errors -o none
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  Budget `$$Amount/month created; alerts at 80% and 100% go to $Email" -ForegroundColor Green
        } else {
            Write-Host '  Budget could not be created (see error above). Create it in Portal: Cost Management > Budgets.' -ForegroundColor Yellow
        }
    } finally {
        Remove-Item -Path $bodyPath -Force -ErrorAction SilentlyContinue
    }
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

if (-not $SkipBudget) {
    Write-Step 'Budget alert'
    Set-MonthlyBudget $BudgetAmount $AlertEmail
}

$adminToken = $null
$neonConn = $null
$generatedToken = $false
$oldEnvId = $null
$backupPath = Join-Path $HOME '.echo-deploy-secrets.json'

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
    $oldEnvId = $existingEnvId

    Write-Step "Reading secrets from existing Express app '$AppName'"
    $secrets = Invoke-AzTryJson containerapp secret list --resource-group $ResourceGroup --name $AppName --show-values
    $adminToken = Get-AppSetting $existingApp $secrets 'AMiracle__Echo__AdminToken' 'admin-token'
    $neonConn = Get-AppSetting $existingApp $secrets 'AMiracle__Echo__Database__ConnectionString' 'neon-conn'
    Write-Host ("  admin token:       " + $(if ($adminToken) { 'found' } else { 'NOT found' }))
    Write-Host ("  Neon connection:   " + $(if ($neonConn) { 'found' } else { 'NOT found' }))
}

if ((-not $adminToken -or -not $neonConn) -and (Test-Path $backupPath)) {
    $backup = Get-Content -Path $backupPath -Raw | ConvertFrom-Json
    if (-not $adminToken) { $adminToken = $backup.adminToken }
    if (-not $neonConn) { $neonConn = $backup.neonConn }
    $generatedToken = $adminToken -eq $backup.adminToken -and [bool]$backup.generated
    Write-Host "  Using secrets saved by a previous run ($backupPath)"
}
if (-not $adminToken) {
    $adminToken = Read-Host 'Admin token (paste it, or press Enter to generate a new one)' -MaskInput
    if (-not $adminToken) {
        $adminToken = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
        $generatedToken = $true
    }
}
if (-not $neonConn) { $neonConn = Read-Host 'Neon .NET connection string (paste, input hidden)' -MaskInput }
if (-not $neonConn) { throw 'The Neon connection string is required.' }
if ($neonConn -notmatch 'Check Certificate Revocation') {
    $neonConn = $neonConn.TrimEnd(';', ' ') + ';Check Certificate Revocation=true'
}
@{ adminToken = $adminToken; neonConn = $neonConn; generated = $generatedToken } | ConvertTo-Json | Set-Content -Path $backupPath -Encoding utf8NoBOM
if ($IsLinux) { chmod 600 $backupPath }

if (-not $Location) { $Location = $rg.location }
$Location = ($Location -replace '\s', '').ToLowerInvariant()

$envObj = Invoke-AzTryJson containerapp env show --resource-group $ResourceGroup --name $EnvironmentName
if (Test-IsExpress $envObj) {
    if ($oldEnvId -and $envObj.id -eq $oldEnvId) {
        throw "Environment '$EnvironmentName' is the Express environment of '$AppName'. Re-run with -EnvironmentName <another-name>."
    }
    $appsInEnv = @(Invoke-AzJson containerapp list --resource-group $ResourceGroup | Where-Object {
        "$($_.properties.environmentId)$($_.properties.managedEnvironmentId)" -eq $envObj.id })
    if ($appsInEnv.Count -gt 0) {
        throw "Environment '$EnvironmentName' is Express and still has apps ($($appsInEnv.name -join ', ')). Re-run with -EnvironmentName <another-name>."
    }
    Write-Host "Environment '$EnvironmentName' is an empty Express environment and must be replaced by a standard one." -ForegroundColor Yellow
    if ((Read-Host "Type 'yes' to delete it") -ne 'yes') { throw 'Cancelled.' }
    Write-Step "Deleting Express environment '$EnvironmentName' (takes a few minutes)"
    az resource delete --ids $envObj.id --only-show-errors -o none
    $envObj = $null
}
if (-not $envObj) {
    Write-Step "Creating standard environment '$EnvironmentName' in $Location (takes a few minutes)"
    az containerapp env create --resource-group $ResourceGroup --name $EnvironmentName --location $Location `
        --environment-mode WorkloadProfiles --logs-destination none --only-show-errors -o none
    $envObj = Invoke-AzJson containerapp env show --resource-group $ResourceGroup --name $EnvironmentName
    if (Test-IsExpress $envObj) { throw "Azure created '$EnvironmentName' as Express despite --environment-mode WorkloadProfiles. Your app was NOT touched." }
} else {
    Write-Step "Using existing environment '$EnvironmentName'"
    for ($i = 0; $i -lt 120 -and "$($envObj.properties.provisioningState)" -notin @('Succeeded', 'Failed', ''); $i++) {
        if ($i -eq 0) { Write-Host "  still being created ($($envObj.properties.provisioningState)) - waiting..." }
        Start-Sleep -Seconds 15
        $envObj = Invoke-AzJson containerapp env show --resource-group $ResourceGroup --name $EnvironmentName
    }
    if ("$($envObj.properties.provisioningState)" -eq 'Failed') { throw "Environment '$EnvironmentName' failed to provision. Delete it in Portal and re-run." }
}
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

if ($existingApp) {
    Write-Host ''
    Write-Host "About to DELETE the Express container app '$AppName' and recreate it in '$EnvironmentName'." -ForegroundColor Yellow
    Write-Host "Neon data, the storage account and the file share are NOT touched. Secrets are kept in $backupPath until the new app exists."
    if ((Read-Host "Type 'yes' to continue") -ne 'yes') { throw 'Cancelled.' }
    Write-Step "Deleting Express app '$AppName'"
    az containerapp delete --resource-group $ResourceGroup --name $AppName --yes --only-show-errors -o none
}

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
Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue

if ($oldEnvId) {
    Write-Step "Deleting old Express environment '$(($oldEnvId -split '/')[-1])' (takes a few minutes)"
    $PSNativeCommandUseErrorActionPreference = $false
    az resource delete --ids $oldEnvId --only-show-errors -o none
    if ($LASTEXITCODE -ne 0) { Write-Host '  Could not delete it - delete it in Portal later (it costs nothing while empty).' -ForegroundColor Yellow }
    $PSNativeCommandUseErrorActionPreference = $true
}

if ($generatedToken) {
    Write-Host ''
    Write-Host 'New admin token (save it in a password manager; also in Portal > app > Settings > Secrets > admin-token):' -ForegroundColor Yellow
    Write-Host "  $adminToken"
}
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
