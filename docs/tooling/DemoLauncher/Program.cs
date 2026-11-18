using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;

var appPath = Path.Combine(AppContext.BaseDirectory, "ASI.Basecode.WebApp.exe");
if (args is ["--check-package"])
{
    var required = new[] { appPath, Path.Combine(AppContext.BaseDirectory, "demo", "attendta-mvp-schema.sql"),
        Path.Combine(AppContext.BaseDirectory, "demo", "venue-1.png"),
        Path.Combine(AppContext.BaseDirectory, "demo", "event-1.png") };
    Console.WriteLine(required.All(File.Exists) ? "Package files present." : "Package files missing.");
    return required.All(File.Exists) ? 0 : 1;
}

Console.WriteLine("AttendTa demo launcher");
Console.WriteLine("First provision a dedicated LocalDB demo and private store; this launcher does not create or reset them.");
if (!File.Exists(appPath))
{
    Console.WriteLine("Application files are missing. Extract the entire package before starting.");
    Console.ReadLine();
    return 1;
}

Console.Write("Private setup root (absolute path): ");
var root = Console.ReadLine()?.Trim().Trim('"');
Console.Write("Dedicated LocalDB instance [AttendTaDemo]: ");
var instance = Console.ReadLine()?.Trim();
if (string.IsNullOrEmpty(instance)) instance = "AttendTaDemo";
if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) || !Directory.Exists(root)
    || !Regex.IsMatch(instance, @"\AAttendTaDemo(?:_[0-9a-f]{32})?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
{
    Console.WriteLine("Invalid root or instance. No database changes were made. Press Enter to close.");
    Console.ReadLine();
    return 1;
}

// The child binds loopback port 5000 itself and reports its address over its private stdout pipe.
var start = new ProcessStartInfo(appPath) { WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false, RedirectStandardOutput = true };
start.ArgumentList.Add("--demo-launcher");
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
start.Environment["Hotspot__Enabled"] = "false";
start.Environment["Common__SetupRoot"] = root;
start.Environment["ConnectionStrings__DefaultConnection"] =
    $@"Server=(localdb)\{instance};Database=AttendTa_Demo;Integrated Security=true;TrustServerCertificate=true";
using var process = Process.Start(start);
if (process is null) return 1;
Console.WriteLine("Starting the application. Close this window only after stopping the application.");
var announcement = new TaskCompletionSource<(string Url, string Nonce)>(TaskCreationOptions.RunContinuationsAsynchronously);
var output = Task.Run(async () =>
{
    string? line;
    while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
    {
        var match = Regex.Match(line, @"\AATTENDTA_LAUNCH_READY (http://127\.0\.0\.1:5000) ([A-F0-9]{64})\z", RegexOptions.CultureInvariant);
        if (match.Success)
            announcement.TrySetResult((match.Groups[1].Value, match.Groups[2].Value));
        else
            Console.WriteLine(line);
    }
    announcement.TrySetCanceled();
});
string? url = null;
try
{
    var (address, nonce) = await announcement.Task.WaitAsync(TimeSpan.FromSeconds(30));
    using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { Timeout = TimeSpan.FromSeconds(2) };
    using var response = await client.GetAsync(address + "/_demo-launcher-ready");
    if (response.IsSuccessStatusCode && await response.Content.ReadAsStringAsync() == nonce && !process.HasExited)
        url = address;
}
catch (Exception exception) when (exception is TimeoutException or TaskCanceledException or HttpRequestException) { }
if (url is null)
{
    Console.WriteLine("Application did not become ready. Port 5000 may already be in use; check it and the dedicated instance and private-store configuration. No existing process was stopped.");
    if (!process.HasExited) process.Kill(entireProcessTree: true);
    Console.WriteLine("Press Enter to close.");
    Console.ReadLine();
    return 1;
}
Console.WriteLine("Ready at http://localhost:5000/Account/Login (IPv4 loopback). Press Enter to stop the application.");
try { Process.Start(new ProcessStartInfo(url + "/Account/Login") { UseShellExecute = true }); }
catch (Exception) { Console.WriteLine("Open the address above in a browser."); }
Console.ReadLine();
if (!process.HasExited) process.Kill(entireProcessTree: true);
await process.WaitForExitAsync();
await output;
return 0;
