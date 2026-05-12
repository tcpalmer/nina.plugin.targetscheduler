
# Automates TS release step 2: create the GitHub release.
#   - Tags the repo with vVERSION and creates the release
#   - Extracts the top bullet-point block from CHANGELOG.md as the release description
#   - Attaches the plugin zip produced by releasePart1.ps1
#   - GitHub auto-generates source code zip and tar.gz for every tagged release

[CmdletBinding()]
param(
    [Parameter(Mandatory, HelpMessage="Version string for the release (e.g. '5.10.0.0')")]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$pluginName    = "NINA.Plugin.TargetScheduler"
$versionedName = "$pluginName-$Version"
$tag           = "v$Version"
$repo          = "tcpalmer/nina.plugin.targetscheduler"
$packageDir    = "C:\Users\Tom\source\repos\package\$pluginName"
$zipPath       = Join-Path $packageDir "$versionedName.zip"
$changelogPath = Join-Path $PSScriptRoot "..\CHANGELOG.md"

# --- Validate prerequisites ---

if (-not (Test-Path $zipPath)) {
    Write-Error "Release zip not found: $zipPath"
    exit 1
}

if (-not (Test-Path $changelogPath)) {
    Write-Error "CHANGELOG.md not found: $changelogPath"
    exit 1
}

# --- Extract the top bullet-point block from CHANGELOG.md ---

$notes = [System.Collections.Generic.List[string]]::new()
$inBlock = $false

foreach ($line in (Get-Content $changelogPath)) {
    if ($line -match '^## ') {
        if ($inBlock) { break }
        $inBlock = $true
        continue
    }
    if ($inBlock) {
        $notes.Add($line)
    }
}

# Trim leading/trailing blank lines
while ($notes.Count -gt 0 -and $notes[0].Trim() -eq '')              { $notes.RemoveAt(0) }
while ($notes.Count -gt 0 -and $notes[$notes.Count - 1].Trim() -eq '') { $notes.RemoveAt($notes.Count - 1) }

$releaseNotes = $notes -join "`n"

# --- Write notes to a temp file so gh receives them verbatim ---

$tempNotes = [System.IO.Path]::GetTempFileName()
try {
    [System.IO.File]::WriteAllText($tempNotes, $releaseNotes, [System.Text.Encoding]::UTF8)

    Write-Host "Creating GitHub release $tag ..."
    Write-Host "  Repo:    $repo"
    Write-Host "  Zip:     $zipPath"
    Write-Host "  Notes:`n$releaseNotes`n"

    gh release create $tag `
        --repo $repo `
        --title $tag `
        --latest `
        --notes-file $tempNotes `
        $zipPath

    Write-Host "`nPart 2 complete. Release $tag created at: https://github.com/$repo/releases/tag/$tag"
} finally {
    Remove-Item $tempNotes -ErrorAction SilentlyContinue
}
