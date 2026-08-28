[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SolutionRoot = (Join-Path $PSScriptRoot '..\Solutions'),

    [Parameter(Mandatory = $false)]
    [string]$PluginDll = (Join-Path $PSScriptRoot '..\RepairShop.Plugins\RepairShop.Plugins\bin\Release\net462\RepairShop.Plugins.dll')
)

$ErrorActionPreference = 'Stop'
$solutionRootPath = (Resolve-Path -LiteralPath $SolutionRoot).Path
$pluginDllPath = (Resolve-Path -LiteralPath $PluginDll).Path
$metadataFiles = @(Get-ChildItem -LiteralPath (Join-Path $solutionRootPath 'PluginAssemblies') -Recurse -Filter '*.dll.data.xml' -File)

if ($metadataFiles.Count -ne 1) {
    throw "Expected exactly one plug-in assembly metadata file, found $($metadataFiles.Count)."
}

[xml]$assemblyMetadata = Get-Content -LiteralPath $metadataFiles[0].FullName -Raw
$relativeTarget = ([string]$assemblyMetadata.PluginAssembly.FileName).TrimStart('/').Replace('/', [IO.Path]::DirectorySeparatorChar)
$targetDllPath = Join-Path $solutionRootPath $relativeTarget
$targetDirectory = Split-Path -Parent $targetDllPath

New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
Copy-Item -LiteralPath $pluginDllPath -Destination $targetDllPath -Force

$buildHash = (Get-FileHash -LiteralPath $pluginDllPath -Algorithm SHA256).Hash
$solutionHash = (Get-FileHash -LiteralPath $targetDllPath -Algorithm SHA256).Hash
if ($buildHash -ne $solutionHash) {
    throw "Plug-in assembly hash mismatch after copy. Build=$buildHash Solution=$solutionHash"
}

Write-Host "Copied CI-built plug-in assembly to $relativeTarget"
Write-Host "SHA256: $solutionHash"

