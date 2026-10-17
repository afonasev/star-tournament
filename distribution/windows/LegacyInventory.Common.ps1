# Compatible with Windows PowerShell 5.1. Observations never authorize an update.
Set-StrictMode -Version 2.0

function Get-LegacyPlainPath([string]$Path) {
    if (-not [IO.Path]::IsPathRooted($Path)) { throw 'Absolute path required' }
    $full = [IO.Path]::GetFullPath($Path)
    $current = $full
    while ($current) {
        if ([IO.File]::Exists($current) -or [IO.Directory]::Exists($current)) {
            if (([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Reparse path is not an installation observation'
            }
        }
        $parent = [IO.Path]::GetDirectoryName($current)
        if ($parent -eq $current) { break }
        $current = $parent
    }
    return $full
}

function Test-LegacyBelow([string]$Path, [string]$Root) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    return $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Get-LegacyHash([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose(); $stream.Dispose() }
}

function Get-LegacyPe([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    $reader = New-Object IO.BinaryReader($stream)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5a4d) { throw 'Not a PE image' }
        [void]$stream.Seek(0x3c, [IO.SeekOrigin]::Begin); $offset = $reader.ReadUInt32()
        if ($offset -gt 1048576 -or $offset + 24 -gt $stream.Length) { throw 'Invalid PE header offset' }
        [void]$stream.Seek($offset, [IO.SeekOrigin]::Begin)
        if ($reader.ReadUInt32() -ne 0x4550) { throw 'Invalid PE signature' }
        $machine = $reader.ReadUInt16(); $count = $reader.ReadUInt16()
        [void]$stream.Seek($offset + 20, [IO.SeekOrigin]::Begin); $optional = $reader.ReadUInt16()
        $table = $offset + 24 + $optional
        if ($count -lt 1 -or $count -gt 96 -or $table + 40 * $count -gt $stream.Length) { throw 'Invalid PE section table' }
        $sections = @()
        for ($i = 0; $i -lt $count; $i++) {
            [void]$stream.Seek($table + $i * 40, [IO.SeekOrigin]::Begin)
            $name = [Text.Encoding]::ASCII.GetString($reader.ReadBytes(8)).TrimEnd([char]0)
            [void]$stream.Seek($table + $i * 40 + 16, [IO.SeekOrigin]::Begin)
            $size = $reader.ReadUInt32(); $raw = $reader.ReadUInt32()
            [void]$stream.Seek($table + $i * 40 + 36, [IO.SeekOrigin]::Begin); $flags = $reader.ReadUInt32()
            if ($size -gt 67108864 -or [uint64]$raw + [uint64]$size -gt [uint64]$stream.Length) { throw 'Invalid PE section bounds' }
            [void]$stream.Seek($raw, [IO.SeekOrigin]::Begin); $bytes = $reader.ReadBytes([int]$size)
            if ($bytes.Length -ne $size) { throw 'PE changed during observation' }
            $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
            $sections += [pscustomobject]@{ name = $name; size = $size; offset = $raw; characteristics = $flags; sha256 = $hash }
        }
        return [pscustomobject]@{ machine = $machine; sections = @($sections) }
    } finally { $sha.Dispose(); $reader.Dispose(); $stream.Dispose() }
}

function Get-LegacyFile([string]$Path, [switch]$Pe) {
    try {
        $plain = Get-LegacyPlainPath $Path
        if (-not [IO.File]::Exists($plain)) { return [pscustomobject]@{ status = 'missing' } }
        $before = Get-Item -LiteralPath $plain -ErrorAction Stop
        $size = $before.Length; $ticks = $before.LastWriteTimeUtc.Ticks
        $hash = Get-LegacyHash $plain
        $result = [ordered]@{ status = 'observed'; size = $size; sha256 = $hash }
        if ($Pe) {
            try { $result.pe = Get-LegacyPe $plain }
            catch { $result.peError = $_.Exception.Message }
        }
        $after = Get-Item -LiteralPath $plain -ErrorAction Stop
        $result.stableMetadataDuringRead = $after.Length -eq $size -and $after.LastWriteTimeUtc.Ticks -eq $ticks
        return [pscustomobject]$result
    } catch { return [pscustomobject]@{ status = 'unavailable'; error = $_.Exception.Message } }
}

function Get-LegacyMetadata([string]$Path) {
    try {
        $plain = Get-LegacyPlainPath $Path
        if (-not [IO.File]::Exists($plain)) { return [pscustomobject]@{ status = 'missing' } }
        if ((Get-Item -LiteralPath $plain -ErrorAction Stop).Length -gt 65536) { throw 'Metadata exceeds observation bound' }
        $source = [IO.File]::ReadAllText($plain) | ConvertFrom-Json -ErrorAction Stop
        $result = [ordered]@{ status = 'observed' }
        foreach ($name in @('appId','version','platform','architecture','updateChannel','protocol','metadataReader','product','installerVersion')) {
            $property = $source.PSObject.Properties[$name]
            if ($null -ne $property) {
                $value = $property.Value
                if ($null -eq $value) { continue }
                if ($value -is [string]) {
                    if ($value.Length -gt 256) { throw 'Metadata field exceeds observation bound' }
                } elseif ($value -isnot [ValueType]) { throw 'Metadata field must be scalar' }
                $result[$name] = $value
            }
        }
        return [pscustomobject]$result
    } catch { return [pscustomobject]@{ status = 'unavailable'; error = 'Metadata unavailable, malformed, reparse, or outside scalar/size bounds' } }
}

function Get-LegacyProcesses($Rows, [string]$Root) {
    $found = @(); $opaque = 0
    foreach ($row in $Rows) {
        if (-not $row.ExecutablePath) { $opaque++; continue }
        try { $below = Test-LegacyBelow $row.ExecutablePath $Root }
        catch { $opaque++; continue }
        if ($below) {
            $started = $null
            if ($row.CreationDate -is [DateTime]) { $started = $row.CreationDate.ToUniversalTime().ToString('o') }
            $found += [pscustomobject]@{ pid = $row.ProcessId; executable = $row.ExecutablePath; startedUtc = $started; commandLineAvailable = ($null -ne $row.CommandLine) }
        }
    }
    return [pscustomobject]@{ status = 'observed-not-atomic'; opaqueProcessCount = $opaque; processes = @($found) }
}

function Write-LegacyReport([string]$Path, [string]$Root, $Report) {
    $plain = Get-LegacyPlainPath $Path
    if ((Test-LegacyBelow $plain $Root) -or $plain -eq [IO.Path]::GetFullPath($Root)) { throw 'Report must be outside installation' }
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(($Report | ConvertTo-Json -Depth 16))
    $stream = [IO.File]::Open($plain, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush() } finally { $stream.Dispose() }
    return $plain
}
