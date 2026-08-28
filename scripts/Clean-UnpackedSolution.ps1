[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SolutionRoot = (Join-Path $PSScriptRoot '..\Solutions'),

    [Parameter(Mandatory = $false)]
    [string]$PluginTypePrefix = 'RepairShop.Plugins.'
)

$ErrorActionPreference = 'Stop'
$solutionRootPath = (Resolve-Path -LiteralPath $SolutionRoot).Path
$stepsPath = Join-Path $solutionRootPath 'SdkMessageProcessingSteps'
$solutionXmlPath = Join-Path $solutionRootPath 'Other\Solution.xml'

if (-not (Test-Path -LiteralPath $stepsPath -PathType Container)) {
    throw "SDK message processing step folder not found: $stepsPath"
}
if (-not (Test-Path -LiteralPath $solutionXmlPath -PathType Leaf)) {
    throw "Solution manifest not found: $solutionXmlPath"
}

$keptStepIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$keptImageIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$removedFiles = 0

foreach ($stepFile in Get-ChildItem -LiteralPath $stepsPath -Filter '*.xml' -File) {
    [xml]$stepDocument = Get-Content -LiteralPath $stepFile.FullName -Raw
    $step = $stepDocument.SdkMessageProcessingStep
    if ([string]$step.PluginTypeName -like "$PluginTypePrefix*") {
        [void]$keptStepIds.Add(([string]$step.SdkMessageProcessingStepId).Trim('{}'))
        foreach ($image in @($step.SdkMessageProcessingStepImages.SdkMessageProcessingStepImage)) {
            if ($null -ne $image -and -not [string]::IsNullOrWhiteSpace([string]$image.SdkMessageProcessingStepImageId)) {
                [void]$keptImageIds.Add(([string]$image.SdkMessageProcessingStepImageId).Trim('{}'))
            }
        }
        continue
    }

    Remove-Item -LiteralPath $stepFile.FullName -Force
    $removedFiles++
}

[xml]$solutionDocument = New-Object System.Xml.XmlDocument
$solutionDocument.PreserveWhitespace = $true
$solutionDocument.Load($solutionXmlPath)

$removedRootComponents = 0
foreach ($component in @($solutionDocument.SelectNodes('/ImportExportXml/SolutionManifest/RootComponents/RootComponent[@type="92"]'))) {
    $componentId = ([string]$component.GetAttribute('id')).Trim('{}')
    if (-not $keptStepIds.Contains($componentId)) {
        [void]$component.ParentNode.RemoveChild($component)
        $removedRootComponents++
    }
}

$removedDependencies = 0
foreach ($dependency in @($solutionDocument.SelectNodes('/ImportExportXml/SolutionManifest/MissingDependencies/MissingDependency'))) {
    $dependent = $dependency.SelectSingleNode('Dependent')
    if ($null -eq $dependent) {
        continue
    }

    $dependentType = [string]$dependent.GetAttribute('type')
    $dependentId = ([string]$dependent.GetAttribute('id')).Trim('{}')
    $removeDependency =
        ($dependentType -eq '92' -and -not $keptStepIds.Contains($dependentId)) -or
        ($dependentType -eq '93' -and -not $keptImageIds.Contains($dependentId))

    if ($removeDependency) {
        [void]$dependency.ParentNode.RemoveChild($dependency)
        $removedDependencies++
    }
}

$solutionDocument.Save($solutionXmlPath)

Write-Host "Kept $($keptStepIds.Count) $PluginTypePrefix SDK steps and $($keptImageIds.Count) images."
Write-Host "Removed $removedFiles unrelated step files, $removedRootComponents root components, and $removedDependencies stale dependency entries."

