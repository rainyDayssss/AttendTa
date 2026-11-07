<# File comparison only. Run against a quiesced tree; never a backup or ACL validator. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Create', 'Verify')][string]$Mode,
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$Manifest
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-NoReparseAncestor([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    while ($null -ne $item) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Reparse points are not allowed.'
        }
        $item = $item.Parent
    }
}

function Read-Tree([string]$Directory, [string]$Prefix) {
    foreach ($item in @(Get-ChildItem -LiteralPath $Directory -Force)) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Reparse points are not allowed.'
        }
        $relative = $Prefix + $item.Name
        if ($item.PSIsContainer) {
            [pscustomobject]@{ Path = $relative; Kind = 'Directory'; Length = 0; Sha256 = '' }
            Read-Tree $item.FullName ($relative + '/')
        } else {
            [pscustomobject]@{
                Path = $relative; Kind = 'File'; Length = $item.Length
                Sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
            }
        }
    }
}

try {
    foreach ($path in @($Root, $Manifest)) {
        if (($path -notmatch '^[A-Za-z]:[\\/]' -and $path -notmatch '^\\\\[^\\]+\\[^\\]+') -or
            $path -match '^\\\\[?.]\\') {
            throw 'Root and Manifest must be absolute filesystem paths, not drive-relative or device paths.'
        }
    }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    if ($rootPath -eq [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($Root)).TrimEnd('\', '/')) {
        throw 'Refusing to scan an entire drive or share root.'
    }
    $manifestPath = [IO.Path]::GetFullPath($Manifest)
    if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) { throw 'Root must exist.' }
    Assert-NoReparseAncestor $rootPath
    $parent = [IO.Path]::GetDirectoryName($manifestPath)
    Assert-NoReparseAncestor $parent
    if ($manifestPath.Equals($rootPath, [StringComparison]::OrdinalIgnoreCase) -or
        $manifestPath.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Manifest must be outside the scanned tree.'
    }
    if ($Mode -eq 'Create' -and (Test-Path -LiteralPath $manifestPath)) { throw 'Manifest already exists; refusing overwrite.' }
    if ($Mode -eq 'Verify') {
        $manifestItem = Get-Item -LiteralPath $manifestPath -Force
        if ($manifestItem.PSIsContainer -or ($manifestItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Manifest must be a regular file.'
        }
    }
    $actual = @(Read-Tree $rootPath '' | Sort-Object Path)
    $keys = @($actual | Where-Object { $_.Kind -eq 'File' -and $_.Path -match '^keys/key-[^/]+\.xml$' -and $_.Length -gt 0 })
    if ($keys.Count -eq 0) {
        Write-Output 'MISSING required nonempty keys/key-*.xml files (presence is not decryptability).'
        if ($Mode -eq 'Create') { throw 'Missing keys.' }
    }
    if ($Mode -eq 'Create') {
        $document = [ordered]@{ Version = 1; Algorithm = 'SHA256'; Entries = @($actual) }
        $json = $document | ConvertTo-Json -Depth 5
        # CreateNew is atomic: an existing destination can never be truncated.
        $stream = [IO.File]::Open($manifestPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
            $stream.Write($bytes, 0, $bytes.Length)
        } finally { $stream.Dispose() }
        Write-Output ('PASS created manifest; files={0}; keys={1}' -f @($actual | Where-Object Kind -eq 'File').Count, $keys.Count)
    } else {
        $document = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($document.Version -ne 1 -or $document.Algorithm -cne 'SHA256') { throw 'Unsupported manifest.' }
        $expected = @{}
        foreach ($entry in @($document.Entries)) {
            $p = $entry.Path
            if ($p -isnot [string] -or [string]::IsNullOrWhiteSpace($p) -or
                $p -match '[\\:\x00-\x1f]' -or $p.StartsWith('/') -or
                @($p.Split('/') | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' -or $_ -match '[. ]$' }).Count -gt 0 -or
                $expected.ContainsKey($p)) { throw 'Unsafe or duplicate manifest path.' }
            if ($entry.Kind -notin @('File', 'Directory') -or $entry.Length -lt 0 -or
                ($entry.Kind -eq 'File' -and $entry.Sha256 -notmatch '^[A-Fa-f0-9]{64}$') -or
                ($entry.Kind -eq 'Directory' -and ($entry.Length -ne 0 -or $entry.Sha256 -ne ''))) { throw 'Invalid manifest entry.' }
            $expected[$p] = $entry
        }
        $seen = @{}
        $differences = 0
        foreach ($entry in $actual) {
            $seen[$entry.Path] = $true
            if (-not $expected.ContainsKey($entry.Path)) {
                Write-Output ('UNEXPECTED {0}' -f $entry.Path); $differences++
            } else {
                $old = $expected[$entry.Path]
                if ($old.Kind -ne $entry.Kind -or $old.Length -ne $entry.Length -or $old.Sha256 -ne $entry.Sha256) {
                    Write-Output ('CHANGED {0}' -f $entry.Path); $differences++
                }
            }
        }
        foreach ($p in @($expected.get_Keys() | Sort-Object)) {
            if (-not $seen.ContainsKey($p)) { Write-Output ('MISSING {0}' -f $p); $differences++ }
        }
        if ($differences -gt 0 -or $keys.Count -eq 0) { throw 'Tree differs from manifest or keys absent.' }
        Write-Output ('PASS verified manifest; entries={0}; keys={1}' -f $actual.Count, $keys.Count)
    }
    exit 0
} catch {
    # Do not print arbitrary exception details: OS errors may disclose protected paths.
    Write-Output 'FAIL storage manifest check (invalid/unsafe input, missing keys, inaccessible files, or comparison failure).'
    exit 1
}
