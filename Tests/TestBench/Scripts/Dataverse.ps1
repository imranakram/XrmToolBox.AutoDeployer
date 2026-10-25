# Shared helper: reads the test bench settings and connects to the test environment (Web API,
# client credentials). Dot-source it: . "$PSScriptRoot\Dataverse.ps1"
#
# Settings come from Tests\testbench.local.json (gitignored), each one overridable by an environment
# variable. The integration tests read them the same way.
#   connection  AUTODEPLOYER_TEST_CONNECTION  AuthType=ClientSecret;Url=...;ClientId=...;ClientSecret=...
#   publisher   AUTODEPLOYER_TEST_PUBLISHER   unique name of the publisher for the test components
#   prefix      AUTODEPLOYER_TEST_PREFIX      that publisher's customization prefix
# Without publisher/prefix, the test bench uses its own publisher AutoDeployTest (prefix adt).

$ErrorActionPreference = 'Stop'

$SettingsFile = Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'testbench.local.json'
$settings = if (Test-Path $SettingsFile) { Get-Content $SettingsFile -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
function Get-Setting([string]$Name, [string]$Variable, [string]$Default) {
    $value = [Environment]::GetEnvironmentVariable($Variable)
    if (-not $value) { $value = $settings.$Name }
    if (-not $value) { $value = $Default }
    "$value".Trim()
}

$cs = Get-Setting 'connection' 'AUTODEPLOYER_TEST_CONNECTION' ''
if (-not $cs) { throw "No connection string: set AUTODEPLOYER_TEST_CONNECTION or create $SettingsFile" }
$script:PublisherName = Get-Setting 'publisher' 'AUTODEPLOYER_TEST_PUBLISHER' 'AutoDeployTest'
$script:Prefix = Get-Setting 'prefix' 'AUTODEPLOYER_TEST_PREFIX' 'adt'

# Names of everything the test bench registers. The integration tests and the sample projects use the same.
$script:SolutionName = 'AutoDeployTest'
$script:AssemblyName = 'AutoDeployTest.Plugins'
$script:PackageName = "$($Prefix)_AutoDeployTest.Package"
$script:WebResourcePrefix = "$($Prefix)_/autodeploytest"

# Marks a publisher that Setup-Dataverse.ps1 created, so Cleanup-Dataverse.ps1 only removes that one
$script:CreatedByTestBench = 'Created by the AutoDeployer test bench'

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
