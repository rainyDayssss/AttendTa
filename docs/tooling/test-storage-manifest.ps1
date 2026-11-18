[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tempBase = Join-Path $env:LOCALAPPDATA 'Temp/opencode'
if (-not (Test-Path -LiteralPath $tempBase -PathType Container)) { $tempBase = [IO.Path]::GetTempPath() }
$fixture = Join-Path $tempBase ('storage-manifest-smoke-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $fixture
$root = Join-Path $fixture 'tree'
$manifest = Join-Path $fixture 'manifest.json'
$helper = Join-Path $PSScriptRoot 'storage-manifest.ps1'
$log = Join-Path $fixture 'results.txt'
$junction = Join-Path $root 'linked'
$script:cases = 0
function Check([string]$Name, [int]$Expected, [string]$CheckRoot = $root, [string]$CheckManifest = $manifest, [string]$Mode = 'Verify', [string]$Marker = '') {
    $output = @(& powershell -NoProfile -File $helper -Mode $Mode -Root $CheckRoot -Manifest $CheckManifest 2>&1)
    $code = $LASTEXITCODE
    Add-Content -LiteralPath $log -Value (@("CASE $Name exit=$code") + $output)
    if (($Expected -eq 0 -and $code -ne 0) -or ($Expected -ne 0 -and $code -eq 0) -or
        ($Marker -ne '' -and ($output -join "`n") -notmatch $Marker)) { throw "Unexpected result: $Name" }
    $script:cases++
    Write-Output "PASS $Name"
}
try {
    $null = New-Item -ItemType Directory -Path (Join-Path $root 'keys'), (Join-Path $root 'empty')
    Set-Content -LiteralPath (Join-Path $root 'keys/key-fixture.xml') -Value '<fixture />'
    Set-Content -LiteralPath (Join-Path $root 'private.txt') -Value 'synthetic only'
    Check 'create' 0 -Mode Create
    Check 'matching tree' 0
    Check 'refuse existing manifest' 1 -Mode Create
    Set-Content -LiteralPath (Join-Path $root 'private.txt') -Value 'changed'
    Check 'altered file' 1 -Marker 'CHANGED private.txt'
    Remove-Item -LiteralPath (Join-Path $root 'private.txt')
    Check 'missing file' 1 -Marker 'MISSING private.txt'
    Set-Content -LiteralPath (Join-Path $root 'private.txt') -Value 'synthetic only'
    Set-Content -LiteralPath (Join-Path $root 'extra.txt') -Value 'extra'
    Check 'unexpected file' 1 -Marker 'UNEXPECTED extra.txt'
    Remove-Item -LiteralPath (Join-Path $root 'extra.txt')
    Remove-Item -LiteralPath (Join-Path $root 'empty')
    Check 'missing empty directory' 1 -Marker 'MISSING empty'
    $null = New-Item -ItemType Directory -Path (Join-Path $root 'empty')
    Remove-Item -LiteralPath (Join-Path $root 'keys/key-fixture.xml')
    Check 'absent keys verify' 1 -Marker 'MISSING keys/key-fixture.xml'
    Check 'absent keys create' 1 -Mode Create -CheckManifest (Join-Path $fixture 'no-keys.json')
    Set-Content -LiteralPath (Join-Path $root 'keys/key-fixture.xml') -Value '<fixture />'
    Check 'manifest inside tree' 1 -Mode Create -CheckManifest (Join-Path $root 'unsafe.json')
    Check 'relative input' 1 -CheckRoot 'relative'
    Check 'drive-relative input' 1 -CheckRoot 'C:relative'
    Check 'drive root input' 1 -CheckRoot ([IO.Path]::GetPathRoot($root))
    Check 'missing root' 1 -CheckRoot (Join-Path $fixture 'missing')
    $bad = Join-Path $fixture 'bad.json'
    Set-Content -LiteralPath $bad -Value '{"Version":1,"Algorithm":"SHA256","Entries":[{"Path":"../escape","Kind":"Directory","Length":0,"Sha256":""}]}'
    Check 'traversal manifest' 1 -CheckManifest $bad
    $null = New-Item -ItemType Junction -Path $junction -Target (Join-Path $root 'empty')
    Check 'nested reparse point' 1
    Check 'root reparse point' 1 -CheckRoot $junction
    [IO.Directory]::Delete($junction)
    Check 'matching tree after guards' 0
    Write-Output "PASS $script:cases smoke cases (synthetic fixtures only)."
    Remove-Item -LiteralPath $fixture -Recurse -Force
    exit 0
} catch {
    # Never recursively remove a junction, even in failed synthetic fixtures.
    Write-Output "FAIL smoke: $($_.Exception.Message). Evidence retained at $fixture"
    exit 1
}
