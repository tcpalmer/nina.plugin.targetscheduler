
# PowerShell script to remap a user-supplied TS database to a local NINA profile.
# Runs the profile ID update in-place against the provided database file.

[CmdletBinding()]
param(
    [Parameter(Mandatory, HelpMessage="Path to the SQLite database file to update")]
    [ValidateScript({
        $clean = $_.TrimStart('"').TrimStart("'").TrimEnd('"').TrimEnd("'")
        if (Test-Path -Path $clean) { $true } else { throw "Database file not found: $clean" }
    })]
    [string]$DatabasePath,

    [Parameter(Mandatory, HelpMessage="Profile ID to replace (FROM)")]
    [string]$FromProfileId,

    [Parameter(HelpMessage="Profile ID to substitute in (TO); defaults to the local profile")]
    [string]$ToProfileId = 'c0e1645f-4d4c-4cff-b6f8-c66a58be9cd4'
)

$DatabasePath  = $DatabasePath.TrimStart('"').TrimStart("'").TrimEnd('"').TrimEnd("'")
$FromProfileId = $FromProfileId.TrimStart('"').TrimStart("'").TrimEnd('"').TrimEnd("'")
$ToProfileId   = $ToProfileId.TrimStart('"').TrimStart("'").TrimEnd('"').TrimEnd("'")

$sqliteDll = Join-Path $PSScriptRoot "..\NINA.Plugin.TargetScheduler\bin\Debug\net10.0-windows7.0\System.Data.SQLite.dll"
$sqliteDll = [System.IO.Path]::GetFullPath($sqliteDll)

if (-not (Test-Path -Path $sqliteDll)) {
    Write-Error "System.Data.SQLite.dll not found at: $sqliteDll`nBuild the project first."
    exit 1
}

Add-Type -Path $sqliteDll

$tables = @('project', 'exposureplan', 'exposuretemplate', 'profilepreference', 'acquiredimage', 'flathistory')

$conn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$DatabasePath;Version=3;")
$conn.Open()

Write-Output ""
Write-Output "Database : $DatabasePath"
Write-Output "FROM     : $FromProfileId"
Write-Output "TO       : $ToProfileId"
Write-Output ""

$totalRows = 0

foreach ($table in $tables) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "UPDATE $table SET profileId = @to WHERE profileId = @from"
    $cmd.Parameters.AddWithValue("@to",   $ToProfileId)   | Out-Null
    $cmd.Parameters.AddWithValue("@from", $FromProfileId) | Out-Null
    $rows = $cmd.ExecuteNonQuery()
    $totalRows += $rows
    Write-Output ("  {0,-20} {1,4} row(s) updated" -f $table, $rows)
}

$conn.Close()

Write-Output ""
Write-Output "Done. $totalRows total row(s) updated."
Write-Output ""
