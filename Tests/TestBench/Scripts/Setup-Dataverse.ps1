# Registers the AutoDeployer test components in the test environment, inside an "AutoDeployTest" solution
# under the configured publisher (see Dataverse.ps1), and writes ready-to-open sample projects for this
# clone and environment to Projects\Local. Safe to run again: existing components are left as they are.
# Build the projects first (dotnet build in PluginAssembly and PluginPackage).

. "$PSScriptRoot\Dataverse.ps1"
$root = Split-Path $PSScriptRoot

# Publisher: use the configured one, or create it when it doesn't exist yet
$publisher = Get-DvFirst "publishers?`$select=publisherid,customizationprefix&`$filter=uniquename eq '$PublisherName'"
if ($publisher) {
    if ($publisher.customizationprefix -ne $Prefix) {
        throw "Publisher '$PublisherName' has the prefix '$($publisher.customizationprefix)', not '$Prefix'. Fix the prefix setting."
    }
}
else {
    $taken = Get-DvFirst "publishers?`$select=uniquename&`$filter=customizationprefix eq '$Prefix'"
    if ($taken) { throw "The prefix '$Prefix' is already used by publisher '$($taken.uniquename)'. Use that publisher, or another prefix." }

    # Option value prefixes must be unique too; take the first free one from a fixed start
    $optionPrefix = 37210
    while (Get-DvFirst "publishers?`$select=publisherid&`$filter=customizationoptionvalueprefix eq $optionPrefix") { $optionPrefix++ }

    $publisher = Invoke-Dv -Method Post -Path 'publishers' -Body @{
        uniquename = $PublisherName; friendlyname = $PublisherName
        customizationprefix = $Prefix; customizationoptionvalueprefix = $optionPrefix
        description = "$CreatedByTestBench. Remove with Cleanup-Dataverse.ps1."
    }
    Write-Host "Created publisher $PublisherName (prefix $Prefix)"
}

# Solution
if (-not (Get-DvFirst "solutions?`$select=solutionid&`$filter=uniquename eq '$SolutionName'")) {
    Invoke-Dv -Method Post -Path 'solutions' -Body @{
        uniquename = $SolutionName; friendlyname = 'AutoDeployer Test'; version = '1.0.0.0'
        'publisherid@odata.bind' = "/publishers($($publisher.publisherid))"
    } | Out-Null
    Write-Host "Created solution $SolutionName"
}

# Classic plugin assembly + its plugin type
$dllPath = Join-Path $root 'PluginAssembly\bin\Debug\net462\AutoDeployTest.Plugins.dll'
if (-not (Test-Path $dllPath)) { throw "Build the test plugin assembly first: dotnet build .\PluginAssembly -c Debug" }
$asmName = [Reflection.AssemblyName]::GetAssemblyName($dllPath)
$asm = Get-DvFirst "pluginassemblies?`$select=pluginassemblyid&`$filter=name eq '$($asmName.Name)'"
if (-not $asm) {
    $asm = Invoke-Dv -Method Post -Path 'pluginassemblies' -Solution $SolutionName -Body @{
        name           = $asmName.Name
        version        = $asmName.Version.ToString()
        culture        = 'neutral'
        publickeytoken = (($asmName.GetPublicKeyToken() | ForEach-Object { $_.ToString('x2') }) -join '')
        isolationmode  = 2   # sandbox
        sourcetype     = 0   # database
        content        = [Convert]::ToBase64String([IO.File]::ReadAllBytes($dllPath))
    }
    Write-Host "Registered plugin assembly $($asmName.Name)"
}
$typeName = 'AutoDeployTest.Plugins.TestPlugin'
if (-not (Get-DvFirst "plugintypes?`$select=plugintypeid&`$filter=typename eq '$typeName'")) {
    Invoke-Dv -Method Post -Path 'plugintypes' -Solution $SolutionName -Body @{
        typename = $typeName; name = $typeName; friendlyname = 'AutoDeployTest TestPlugin'
        'pluginassemblyid@odata.bind' = "/pluginassemblies($($asm.pluginassemblyid))"
    } | Out-Null
    Write-Host "Registered plugin type $typeName"
}

# Plugin package
$nupkg = Get-ChildItem (Join-Path $root 'PluginPackage\bin\Debug') -Filter '*.nupkg' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $nupkg) { throw "Build the test plugin package first: dotnet build .\PluginPackage -c Debug" }
$pkg = Get-DvFirst "pluginpackages?`$select=pluginpackageid&`$filter=name eq '$PackageName'"
if (-not $pkg) {
    $pkg = Invoke-Dv -Method Post -Path 'pluginpackages' -Solution $SolutionName -Body @{
        name = $PackageName; uniquename = $PackageName; version = '1.0.0'
        content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($nupkg.FullName))
    }
    Write-Host "Registered plugin package $PackageName"
}

# Web resources ($WebResourcePrefix + path relative to WebResources\)
$types = @{ '.html' = 1; '.css' = 2; '.js' = 3 }
$wrRoot = Join-Path $root 'WebResources'
$created = @()
foreach ($file in Get-ChildItem $wrRoot -Recurse -File) {
    $rel = $file.FullName.Substring($wrRoot.Length + 1).Replace('\', '/')
    $name = "$WebResourcePrefix/$rel"
    if (Get-DvFirst "webresourceset?`$select=webresourceid&`$filter=name eq '$name'") { continue }
    $wr = Invoke-Dv -Method Post -Path 'webresourceset' -Solution $SolutionName -Body @{
        name = $name; displayname = "AutoDeployTest $rel"; webresourcetype = $types[$file.Extension]
        content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($file.FullName))
    }
    $created += $wr.webresourceid
    Write-Host "Created web resource $name"
}
if ($created.Count -gt 0) {
    $xml = '<importexportxml><webresources>' + (($created | ForEach-Object { "<webresource>{$_}</webresource>" }) -join '') + '</webresources></importexportxml>'
    Invoke-Dv -Method Post -Path 'PublishXml' -Body @{ ParameterXml = $xml } | Out-Null
    Write-Host 'Published web resources'
}

# Sample projects for this clone and environment: fill in the placeholders of Projects\*.adproj
$local = Join-Path $root 'Projects\Local'
New-Item -ItemType Directory -Force $local | Out-Null
foreach ($template in Get-ChildItem (Join-Path $root 'Projects') -Filter '*.adproj') {
    $text = [IO.File]::ReadAllText($template.FullName)
    $text = $text.Replace('$(TestBench)', $root).Replace('$(PackageId)', $pkg.pluginpackageid).Replace('$(Prefix)', $Prefix)
    [IO.File]::WriteAllText((Join-Path $local $template.Name), $text)
}
Write-Host "Wrote sample projects to $local"

Write-Host 'Done.'
