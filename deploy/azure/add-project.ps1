<#
.SYNOPSIS
    Creates an Echo project (one per app that embeds the widget) and prints its widget snippet.

.DESCRIPTION
    Run in Azure Cloud Shell (PowerShell) after deploy.ps1. Reads the admin token from the container app's
    secrets, so nothing has to be pasted. If a project with the same name exists, prints its snippet instead.

.EXAMPLE
    ./add-project.ps1 -Name "My App" -Origins https://myapp.com,http://localhost:5173
.EXAMPLE
    ./add-project.ps1 -Name "My Mobile App" -Origins app://com.company.myapp
    Native apps send no Origin header on their own; the app must send this value as its Origin header.
#>
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string[]]$Origins,
    [string]$ResourceGroup = 'AMiracle',
    [string]$AppName = 'echo-host'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$Origins = @($Origins | ForEach-Object { $_.Trim().TrimEnd('/') } | Where-Object { $_ })
$bad = @($Origins | Where-Object { $_ -notmatch '^[a-zA-Z][a-zA-Z0-9+.-]*://[^/\s]+$' })
if ($bad.Count -gt 0) { throw "Origins must look like https://myapp.com or app://com.company.app (scheme + host, no path). Invalid: $($bad -join ', ')" }

$app = (az containerapp show --resource-group $ResourceGroup --name $AppName -o json --only-show-errors) -join "`n" | ConvertFrom-Json -Depth 50
$customHost = @($app.properties.configuration.ingress.customDomains | Where-Object { $_.bindingType -eq 'SniEnabled' }) | Select-Object -First 1
$echoHost = if ($customHost) { $customHost.name } else { $app.properties.configuration.ingress.fqdn }

$secrets = (az containerapp secret list --resource-group $ResourceGroup --name $AppName --show-values -o json --only-show-errors) -join "`n" | ConvertFrom-Json -Depth 50
$token = ($secrets | Where-Object { $_.name -eq 'admin-token' } | Select-Object -First 1).value
if (-not $token) { throw "Secret 'admin-token' not found on app '$AppName'." }

$api = "https://$echoHost/api/v1/admin/projects"
$headers = @{ Authorization = "Bearer $token" }

function Invoke-EchoApi([string]$Method, [string]$Body) {
    for ($attempt = 1; ; $attempt++) {
        try {
            if ($Body) { return Invoke-RestMethod -Method $Method -Uri $api -Headers $headers -ContentType 'application/json' -Body $Body -TimeoutSec 60 }
            return Invoke-RestMethod -Method $Method -Uri $api -Headers $headers -TimeoutSec 60
        } catch {
            $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
            if ($attempt -ge 4 -or ($status -ge 400 -and $status -lt 500)) { throw }
            Write-Host "  Echo isn't answering yet (it may be starting from zero) - retrying..."
            Start-Sleep -Seconds 10
        }
    }
}

$project = @(Invoke-EchoApi 'Get') | Where-Object { $_.name -eq $Name } | Select-Object -First 1
if ($project) {
    Write-Host "Project '$Name' already exists (allowed origins: $($project.allowedOrigins -join ', '))." -ForegroundColor Yellow
} else {
    $project = Invoke-EchoApi 'Post' (@{ name = $Name; allowedOrigins = $Origins } | ConvertTo-Json)
    Write-Host "Created project '$Name' (allowed origins: $($Origins -join ', '))." -ForegroundColor Green
}

Write-Host "`nPaste this into your app's HTML:`n" -ForegroundColor Cyan
Write-Host @"
<script src="https://$echoHost/echo/widget.js"
        data-project-id="$($project.id)"
        data-public-key="$($project.publicKey)"
        defer></script>
"@
