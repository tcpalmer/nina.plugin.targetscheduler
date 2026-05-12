
# Automates TS release steps 2-5:
#   1. Do a clean build as usual.
#   2. Rename the DLL and plugin folder in the NINA Plugins directory
#   3. Move the versioned folder to the package repo
#   4. Run CreateNET7Manifest.ps1 to generate the manifest and zip archive
#   5. Move the generated zip into the package subfolder

[CmdletBinding()]
param(
    [Parameter(Mandatory, HelpMessage="Version string for the release (e.g. '5.1.2.3')")]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$pluginName    = "NINA.Plugin.TargetScheduler"
$versionedName = "$pluginName-$Version"
$pluginsDir    = "C:\Users\Tom\AppData\Local\NINA\Plugins\3.0.0"
$packageDir    = "C:\Users\Tom\source\repos\package\$pluginName"
$manifestScript = "C:\Users\Tom\source\repos\package\CreateNET7Manifest.ps1"

# --- Validate prerequisites ---

$pluginFolder = Join-Path $pluginsDir $pluginName
if (-not (Test-Path $pluginFolder)) {
    Write-Error "Plugin folder not found: $pluginFolder"
    exit 1
}

$dllSource = Join-Path $pluginFolder "$pluginName.dll"
if (-not (Test-Path $dllSource)) {
    Write-Error "Plugin DLL not found: $dllSource"
    exit 1
}

if (-not (Test-Path $packageDir)) {
    Write-Error "Package directory not found: $packageDir"
    exit 1
}

if (-not (Test-Path $manifestScript)) {
    Write-Error "Manifest script not found: $manifestScript"
    exit 1
}

# --- Step 2a: Rename the DLL inside the plugin folder ---

$dllDest = Join-Path $pluginFolder "$versionedName.dll"
Write-Host "Renaming DLL ..."
Write-Host "  $dllSource"
Write-Host "  -> $dllDest"
Rename-Item -Path $dllSource -NewName "$versionedName.dll"

# --- Step 2b: Rename the plugin folder ---

$versionedFolder = Join-Path $pluginsDir $versionedName
Write-Host "Renaming plugin folder ..."
Write-Host "  $pluginFolder"
Write-Host "  -> $versionedFolder"
Rename-Item -Path $pluginFolder -NewName $versionedName

# --- Step 3: Move versioned folder to the package directory ---

$packageTarget = Join-Path $packageDir $versionedName
Write-Host "Moving plugin folder to package directory ..."
Write-Host "  $versionedFolder"
Write-Host "  -> $packageTarget"
Move-Item -Path $versionedFolder -Destination $packageTarget

# --- Step 4: Run CreateNET7Manifest.ps1 ---

$dllPath     = Join-Path $packageTarget "$versionedName.dll"
$installerUrl = "https://github.com/tcpalmer/nina.plugin.targetscheduler/releases/download/v$Version/$versionedName.zip"

Write-Host "Running CreateNET7Manifest.ps1 ..."
Write-Host "  -file $dllPath"
Write-Host "  -installerUrl $installerUrl"
# The manifest script outputs the zip with a relative path, so run it from the package root.
Push-Location "C:\Users\Tom\source\repos\package"
try {
    & $manifestScript -file $dllPath -createArchive -includeAll -installerUrl $installerUrl
} finally {
    Pop-Location
}

# --- Step 5: Move the generated zip into the package subdirectory ---

$zipSource = "C:\Users\Tom\source\repos\package\$versionedName.zip"
$zipDest   = Join-Path $packageDir "$versionedName.zip"

if (-not (Test-Path $zipSource)) {
    Write-Error "Expected zip not found after manifest script: $zipSource"
    exit 1
}

Write-Host "Moving zip to package directory ..."
Write-Host "  $zipSource"
Write-Host "  -> $zipDest"
Move-Item -Path $zipSource -Destination $zipDest

Write-Host "`nPart 1 complete. Artifacts in: $packageDir"
