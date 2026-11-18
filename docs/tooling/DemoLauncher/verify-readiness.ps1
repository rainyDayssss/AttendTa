$ErrorActionPreference = 'Stop'
$web = (Resolve-Path (Join-Path $PSScriptRoot '../../../ASI.Basecode.WebApp')).Path
$dll = Join-Path $web 'bin/Debug/net10.0/ASI.Basecode.WebApp.dll'
if (-not (Test-Path $dll)) { throw 'Build ASI.Basecode.WebApp in Debug first.' }
$root = Join-Path ([IO.Path]::GetTempPath()) ("attendta-launch-test-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $root | Out-Null
$occupied = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$occupied.Start()
$port = $occupied.LocalEndpoint.Port
$process = [Diagnostics.Process]::new()
try {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $web
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.ArgumentList.Add($dll)
    $start.ArgumentList.Add('--demo-launcher')
    $start.Environment['Common__SetupRoot'] = $root
    $start.Environment['ConnectionStrings__DefaultConnection'] = 'Server=(localdb)\Unused;Database=Unused;Integrated Security=true'
    $start.Environment['Hotspot__Enabled'] = 'false'
    $start.Environment['Kestrel__Endpoints__Injected__Url'] = "http://127.0.0.1:$port"
    $process.StartInfo = $start
    if (-not $process.Start()) { throw 'Application failed to start.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $announcement = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        $line = $process.StandardOutput.ReadLineAsync().WaitAsync([TimeSpan]::FromSeconds(30)).GetAwaiter().GetResult()
        if ($null -eq $line) { throw 'Application exited before announcing readiness.' }
        if ($line -cmatch '^ATTENDTA_LAUNCH_READY (http://127\.0\.0\.1:5000) ([A-F0-9]{64})$') {
            $announcement = @($Matches[1], $Matches[2])
            break
        }
    }
    if ($null -eq $announcement) { throw 'No valid readiness announcement.' }
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.UseProxy = $false
    $handler.AllowAutoRedirect = $false
    $client = [Net.Http.HttpClient]::new($handler)
    try {
        $response = $client.GetAsync($announcement[0] + '/_demo-launcher-ready').GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode -or $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() -cne $announcement[1]) {
            throw 'Readiness response did not authenticate the launched process.'
        }
        if ($process.HasExited) { throw 'Application exited during readiness verification.' }
        $conflict = [Diagnostics.Process]::new()
        try {
            $conflict.StartInfo = $start
            if (-not $conflict.Start()) { throw 'Unable to start conflict test.' }
            if (-not $conflict.WaitForExit(30000)) { throw 'Second application did not fail on occupied port 5000.' }
            if ($conflict.ExitCode -eq 0) { throw 'Second application unexpectedly succeeded on occupied port 5000.' }
            $again = $client.GetAsync($announcement[0] + '/_demo-launcher-ready').GetAwaiter().GetResult()
            if ($again.Content.ReadAsStringAsync().GetAwaiter().GetResult() -cne $announcement[1] -or $process.HasExited) {
                throw 'Original process did not remain available after the conflict.'
            }
        } finally {
            if ($null -ne $conflict -and -not $conflict.HasExited) { $conflict.Kill($true); $conflict.WaitForExit() }
            $conflict.Dispose()
        }
    } finally { $client.Dispose() }
    Write-Host 'PASS: inherited endpoint ignored; port 5000 readiness nonce matches; a second child fails without stopping the first.'
} finally {
    if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $process.Dispose()
    $occupied.Stop()
    Remove-Item -LiteralPath $root -Recurse -Force
}
