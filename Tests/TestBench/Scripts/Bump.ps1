# Changes a test file so AutoDeployer has something to deploy.
#   .\Bump.ps1 Assembly     -> new BuildMarker in TestPlugin.cs, rebuilds the DLL
#   .\Bump.ps1 Package      -> new marker in the package plugin, rebuilds the .nupkg
#   .\Bump.ps1 WebResource  -> new marker in scripts\test.js
param([Parameter(Mandatory)][ValidateSet('Assembly', 'Package', 'WebResource')][string]$What)

$root = Split-Path $PSScriptRoot
$marker = Get-Date -Format 'HH:mm:ss'

switch ($What) {
    'Assembly' {
        $file = Join-Path $root 'PluginAssembly\TestPlugin.cs'
        (Get-Content $file -Raw) -replace 'BuildMarker = ".*?"', "BuildMarker = `"$marker`"" | Set-Content $file -NoNewline
        dotnet build (Join-Path $root 'PluginAssembly') -c Debug --nologo -v q
    }
    'Package' {
        $file = Join-Path $root 'PluginPackage\Plugin1.cs'
        $text = Get-Content $file -Raw
        if ($text -match 'BuildMarker = ".*?"') {
            $text = $text -replace 'BuildMarker = ".*?"', "BuildMarker = `"$marker`""
        }
        else {
            $text = $text -replace '(public class Plugin1 : PluginBase\s*\{)', "`$1`r`n        public const string BuildMarker = `"$marker`";`r`n"
        }
        Set-Content $file $text -NoNewline
        dotnet build (Join-Path $root 'PluginPackage') -c Debug --nologo -v q
    }
    'WebResource' {
        $file = Join-Path $root 'WebResources\scripts\test.js'
        (Get-Content $file -Raw) -replace 'marker = ".*?"', "marker = `"$marker`"" | Set-Content $file -NoNewline
    }
}
Write-Host "Bumped $What to marker $marker"
