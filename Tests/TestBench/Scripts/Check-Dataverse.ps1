# Shows when the test components were last modified in Dataverse, to confirm a deploy landed.

. "$PSScriptRoot\Dataverse.ps1"

$rows = @()
foreach ($a in (Invoke-Dv -Path "pluginassemblies?`$select=name,modifiedon&`$filter=name eq 'AutoDeployTest.Plugins'").value) {
    $rows += [pscustomobject]@{ Type = 'Assembly'; Name = $a.name; ModifiedOn = ([datetime]$a.modifiedon).ToLocalTime() }
}
foreach ($p in (Invoke-Dv -Path "pluginpackages?`$select=name,modifiedon&`$filter=name eq 'biz_AutoDeployTest.Package'").value) {
    $rows += [pscustomobject]@{ Type = 'Package'; Name = $p.name; ModifiedOn = ([datetime]$p.modifiedon).ToLocalTime() }
}
foreach ($w in (Invoke-Dv -Path "webresourceset?`$select=name,modifiedon&`$filter=startswith(name,'biz_/autodeploytest/')").value) {
    $rows += [pscustomobject]@{ Type = 'WebResource'; Name = $w.name; ModifiedOn = ([datetime]$w.modifiedon).ToLocalTime() }
}
$rows | Format-Table -AutoSize
