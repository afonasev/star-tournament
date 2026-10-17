#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../windows/LegacyInventory.Common.ps1')
$script:checks = 0
function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }; $script:checks++
}
function Reject([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert $rejected $Message
}
$temp = Join-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../.local'))) ('legacy-inventory-test-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
try {
    $root = Join-Path $temp 'install'; [void][IO.Directory]::CreateDirectory($root)
    $file = Join-Path $root 'fixture.exe'
    $bytes = New-Object byte[] 528
    $stream = New-Object IO.MemoryStream(,$bytes)
    $writer = New-Object IO.BinaryWriter($stream)
    $writer.Write([uint16]0x5a4d); $stream.Position = 0x3c; $writer.Write([uint32]0x80)
    $stream.Position = 0x80; $writer.Write([uint32]0x4550); $writer.Write([uint16]0x8664); $writer.Write([uint16]1)
    $stream.Position = 0x94; $writer.Write([uint16]0xf0)
    $stream.Position = 392; $writer.Write([Text.Encoding]::ASCII.GetBytes('.text'))
    $stream.Position = 408; $writer.Write([uint32]16); $writer.Write([uint32]512)
    $stream.Position = 428; $writer.Write([uint32]0x60000020)
    $stream.Position = 512; $writer.Write([Text.Encoding]::ASCII.GetBytes('fixture-section!'))
    $writer.Dispose(); [IO.File]::WriteAllBytes($file, $bytes)
    $pe = Get-LegacyPe $file
    Assert ($pe.machine -eq 0x8664 -and $pe.sections.Count -eq 1) 'PE header'
    Assert ($pe.sections[0].sha256 -eq 'aa85e8b2948d4e360c2bf270ba8fc05d7d0d31900f8e85da6df0cc387b9f21b1') 'PE section exact bytes'
    $observed = Get-LegacyFile $file -Pe
    Assert ($observed.status -eq 'observed' -and $observed.stableMetadataDuringRead) ('Observed fixture: ' + ($observed | ConvertTo-Json -Depth 8))
    $known = Join-Path $temp 'known'; [IO.File]::WriteAllText($known, 'abc')
    Assert ((Get-LegacyHash $known) -eq 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad') 'SHA256'
    $bytes[128] = 0; [IO.File]::WriteAllBytes($file, $bytes)
    Reject { Get-LegacyPe $file } 'Bad PE signature accepted'
    $bytes[128] = 0x50; $bytes[134] = 97; [IO.File]::WriteAllBytes($file, $bytes)
    Reject { Get-LegacyPe $file } 'Bad section count accepted'
    $bytes[134] = 1; $bytes[409] = 255; [IO.File]::WriteAllBytes($file, $bytes)
    Reject { Get-LegacyPe $file } 'Bad section bounds accepted'
    Assert ((Get-LegacyFile (Join-Path $root 'missing')).status -eq 'missing') 'Missing state'
    Assert (Test-LegacyBelow $file $root) 'Child boundary'
    Assert (-not (Test-LegacyBelow ($root + '-other/file') $root)) 'Sibling boundary'
    Reject { Get-LegacyPlainPath 'relative' } 'Relative accepted'
    $meta = Join-Path $root 'metadata.json'
    [IO.File]::WriteAllText($meta, '{"version":"0.1.5","protocol":2,"private":"SECRET"}')
    $m = Get-LegacyMetadata $meta
    Assert ($m.version -eq '0.1.5' -and $m.protocol -eq 2 -and ($m | ConvertTo-Json) -notmatch 'SECRET') 'Metadata allowlist'
    [IO.File]::WriteAllText($meta, '{"version":{"private":"SECRET"}}')
    Assert ((Get-LegacyMetadata $meta).status -eq 'unavailable') 'Nested metadata accepted'
    [IO.File]::WriteAllText($meta, ('x' * 65537))
    Assert ((Get-LegacyMetadata $meta).status -eq 'unavailable') 'Oversize accepted'
    [IO.File]::WriteAllText($meta, '{SECRET')
    Assert ((Get-LegacyMetadata $meta).status -eq 'unavailable') 'Malformed metadata accepted'
    Assert (((Get-LegacyMetadata $meta) | ConvertTo-Json) -notmatch 'SECRET') 'Parser error leaked metadata'
    $rows = @(
        [pscustomobject]@{ExecutablePath=$file;ProcessId=12;CreationDate=[DateTime]::UtcNow;CommandLine='SECRET'},
        [pscustomobject]@{ExecutablePath=($root+'-other/file');ProcessId=13;CreationDate=$null;CommandLine='SECRET'},
        [pscustomobject]@{ExecutablePath=$null;ProcessId=14;CreationDate=$null;CommandLine=$null})
    $proc = Get-LegacyProcesses $rows $root
    Assert ($proc.processes.Count -eq 1 -and $proc.opaqueProcessCount -eq 1) 'Process scope/coverage'
    Assert (($proc | ConvertTo-Json -Depth 8) -notmatch 'SECRET') 'Raw commandline leaked'
    $report = Join-Path $temp 'report.json'
    [void](Write-LegacyReport $report $root $proc)
    Assert ([IO.File]::Exists($report)) 'Report missing'
    Reject { Write-LegacyReport $report $root @{} } 'Overwrote output'
    Reject { Write-LegacyReport (Join-Path $root 'report.json') $root @{} } 'Wrote installation'
    Assert (([IO.File]::ReadAllText($report) | ConvertFrom-Json).opaqueProcessCount -eq 1) 'Roundtrip'
    $link = Join-Path $temp 'link'
    [void](New-Item -ItemType SymbolicLink -Path $link -Target $root)
    Reject { Get-LegacyPlainPath (Join-Path $link 'fixture.exe') } 'Reparse accepted'
    Write-Output ('PASS: ' + $script:checks + ' assertions; helper functions executed; no game/updater executed.')
} finally { [IO.Directory]::Delete($temp, $true) }
