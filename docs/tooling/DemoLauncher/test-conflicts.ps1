param([Parameter(Mandatory = $true)][string] $LauncherDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$launcher = Join-Path $LauncherDirectory 'AttendTa-Demo.dll'
if (-not (Test-Path -LiteralPath $launcher)) { throw 'Build the launcher first and supply its output directory.' }
$tempRoot = Join-Path $env:TEMP ("AttendTa-conflict-test-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $tempRoot 'AttendTa/app') -Force | Out-Null
$marker = Join-Path $tempRoot 'AttendTa/app/keep.txt'
Set-Content -LiteralPath $marker -Value 'preserve'
$app = Join-Path $LauncherDirectory 'ASI.Basecode.WebApp.exe'
if (Test-Path -LiteralPath $app) { throw 'Use an isolated launcher build directory without an existing web app.' }
$oldTemp = $env:TEMP
$oldTmp = $env:TMP
try {
    New-Item -ItemType File -Path $app | Out-Null
    $env:TEMP = $tempRoot
    $env:TMP = $tempRoot
    $output = "y" | & dotnet $launcher 2>&1 | Out-String
    if ($LASTEXITCODE -ne 1 -or $output -notmatch 'Possible v1 installation found' -or
        $output -notmatch 'Startup is blocked' -or $output -match 'Continue with a separate installation' -or
        -not (Test-Path -LiteralPath $marker)) {
        throw "Legacy blocking test failed: $output"
    }
    Remove-Item -LiteralPath $marker
    Remove-Item -LiteralPath (Join-Path $tempRoot 'AttendTa/app')
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 5000)
    $listener.Start()
    try {
        $output = "1", "" | & dotnet $launcher 2>&1 | Out-String
        if ($LASTEXITCODE -ne 1 -or $output -notmatch 'TCP port 5000 is already in use' -or
            (Test-Path -LiteralPath (Join-Path $tempRoot 'AttendTa/app'))) { throw "Port conflict test failed: $output" }
        if (-not $listener.Server.IsBound) { throw 'Existing listener was stopped.' }
    } finally { $listener.Stop() }
    Write-Host 'PASS: legacy hint blocks startup and preserves files; occupied port blocks startup without stopping its listener.'
} finally {
    $env:TEMP = $oldTemp
    $env:TMP = $oldTmp
    if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app }
    if (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
    if (Test-Path -LiteralPath (Join-Path $tempRoot 'AttendTa/app')) { Remove-Item -LiteralPath (Join-Path $tempRoot 'AttendTa/app') }
    Remove-Item -LiteralPath (Join-Path $tempRoot 'AttendTa')
    Remove-Item -LiteralPath $tempRoot
}
