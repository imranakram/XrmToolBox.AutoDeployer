# Removes everything Setup-Dataverse.ps1 created: test web resources, plugin package,
# plugin assembly (with its plugin types) and the AutoDeployTest solution.

. "$PSScriptRoot\Dataverse.ps1"

foreach ($wr in (Invoke-Dv -Path "webresourceset?`$select=webresourceid,name&`$filter=startswith(name,'biz_/autodeploytest/')").value) {
    Invoke-Dv -Method Delete -Path "webresourceset($($wr.webresourceid))" | Out-Null
    Write-Host "Deleted web resource $($wr.name)"
}

$pkg = Get-DvFirst "pluginpackages?`$select=pluginpackageid&`$filter=name eq 'biz_AutoDeployTest.Package'"
if ($pkg) {
    Invoke-Dv -Method Delete -Path "pluginpackages($($pkg.pluginpackageid))" | Out-Null
    Write-Host 'Deleted plugin package'
}

$asm = Get-DvFirst "pluginassemblies?`$select=pluginassemblyid&`$filter=name eq 'AutoDeployTest.Plugins'"
if ($asm) {
    foreach ($t in (Invoke-Dv -Path "plugintypes?`$select=plugintypeid&`$filter=_pluginassemblyid_value eq $($asm.pluginassemblyid)").value) {
        Invoke-Dv -Method Delete -Path "plugintypes($($t.plugintypeid))" | Out-Null
    }
    Invoke-Dv -Method Delete -Path "pluginassemblies($($asm.pluginassemblyid))" | Out-Null
    Write-Host 'Deleted plugin assembly'
}

$sol = Get-DvFirst "solutions?`$select=solutionid&`$filter=uniquename eq 'AutoDeployTest'"
if ($sol) {
    Invoke-Dv -Method Delete -Path "solutions($($sol.solutionid))" | Out-Null
    Write-Host 'Deleted solution AutoDeployTest'
}
