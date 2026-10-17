#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [string]$OutputPath = ([IO.Path]::Combine([IO.Path]::GetTempPath(), ('StarTournament-legacy-' + [Guid]::NewGuid().ToString('N') + '.json')))
)
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    [Console]::Error.WriteLine('NotRun: Windows installation/CIM observation requires Windows. No report was written.')
    exit 2
}
. (Join-Path $PSScriptRoot 'LegacyInventory.Common.ps1')
try {
    if ($InstallRoot -notmatch '^[A-Za-z]:[\\/]') { throw 'Absolute local Windows installation root required' }
    $root = (Get-LegacyPlainPath $InstallRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not [IO.Directory]::Exists($root) -or $root -eq [IO.Path]::GetPathRoot($root).TrimEnd([IO.Path]::DirectorySeparatorChar)) { throw 'Existing non-drive installation directory required' }
    $observations = @()
    foreach ($relative in @('Update.exe','Star Tournament.exe','maintenance/StarTournamentMaintenance.exe','current/StarTournamentLauncher.exe')) {
        $observations += [pscustomobject]@{ relativePath = $relative; file = (Get-LegacyFile (Join-Path $root $relative) -Pe) }
    }
    $metadata = @()
    foreach ($relative in @('.star-tournament-install.json','maintenance/maintenance.json','current/release.json')) {
        $path = Join-Path $root $relative
        $metadata += [pscustomobject]@{ relativePath = $relative; file = (Get-LegacyFile $path); fields = (Get-LegacyMetadata $path) }
    }
    try { $processes = Get-LegacyProcesses @(Get-CimInstance Win32_Process -ErrorAction Stop) $root }
    catch { $processes = [pscustomobject]@{ status = 'unavailable'; error = $_.Exception.Message } }
    $report = [ordered]@{
        schema = 1; purpose = 'inventory-only-not-update-qualification'; observedUtc = [DateTime]::UtcNow.ToString('o')
        installRoot = $root; collectorSha256 = (Get-LegacyHash $PSCommandPath)
        helpersSha256 = (Get-LegacyHash (Join-Path $PSScriptRoot 'LegacyInventory.Common.ps1'))
        operatingSystemVersion = [Environment]::OSVersion.Version.ToString(); powerShellVersion = $PSVersionTable.PSVersion.ToString()
        files = @($observations); metadata = @($metadata); processObservation = $processes
        qualification = 'NotRun'; updateExecuted = $false; playerExecuted = $false
    }
    $written = Write-LegacyReport $OutputPath $root $report
    Write-Output ('Inventory written: ' + $written)
    Write-Output 'Update/startup/argv/recovery qualification remains NotRun.'
    exit 0
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
