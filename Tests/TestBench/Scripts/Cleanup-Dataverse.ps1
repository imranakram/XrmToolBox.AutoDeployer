# Removes everything Setup-Dataverse.ps1 created: test web resources, plugin package,
# plugin assembly (with its plugin types), the AutoDeployTest solution, the publisher if the
# test bench created it (an existing publisher is left alone), and Projects\Local.

. "$PSScriptRoot\Dataverse.ps1"

foreach ($wr in (Invoke-Dv -Path "webresourceset?`$select=webresourceid,name&`$filter=startswith(name,'$WebResourcePrefix/')").value) {
    Invoke-Dv -Method Delete -Path "webresourceset($($wr.webresourceid))" | Out-Null
    Write-Host "Deleted web resource $($wr.name)"
}

$pkg = Get-DvFirst "pluginpackages?`$select=pluginpackageid&`$filter=name eq '$PackageName'"
if ($pkg) {
    Invoke-Dv -Method Delete -Path "pluginpackages($($pkg.pluginpackageid))" | Out-Null
    Write-Host 'Deleted plugin package'
}

$asm = Get-DvFirst "pluginassemblies?`$select=pluginassemblyid&`$filter=name eq '$AssemblyName'"
if ($asm) {
    foreach ($t in (Invoke-Dv -Path "plugintypes?`$select=plugintypeid&`$filter=_pluginassemblyid_value eq $($asm.pluginassemblyid)").value) {
        Invoke-Dv -Method Delete -Path "plugintypes($($t.plugintypeid))" | Out-Null
    }
    Invoke-Dv -Method Delete -Path "pluginassemblies($($asm.pluginassemblyid))" | Out-Null
    Write-Host 'Deleted plugin assembly'
}

$sol = Get-DvFirst "solutions?`$select=solutionid&`$filter=uniquename eq '$SolutionName'"
if ($sol) {
    Invoke-Dv -Method Delete -Path "solutions($($sol.solutionid))" | Out-Null
    Write-Host "Deleted solution $SolutionName"
}

$publisher = Get-DvFirst "publishers?`$select=publisherid,description&`$filter=uniquename eq '$PublisherName'"
if ($publisher -and "$($publisher.description)".StartsWith($CreatedByTestBench)) {
    Invoke-Dv -Method Delete -Path "publishers($($publisher.publisherid))" | Out-Null
    Write-Host "Deleted publisher $PublisherName"
}

$local = Join-Path (Split-Path $PSScriptRoot) 'Projects\Local'
if (Test-Path $local) {
    Remove-Item $local -Recurse -Force
    Write-Host 'Deleted Projects\Local'
}
