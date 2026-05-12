
# Automates TS release phase 3: update the NINA plugin manifests fork and push a branch.
#   1. Sync the fork with upstream
#   2. Update the local clone to match the fork's main branch
#   3. Create a new feature branch
#   4. Interactively rename/delete existing manifests
#   5. Copy in the new manifest
#   6. Commit the changes
#   7. Push the branch to the fork (user creates PR manually)

[CmdletBinding()]
param(
    [Parameter(Mandatory, HelpMessage="Version string for the release (e.g. '5.10.0.0')")]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$forkRepo      = "tcpalmer/nina.plugin.manifests"
$manifestsRepo = "C:\Users\Tom\source\repos\nina.plugin.manifests"
$manifestDir   = "$manifestsRepo\manifests\t\Target Scheduler\3.0.0"
$sourceManifest = "C:\Users\Tom\source\repos\package\manifest.json"
$destManifest   = "$manifestDir\manifest-$Version.json"
$branch         = "feature/Target_Scheduler-$Version"

# --- Validate prerequisites ---

if (-not (Test-Path $manifestsRepo)) {
    Write-Error "Manifests repo not found: $manifestsRepo"
    exit 1
}

if (-not (Test-Path $sourceManifest)) {
    Write-Error "manifest.json not found: $sourceManifest (run releasePart1.ps1 first)"
    exit 1
}

if (-not (Test-Path $manifestDir)) {
    Write-Error "Manifest directory not found: $manifestDir"
    exit 1
}

# --- Step 1: Sync the fork with upstream ---

Write-Host "Syncing fork with upstream ..."
gh repo sync $forkRepo
Write-Host ""

# --- Step 2: Switch to main and pull ---

Write-Host "Updating local clone ..."
git -C $manifestsRepo checkout main
git -C $manifestsRepo pull
Write-Host ""

# --- Step 3: Create and switch to the feature branch ---

Write-Host "Creating branch $branch ..."
git -C $manifestsRepo checkout -b $branch
Write-Host ""

# --- Step 4: Interactively rename/delete existing manifests ---

$existingManifests = Get-ChildItem -Path $manifestDir -Filter "*.json" | Sort-Object Name

if ($existingManifests.Count -eq 0) {
    Write-Host "No existing manifests found in $manifestDir"
} else {
    Write-Host "Existing manifests in $manifestDir :"
    Write-Host ""

    foreach ($file in $existingManifests) {
        Write-Host "  $($file.Name)"
        $response = Read-Host "  Action - enter new name, 'd' to delete, or press Enter to keep"
        $response = $response.Trim()

        if ($response -eq 'd') {
            Write-Host "  Deleting $($file.Name)"
            Remove-Item $file.FullName
        } elseif ($response -ne '') {
            if (-not $response.EndsWith('.json')) {
                $response = "$response.json"
            }
            $newPath = Join-Path $manifestDir $response
            Write-Host "  Renaming to $response"
            Rename-Item -Path $file.FullName -NewName $response
        } else {
            Write-Host "  Keeping $($file.Name)"
        }
        Write-Host ""
    }
}

# --- Step 5: Copy the new manifest into the directory ---

Write-Host "Copying manifest.json -> manifest-$Version.json ..."
Copy-Item -Path $sourceManifest -Destination $destManifest
Write-Host ""

# --- Step 6: Commit ---

Write-Host "Committing ..."
git -C $manifestsRepo add --all
git -C $manifestsRepo commit -m "Target Scheduler $Version"
Write-Host ""

# --- Step 7: Push to fork ---

Write-Host "Pushing $branch to fork ..."
git -C $manifestsRepo push origin $branch
Write-Host ""

Write-Host "Part 3 complete. Review the branch and create the PR at:"
Write-Host "  https://github.com/$forkRepo/compare/$branch"
