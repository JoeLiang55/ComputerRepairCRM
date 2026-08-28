[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageZip,

    [Parameter(Mandatory = $false)]
    [string]$PluginDll,

    [Parameter(Mandatory = $false)]
    [string]$ControlNamespace = 'RepairShop',

    [Parameter(Mandatory = $false)]
    [string]$ControlConstructor = 'RepairStatusControl'
)

$ErrorActionPreference = 'Stop'
$packagePath = (Resolve-Path -LiteralPath $PackageZip).Path

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $customizationsEntry = $archive.GetEntry('customizations.xml')
    if ($null -eq $customizationsEntry) {
        throw 'The solution package does not contain customizations.xml.'
    }

    $reader = [IO.StreamReader]::new($customizationsEntry.Open())
    try {
        [xml]$customizations = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $expectedControlSuffix = "$ControlNamespace.$ControlConstructor"
    $customControls = @($customizations.SelectNodes('/ImportExportXml/CustomControls/CustomControl'))
    $matchingControls = @($customControls | Where-Object { [string]$_.Name -like "*$expectedControlSuffix" })
    if ($matchingControls.Count -ne 1) {
        throw "Expected exactly one '$expectedControlSuffix' custom control, found $($matchingControls.Count)."
    }

    $manifestPath = ([string]$matchingControls[0].FileName).TrimStart('/')
    $manifestEntry = $archive.GetEntry($manifestPath)
    if ($null -eq $manifestEntry) {
        throw "The custom-control manifest is missing from the package: $manifestPath"
    }

    $reader = [IO.StreamReader]::new($manifestEntry.Open())
    try {
        [xml]$controlManifest = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $control = $controlManifest.manifest.control
    if ([string]$control.namespace -ne $ControlNamespace -or [string]$control.constructor -ne $ControlConstructor) {
        throw "Unexpected packaged control identity: $($control.namespace).$($control.constructor)"
    }

    $controlDirectory = [IO.Path]::GetDirectoryName($manifestPath).Replace('\', '/')
    foreach ($resource in @($control.resources.code) + @($control.resources.css) + @($control.resources.resx)) {
        $resourcePath = "$controlDirectory/$($resource.path)"
        if ($null -eq $archive.GetEntry($resourcePath)) {
            throw "The custom-control resource is missing from the package: $resourcePath"
        }
    }

    $pluginEntries = @($archive.Entries | Where-Object { $_.FullName -like 'PluginAssemblies/*.dll' })
    if ($pluginEntries.Count -ne 1) {
        throw "Expected exactly one packaged plug-in DLL, found $($pluginEntries.Count)."
    }

    if (-not [string]::IsNullOrWhiteSpace($PluginDll)) {
        $buildDllPath = (Resolve-Path -LiteralPath $PluginDll).Path
        $buildHash = (Get-FileHash -LiteralPath $buildDllPath -Algorithm SHA256).Hash
        $sha256 = [Security.Cryptography.SHA256]::Create()
        $pluginStream = $pluginEntries[0].Open()
        try {
            $packageHash = [Convert]::ToHexString($sha256.ComputeHash($pluginStream))
        }
        finally {
            $pluginStream.Dispose()
            $sha256.Dispose()
        }
        if ($packageHash -ne $buildHash) {
            throw "Packaged plug-in hash mismatch. Build=$buildHash Package=$packageHash"
        }
    }

    Write-Host "Validated packaged custom control: $($matchingControls[0].Name)"
    Write-Host "Manifest: $manifestPath"
    Write-Host "Resources: $(@($control.resources.code).Count) code, $(@($control.resources.css).Count) CSS, $(@($control.resources.resx).Count) RESX"
}
finally {
    $archive.Dispose()
}

