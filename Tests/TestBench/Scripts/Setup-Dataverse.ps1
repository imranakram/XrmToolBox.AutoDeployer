# Registers the AutoDeployer test components in the dev environment, inside an "AutoDeployTest" solution
# (publisher Biznamics, prefix biz). Safe to run again: existing components are left as they are.
# Build the projects first (dotnet build in PluginAssembly and PluginPackage).

. "$PSScriptRoot\Dataverse.ps1"
$root = Split-Path $PSScriptRoot
$solution = 'AutoDeployTest'

# Solution
if (-not (Get-DvFirst "solutions?`$select=solutionid&`$filter=uniquename eq '$solution'")) {
    $publisher = Get-DvFirst "publishers?`$select=publisherid&`$filter=uniquename eq 'Biznamics'"
    Invoke-Dv -Method Post -Path 'solutions' -Body @{
        uniquename = $solution; friendlyname = 'AutoDeployer Test'; version = '1.0.0.0'
        'publisherid@odata.bind' = "/publishers($($publisher.publisherid))"
    } | Out-Null
    Write-Host "Created solution $solution"
}

# Classic plugin assembly + its plugin type
$dllPath = Join-Path $root 'PluginAssembly\bin\Debug\net462\AutoDeployTest.Plugins.dll'
$asmName = [Reflection.AssemblyName]::GetAssemblyName($dllPath)
$asm = Get-DvFirst "pluginassemblies?`$select=pluginassemblyid&`$filter=name eq '$($asmName.Name)'"
if (-not $asm) {
    $asm = Invoke-Dv -Method Post -Path 'pluginassemblies' -Solution $solution -Body @{
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
    Invoke-Dv -Method Post -Path 'plugintypes' -Solution $solution -Body @{
        typename = $typeName; name = $typeName; friendlyname = 'AutoDeployTest TestPlugin'
        'pluginassemblyid@odata.bind' = "/pluginassemblies($($asm.pluginassemblyid))"
    } | Out-Null
    Write-Host "Registered plugin type $typeName"
}

# Plugin package
$nupkg = Get-ChildItem (Join-Path $root 'PluginPackage\bin\Debug') -Filter '*.nupkg' | Select-Object -First 1
$pkgName = 'biz_AutoDeployTest.Package'
if (-not (Get-DvFirst "pluginpackages?`$select=pluginpackageid&`$filter=name eq '$pkgName'")) {
    Invoke-Dv -Method Post -Path 'pluginpackages' -Solution $solution -Body @{
        name = $pkgName; uniquename = $pkgName; version = '1.0.0'
        content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($nupkg.FullName))
    } | Out-Null
    Write-Host "Registered plugin package $pkgName"
}

# Web resources (prefix biz_/autodeploytest, paths relative to WebResources\)
$types = @{ '.html' = 1; '.css' = 2; '.js' = 3 }
$wrRoot = Join-Path $root 'WebResources'
$created = @()
foreach ($file in Get-ChildItem $wrRoot -Recurse -File) {
    $rel = $file.FullName.Substring($wrRoot.Length + 1).Replace('\', '/')
    $name = "biz_/autodeploytest/$rel"
    if (Get-DvFirst "webresourceset?`$select=webresourceid&`$filter=name eq '$name'") { continue }
    $wr = Invoke-Dv -Method Post -Path 'webresourceset' -Solution $solution -Body @{
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

Write-Host 'Done.'
