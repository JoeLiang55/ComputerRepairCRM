[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SolutionRoot = (Join-Path $PSScriptRoot '..\Solutions'),

    [Parameter(Mandatory = $false)]
    [string]$PluginDll,

    [Parameter(Mandatory = $false)]
    [string]$ExpectedSolutionName = 'CaseManagementPoC'
)

$ErrorActionPreference = 'Stop'
$solutionRootPath = (Resolve-Path -LiteralPath $SolutionRoot).Path

foreach ($requiredDirectory in @('Other', 'Entities', 'Workflows', 'PluginAssemblies')) {
    if (-not (Test-Path -LiteralPath (Join-Path $solutionRootPath $requiredDirectory) -PathType Container)) {
        throw "Required unpacked solution directory is missing: $requiredDirectory"
    }
}

$solutionXmlPath = Join-Path $solutionRootPath 'Other\Solution.xml'
[xml]$solutionDocument = Get-Content -LiteralPath $solutionXmlPath -Raw
$manifest = $solutionDocument.ImportExportXml.SolutionManifest
if ([string]$manifest.UniqueName -ne $ExpectedSolutionName) {
    throw "Expected solution identity '$ExpectedSolutionName', found '$($manifest.UniqueName)'."
}
if ([string]$manifest.Managed -ne '0') {
    throw 'The canonical repository source must currently be unmanaged.'
}

$assemblyMetadataFiles = @(Get-ChildItem -LiteralPath (Join-Path $solutionRootPath 'PluginAssemblies') -Recurse -Filter '*.dll.data.xml' -File)
if ($assemblyMetadataFiles.Count -ne 1) {
    throw "Expected 1 plug-in assembly, found $($assemblyMetadataFiles.Count)."
}
[xml]$assemblyMetadata = Get-Content -LiteralPath $assemblyMetadataFiles[0].FullName -Raw
$pluginTypes = @($assemblyMetadata.SelectNodes('/PluginAssembly/PluginTypes/PluginType'))
if ($pluginTypes.Count -ne 5) {
    throw "Expected 5 RepairShop plug-in types, found $($pluginTypes.Count)."
}
if (@($pluginTypes | Where-Object { $_.Name -notlike 'RepairShop.Plugins.*' }).Count -ne 0) {
    throw 'The plug-in assembly metadata contains a non-RepairShop plug-in type.'
}

$stepFiles = @(Get-ChildItem -LiteralPath (Join-Path $solutionRootPath 'SdkMessageProcessingSteps') -Filter '*.xml' -File)
if ($stepFiles.Count -ne 8) {
    throw "Expected 8 RepairShop SDK message processing steps, found $($stepFiles.Count)."
}

$imageCount = 0
foreach ($stepFile in $stepFiles) {
    [xml]$stepDocument = Get-Content -LiteralPath $stepFile.FullName -Raw
    if ([string]$stepDocument.SdkMessageProcessingStep.PluginTypeName -notlike 'RepairShop.Plugins.*') {
        throw "Unrelated SDK processing step remains: $($stepDocument.SdkMessageProcessingStep.Name)"
    }
    $imageCount += @($stepDocument.SelectNodes('/SdkMessageProcessingStep/SdkMessageProcessingStepImages/SdkMessageProcessingStepImage')).Count
}
if ($imageCount -ne 6) {
    throw "Expected 6 plug-in images, found $imageCount."
}

$relativeAssemblyPath = ([string]$assemblyMetadata.PluginAssembly.FileName).TrimStart('/').Replace('/', [IO.Path]::DirectorySeparatorChar)
$solutionDllPath = Join-Path $solutionRootPath $relativeAssemblyPath
if (-not (Test-Path -LiteralPath $solutionDllPath -PathType Leaf)) {
    throw "Packaged plug-in DLL is missing: $relativeAssemblyPath"
}

$solutionHash = (Get-FileHash -LiteralPath $solutionDllPath -Algorithm SHA256).Hash
if (-not [string]::IsNullOrWhiteSpace($PluginDll)) {
    $buildDllPath = (Resolve-Path -LiteralPath $PluginDll).Path
    $buildHash = (Get-FileHash -LiteralPath $buildDllPath -Algorithm SHA256).Hash
    if ($solutionHash -ne $buildHash) {
        throw "Plug-in assembly hash mismatch. Build=$buildHash Solution=$solutionHash"
    }
}

Write-Host "Validated ${ExpectedSolutionName}: 1 assembly, 5 types, 8 steps, 6 images."
Write-Host "Plug-in SHA256: $solutionHash"
