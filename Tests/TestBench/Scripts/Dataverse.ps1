# Shared helper: connects to the test environment (Web API, client credentials).
# Dot-source it: . "$PSScriptRoot\Dataverse.ps1"
#
# The connection string (AuthType=ClientSecret;Url=...;ClientId=...;ClientSecret=...) comes from the
# AUTODEPLOYER_TEST_CONNECTION environment variable, or else from Tests\connection.local.txt,
# which is gitignored. The integration tests use the same two sources.

$ErrorActionPreference = 'Stop'
$ConnectionFile = Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'connection.local.txt'

$cs = $env:AUTODEPLOYER_TEST_CONNECTION
if (-not $cs) {
    if (-not (Test-Path $ConnectionFile)) { throw "Set AUTODEPLOYER_TEST_CONNECTION or create $ConnectionFile" }
    $cs = Get-Content $ConnectionFile -Raw
}
$parts = @{}
foreach ($p in $cs.Trim().Split(';')) { if ($p -match '^\s*([^=]+)=(.*)$') { $parts[$matches[1].Trim()] = $matches[2].Trim() } }
$script:DvUrl = $parts['Url'].TrimEnd('/')

$tenant = $null
try { Invoke-WebRequest "$DvUrl/api/data/v9.2/" -UseBasicParsing | Out-Null }
catch {
    if ($_.Exception.Response.Headers['WWW-Authenticate'] -match 'authorization_uri=https://login\.microsoftonline\.com/([^/,"]+)') { $tenant = $matches[1] }
}
if (-not $tenant) { throw "Could not determine the tenant for $DvUrl" }

$token = (Invoke-RestMethod -Method Post "https://login.microsoftonline.com/$tenant/oauth2/v2.0/token" -Body @{
        grant_type = 'client_credentials'; client_id = $parts['ClientId']; client_secret = $parts['ClientSecret']; scope = "$DvUrl/.default"
    }).access_token

function Invoke-Dv {
    param(
        [string]$Method = 'Get',
        [string]$Path,
        $Body,
        [string]$Solution
    )
    $headers = @{ Authorization = "Bearer $token"; Accept = 'application/json'; 'OData-Version' = '4.0'; 'OData-MaxVersion' = '4.0' }
    if ($Solution) { $headers['MSCRM.SolutionUniqueName'] = $Solution }
    if ($Method -eq 'Post') { $headers['Prefer'] = 'return=representation' }
    $request = @{ Method = $Method; Uri = "$DvUrl/api/data/v9.2/$Path"; Headers = $headers }
    if ($null -ne $Body) {
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 6 -Compress))
        $request.ContentType = 'application/json; charset=utf-8'
    }
    Invoke-RestMethod @request
}

function Get-DvFirst([string]$Path) {
    (Invoke-Dv -Path $Path).value | Select-Object -First 1
}
