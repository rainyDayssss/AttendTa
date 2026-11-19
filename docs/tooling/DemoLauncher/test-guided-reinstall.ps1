param([Parameter(Mandatory = $true)][string] $LauncherDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$launcher = Join-Path $LauncherDirectory 'AttendTa-Demo.dll'
if (-not (Test-Path -LiteralPath $launcher)) { throw 'Build the launcher first.' }
$app = Join-Path $LauncherDirectory 'ASI.Basecode.WebApp.exe'
if (Test-Path -LiteralPath $app) { throw 'Use an isolated launcher build directory.' }
$fixture = Join-Path $env:TEMP ("AttendTa-guided-test-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$oldTemp = $env:TEMP
$oldTmp = $env:TMP
$oldCapture = $env:ATTENDTA_TEST_CAPTURE
$fakeFiles = @()
try {
    $project = Join-Path $fixture 'fake'
    New-Item -ItemType Directory -Path $project | Out-Null
    @'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AssemblyName>ASI.Basecode.WebApp</AssemblyName></PropertyGroup></Project>
'@ | Set-Content -LiteralPath (Join-Path $project 'fake.csproj')
    @'
using System;
using System.IO;
File.WriteAllLines(Environment.GetEnvironmentVariable("ATTENDTA_TEST_CAPTURE")!, new[] {
    string.Join("|", args), Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT")!,
    Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE")!,
    Environment.GetEnvironmentVariable("ATTENDTA_DEMO_CREDENTIAL_FILE")!,
    Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT")!,
    Environment.GetEnvironmentVariable("ATTENDTA_DEMO_UNINSTALL_JOURNAL_ROOT")! });
return 7;
'@ | Set-Content -LiteralPath (Join-Path $project 'Program.cs')
    & dotnet build (Join-Path $project 'fake.csproj') -o (Join-Path $fixture 'fake-out') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Fake app build failed.' }
    foreach ($name in @('ASI.Basecode.WebApp.exe', 'ASI.Basecode.WebApp.dll', 'ASI.Basecode.WebApp.deps.json', 'ASI.Basecode.WebApp.runtimeconfig.json')) {
        $target = Join-Path $LauncherDirectory $name
        if (Test-Path -LiteralPath $target) { throw "Refusing to overwrite $target" }
        Copy-Item -LiteralPath (Join-Path $fixture "fake-out/$name") -Destination $target
        $fakeFiles += $target
    }
    $env:TEMP = $fixture
    $env:TMP = $fixture
    $env:ATTENDTA_TEST_CAPTURE = Join-Path $fixture 'capture.txt'
    $root = Join-Path $fixture 'source'
    $credential = Join-Path $fixture 'credential.json'
    $backup = Join-Path $fixture 'backup'
    $journal = Join-Path $fixture 'journal'
    New-Item -ItemType Directory -Path $root | Out-Null
    Set-Content -LiteralPath $credential -Value 'SENSITIVE-FIXTURE-VALUE'
    $output = "2", "n" | & dotnet $launcher 2>&1 | Out-String
    if ($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $env:ATTENDTA_TEST_CAPTURE)) { throw "Decline failed: $output" }
    $output = "3", "", "" | & dotnet $launcher 2>&1 | Out-String
    if ($LASTEXITCODE -ne 1 -or $output -notmatch 'First-time setup cancelled' -or
        $output -match 'open and follow:' -or (Test-Path -LiteralPath $env:ATTENDTA_TEST_CAPTURE)) {
        throw "First-time acknowledgement refusal failed: $output"
    }
    $output = "3", "INVESTIGATED", "" | & dotnet $launcher 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $output -notmatch 'FIRST-TIME-SETUP.md' -or
        $output -notmatch 'Press Enter to close' -or
        (Test-Path -LiteralPath $env:ATTENDTA_TEST_CAPTURE)) { throw "First-time fallback failed: $output" }
    $output = "2", "y", "relative", "AttendTaDemo", $credential, $backup, $journal | & dotnet $launcher 2>&1 | Out-String
    if ($LASTEXITCODE -ne 1 -or $output -notmatch 'Invalid or missing selection' -or
        (Test-Path -LiteralPath $env:ATTENDTA_TEST_CAPTURE)) { throw "Invalid target refusal failed: $output" }
    $output = "2", "y", $root, "AttendTaDemo", $credential, $backup, $journal | & dotnet $launcher 2>&1 | Out-String
    if ($LASTEXITCODE -ne 7 -or $output -notmatch 'did not complete' -or $output -match 'SENSITIVE-FIXTURE-VALUE') {
        throw "Guided delegation failed: $output"
    }
    $capture = @(Get-Content -LiteralPath $env:ATTENDTA_TEST_CAPTURE)
    if ($capture.Count -ne 6 -or $capture[0] -ne '--reinstall-demo' -or
        $capture[1] -ne $root -or $capture[2] -ne 'AttendTaDemo' -or $capture[3] -ne $credential -or
        $capture[4] -ne $backup -or $capture[5] -ne $journal) { throw 'Incorrect delegated arguments or environment.' }
    if ((Get-Content -Raw -LiteralPath $credential) -notmatch 'SENSITIVE-FIXTURE-VALUE') { throw 'Credential fixture changed.' }
    Write-Host 'PASS: decline stops; guided selection delegates paths only and propagates child failure.'
} finally {
    $env:TEMP = $oldTemp
    $env:TMP = $oldTmp
    $env:ATTENDTA_TEST_CAPTURE = $oldCapture
    foreach ($file in $fakeFiles) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
    if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
