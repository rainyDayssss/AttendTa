using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
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
Console.WriteLine("The launcher cannot discover every previous installation. Do not assume a missing hint means a fresh machine.");
if (!File.Exists(appPath))
{
    Console.WriteLine("Application files are missing. Extract the entire package before starting.");
    Console.ReadLine();
    return 1;
}

var legacyPath = Path.Combine(Path.GetTempPath(), "AttendTa", "app");
if (Directory.Exists(legacyPath) || File.Exists(legacyPath))
{
    Console.WriteLine($"Possible v1 installation found at: {legacyPath}");
    Console.WriteLine("This path is only a hint, not proof of ownership or version. Startup is blocked.");
    Console.WriteLine("Do not remove it based on this hint. Verify the exact installation and its data, and obtain an approved recovery and removal plan.");
    Console.WriteLine("No files, databases, or processes were changed. Press Enter to close.");
    Console.ReadLine();
    return 1;
}

Console.WriteLine("1. Start an already provisioned demo");
Console.WriteLine("2. Remove a selected paired-marker demo and install a clean seed");
Console.WriteLine("3. First-time setup instructions");
Console.Write("Choose 1, 2 or 3 (anything else cancels): ");
var choice = Console.ReadLine()?.Trim();
if (choice == "3")
{
    Console.WriteLine("No automatic search can prove there is no earlier installation.");
    Console.WriteLine("Independently investigate previous installations and verify the LocalDB instance is dedicated and empty.");
    Console.Write("Type INVESTIGATED to confirm you have done this, or press Enter to cancel: ");
    if (Console.ReadLine() != "INVESTIGATED")
    {
        Console.WriteLine("First-time setup cancelled. No database changes were made.");
        Console.WriteLine("Press Enter to close.");
        Console.ReadLine();
        return 1;
    }
    Console.WriteLine($"For first-time setup, open and follow: {Path.Combine(AppContext.BaseDirectory, "FIRST-TIME-SETUP.md")}");
    Console.WriteLine("No installation or database changes have been made. Press Enter to close.");
    Console.ReadLine();
    return 0;
}
if (choice == "2")
{
    Console.WriteLine("The old database and private files will be deleted only after guarded backup and confirmation.");
    Console.WriteLine("Old test changes remain in the backup, not the new seed. Credentials stay in their separate file.");
    Console.WriteLine("Stop the app and all writers. Independently identify and remove old app binaries; this tool never deletes them.");
    Console.Write("Continue to identify the exact old paired-marker installation? [y/N]: ");
    if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Cancelled. No uninstall was started.");
        return 1;
    }
    Console.Write("Existing private root (absolute path): ");
    var oldRoot = Console.ReadLine()?.Trim().Trim('"');
    Console.Write("Exact dedicated LocalDB instance name: ");
    var oldInstance = Console.ReadLine()?.Trim();
    Console.Write("Separate private credential JSON file path: ");
    var credentialFile = Console.ReadLine()?.Trim().Trim('"');
    Console.Write("New backup directory (absolute path): ");
    var backupRoot = Console.ReadLine()?.Trim().Trim('"');
    Console.Write("New journal directory (absolute path): ");
    var journalRoot = Console.ReadLine()?.Trim().Trim('"');
    if (string.IsNullOrWhiteSpace(oldRoot) || !Path.IsPathFullyQualified(oldRoot) || !Directory.Exists(oldRoot)
        || string.IsNullOrWhiteSpace(oldInstance) ||
           !Regex.IsMatch(oldInstance, @"\AAttendTaDemo(?:_[0-9a-f]{32})?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        || string.IsNullOrWhiteSpace(credentialFile) || !Path.IsPathFullyQualified(credentialFile) || !File.Exists(credentialFile)
        || string.IsNullOrWhiteSpace(backupRoot) || !Path.IsPathFullyQualified(backupRoot)
        || string.IsNullOrWhiteSpace(journalRoot) || !Path.IsPathFullyQualified(journalRoot))
    {
        Console.WriteLine("Invalid or missing selection. No uninstall was started.");
        return 1;
    }
    var reinstall = new ProcessStartInfo(appPath) { WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false };
    reinstall.ArgumentList.Add("--reinstall-demo");
    reinstall.Environment["ATTENDTA_DEMO_SETUP_ROOT"] = oldRoot;
    reinstall.Environment["ATTENDTA_DEMO_LOCALDB_INSTANCE"] = oldInstance;
    reinstall.Environment["ATTENDTA_DEMO_CREDENTIAL_FILE"] = credentialFile;
    reinstall.Environment["ATTENDTA_DEMO_BACKUP_ROOT"] = backupRoot;
    reinstall.Environment["ATTENDTA_DEMO_UNINSTALL_JOURNAL_ROOT"] = journalRoot;
    try
    {
        using var child = Process.Start(reinstall);
        if (child is null) throw new InvalidOperationException();
        await child.WaitForExitAsync();
        if (child.ExitCode != 0)
        {
            Console.WriteLine("Reinstall did not complete. Follow the guarded command's recovery instructions; do not retry blindly.");
            return child.ExitCode;
        }
        Console.WriteLine("Reinstall completed. Restart this launcher and select option 1 to start and check the demo.");
        return 0;
    }
    catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
    {
        Console.WriteLine("Could not start the guarded reinstall command. No uninstall was started by the launcher.");
        return 1;
    }
}
if (choice != "1")
{
    Console.WriteLine("Cancelled. No database changes were made.");
    return 1;
}

try
{
    if (IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == 5000))
    {
        Console.WriteLine("TCP port 5000 is already in use; it may be an older AttendTa or an unrelated service.");
        Console.WriteLine("Identify the owner and stop or reconfigure it only with approval. No process was stopped.");
        Console.WriteLine("Press Enter to close.");
        Console.ReadLine();
        return 1;
    }
}
catch (Exception exception) when (exception is NetworkInformationException or System.Net.Sockets.SocketException)
{
    Console.WriteLine("Could not inspect TCP listeners; startup will still check whether port 5000 can be bound.");
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
start.Environment["ATTENDTA_DEMO_SETUP_ROOT"] = root;
start.Environment["ATTENDTA_DEMO_LOCALDB_INSTANCE"] = instance;
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
