using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using ASI.Basecode.Data;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using ASI.Basecode.WebApp;
using ASI.Basecode.WebApp.Demo;
using ASI.Basecode.WebApp.Extensions.Configuration;
using ASI.Basecode.WebApp.Hotspot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

const string HashPasswordArgument = "--hash-password";

if (Array.IndexOf(args, "--reinstall-demo") >= 0)
{
    var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
    var instance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
    var backup = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT");
    var journal = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_UNINSTALL_JOURNAL_ROOT");
    var started = false;
    try
    {
        if (args.Length != 1 || Console.IsInputRedirected || string.IsNullOrWhiteSpace(root) ||
            !System.IO.Path.IsPathFullyQualified(root) || string.IsNullOrWhiteSpace(instance) ||
            string.IsNullOrWhiteSpace(backup) || !System.IO.Path.IsPathFullyQualified(backup) ||
            string.IsNullOrWhiteSpace(journal) || !System.IO.Path.IsPathFullyQualified(journal))
            throw new InvalidOperationException();
        root = System.IO.Path.GetFullPath(root);
        var credentials = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_CREDENTIAL_FILE");
        DemoCleanReinstall.EnsureKnownLegacyHintsClear();
        var preview = await DemoRemovalPreview.RunAsync(root, AppContext.BaseDirectory, instance, credentials, default);
        Console.WriteLine($"Old database to REMOVE and reseed: (localdb)\\{instance} / {DemoTarget.Database}");
        Console.WriteLine($"Old private root to REMOVE and recreate: {root}");
        Console.WriteLine($"Fresh backup to CREATE: {System.IO.Path.GetFullPath(backup)}");
        Console.WriteLine($"Progress journal to CREATE: {System.IO.Path.GetFullPath(journal)}");
        Console.WriteLine("Old private-store entries to REMOVE:");
        foreach (var path in preview) Console.WriteLine(path);
        Console.WriteLine("DATA LOSS: old test changes will be retained in the backup, not migrated. The same instance and root will be reused only after removal.");
        Console.WriteLine("Stop all demo processes. Independently remove verified old app binaries before continuing; this command never removes app binaries.");
        Console.Write("Type OFFLINE to confirm the maintenance window: ");
        var offline = Console.ReadLine();
        Console.Write("Type the exact source instance name: ");
        var typedInstance = Console.ReadLine();
        Console.Write("Type the exact source root shown above: ");
        var typedRoot = Console.ReadLine();
        Console.Write("Type OLD APP REMOVED to attest the old application was verified and removed: ");
        var oldApp = Console.ReadLine();
        Console.Write("Type DELETE DEMO DATA to confirm this data loss: ");
        var dataLoss = Console.ReadLine();
        if (!DemoUninstallPreflight.Confirmed(instance, root, offline, typedInstance, typedRoot) ||
            oldApp != "OLD APP REMOVED" || dataLoss != "DELETE DEMO DATA") throw new InvalidOperationException();
        started = true;
        await DemoCleanReinstall.RunAsync(true, root, AppContext.BaseDirectory, instance, credentials,
            backup, journal, preview, default);
        Console.WriteLine("Clean seed installed and database counts, presenter credentials and 13 banners verified. Backup and journal were preserved.");
        Console.WriteLine("Application startup and browser sign-in were not tested; verify them before the demo.");
        return 0;
    }
    catch (DemoReinstallException exception)
    {
        Console.Error.WriteLine($"Reinstall failed during {exception.Stage}. The source may be removed or partially re-created; do not retry blindly.");
        Console.Error.WriteLine($"Inspect backup {backup} and journal {journal}; use --recover-demo-backup into a new empty dedicated instance and new root.");
        return 2;
    }
    catch
    {
        Console.Error.WriteLine(started
            ? "Reinstall failed; inspect the selected source, backup and journal before any retry."
            : "Reinstall declined or refused before removal. Verify known legacy paths and processes and inspect targets.");
        return 2;
    }
}

if (Array.IndexOf(args, "--uninstall-demo") >= 0)
{
    var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
    var instance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
    var backup = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT");
    var journal = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_UNINSTALL_JOURNAL_ROOT");
    var started = false;
    try
    {
        if (args.Length != 1 || Console.IsInputRedirected || string.IsNullOrWhiteSpace(root) ||
            !System.IO.Path.IsPathFullyQualified(root) || string.IsNullOrWhiteSpace(instance) ||
            string.IsNullOrWhiteSpace(backup) || !System.IO.Path.IsPathFullyQualified(backup) ||
            string.IsNullOrWhiteSpace(journal) || !System.IO.Path.IsPathFullyQualified(journal))
            throw new InvalidOperationException();
        root = System.IO.Path.GetFullPath(root);
        var credentials = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_CREDENTIAL_FILE");
        var preview = await DemoRemovalPreview.RunAsync(root, AppContext.BaseDirectory, instance, credentials, default);
        Console.WriteLine($"Database to REMOVE: (localdb)\\{instance} / {DemoTarget.Database}");
        Console.WriteLine($"Private root to REMOVE: {root}");
        Console.WriteLine($"Fresh backup to CREATE: {System.IO.Path.GetFullPath(backup)}");
        Console.WriteLine($"Progress journal to CREATE: {System.IO.Path.GetFullPath(journal)}");
        Console.WriteLine("Private-store entries to REMOVE:");
        foreach (var path in preview) Console.WriteLine(path);
        Console.WriteLine("DATA LOSS: the selected database and all listed private files will be deleted. Stop the app and every writer.");
        Console.WriteLine("The separately held credentials, backup, journal and application binaries will not be deleted.");
        Console.Write("Type OFFLINE to confirm the maintenance window: ");
        var offline = Console.ReadLine();
        Console.Write("Type the exact source instance name: ");
        var typedInstance = Console.ReadLine();
        Console.Write("Type the exact source root shown above: ");
        var typedRoot = Console.ReadLine();
        Console.Write("Type DELETE DEMO DATA to confirm this data loss: ");
        var dataLoss = Console.ReadLine();
        if (!DemoUninstallPreflight.Confirmed(instance, root, offline, typedInstance, typedRoot) ||
            dataLoss != "DELETE DEMO DATA") throw new InvalidOperationException();
        started = true;
        await DemoGuardedUninstall.RunAsync(true, root, AppContext.BaseDirectory, instance, credentials,
            backup, journal, preview, default);
        Console.WriteLine("Selected demo database and private root removed. Backup and journal were preserved.");
        Console.WriteLine("Application binaries were not removed; independently verify ownership before any manual cleanup.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine(started
            ? "Uninstall failed or was interrupted. The source may be partially removed; do not retry blindly. Inspect the journal and use --recover-demo-backup into a new empty dedicated instance and new root."
            : "Uninstall declined or refused before removal began. No uninstall resources were created.");
        if (started) Console.Error.WriteLine($"Source instance: {instance}; source root: {root}; backup: {backup}; journal: {journal}");
        return 2;
    }
}

if (Array.IndexOf(args, "--recover-demo-backup") >= 0)
{
    var recoveryRoot = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_RECOVERY_ROOT");
    var recoveryInstance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_RECOVERY_INSTANCE");
    try
    {
        if (args.Length != 1 || Console.IsInputRedirected) throw new InvalidOperationException();
        var backup = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT");
        if (string.IsNullOrWhiteSpace(recoveryRoot) || !System.IO.Path.IsPathFullyQualified(recoveryRoot) ||
            string.IsNullOrWhiteSpace(recoveryInstance) || string.IsNullOrWhiteSpace(backup) ||
            !System.IO.Path.IsPathFullyQualified(backup)) throw new InvalidOperationException();
        recoveryRoot = System.IO.Path.GetFullPath(recoveryRoot);
        Console.WriteLine($"Backup: {System.IO.Path.GetFullPath(backup)}");
        Console.WriteLine($"NEW recovery root: {recoveryRoot}");
        Console.WriteLine($"EMPTY dedicated LocalDB instance: {recoveryInstance}; database: {DemoTarget.Database}");
        Console.WriteLine("Stop all writers and preserve the backup and separately held credentials. Existing roots or user databases are never overwritten.");
        Console.Write("Type OFFLINE to confirm the maintenance window: ");
        var offline = Console.ReadLine();
        Console.Write("Type the exact recovery instance name: ");
        var typedInstance = Console.ReadLine();
        Console.Write("Type the exact NEW recovery root shown above: ");
        var typedRoot = Console.ReadLine();
        if (!DemoUninstallPreflight.Confirmed(recoveryInstance, recoveryRoot, offline, typedInstance, typedRoot))
            throw new InvalidOperationException();
        await DemoBackupRecovery.RunAsync(backup, recoveryRoot, AppContext.BaseDirectory, recoveryInstance, default);
        Console.WriteLine("Recovery completed in the selected new root and instance. The backup was not removed.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine("Recovery declined or failed. No existing root or database was overwritten. Partial new resources may remain; do not retry or remove them without inspection.");
        if (!string.IsNullOrWhiteSpace(recoveryRoot) && !string.IsNullOrWhiteSpace(recoveryInstance))
            Console.Error.WriteLine($"Selected recovery instance: {recoveryInstance}; selected recovery root: {recoveryRoot}");
        return 2;
    }
}

if (Array.IndexOf(args, "--locked-demo-backup-check") >= 0)
{
    try
    {
        if (args.Length != 1 || Console.IsInputRedirected) throw new InvalidOperationException();
        var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
        var instance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
        var credentials = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_CREDENTIAL_FILE");
        var destination = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !System.IO.Path.IsPathFullyQualified(root) ||
            string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(destination) ||
            !System.IO.Path.IsPathFullyQualified(destination)) throw new InvalidOperationException();
        root = System.IO.Path.GetFullPath(root);
        Console.WriteLine($"Source instance: {instance}; database: {DemoTarget.Database}; root: {root}");
        Console.WriteLine($"New backup destination: {System.IO.Path.GetFullPath(destination)}");
        Console.WriteLine("Close the app and all writers. This check creates a sensitive backup and disposable restore resources; it does not uninstall.");
        Console.Write("Type OFFLINE to confirm the maintenance window: ");
        var offline = Console.ReadLine();
        Console.Write("Type the exact instance name: ");
        var typedInstance = Console.ReadLine();
        Console.Write("Type the exact source root shown above: ");
        var typedRoot = Console.ReadLine();
        if (!DemoUninstallPreflight.Confirmed(instance, root, offline, typedInstance, typedRoot))
            throw new InvalidOperationException();
        await DemoLockedBackupVerification.RunAsync(root, AppContext.BaseDirectory, instance, credentials, destination, default);
        Console.WriteLine("Fresh backup and isolated restore verified while holding the source ownership lock. No source deletion was performed.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine("Locked backup check declined or failed. No source deletion was performed. A new backup or disposable verification resources may remain; inspect reported targets.");
        return 2;
    }
}

if (Array.IndexOf(args, "--preflight-demo-uninstall") >= 0)
{
    try
    {
        if (args.Length != 1 || Console.IsInputRedirected) throw new InvalidOperationException();
        var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
        var instance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
        var credentialFile = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_CREDENTIAL_FILE");
        var backup = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(instance) ||
            string.IsNullOrWhiteSpace(backup) || !System.IO.Path.IsPathFullyQualified(root) ||
            !System.IO.Path.IsPathFullyQualified(backup)) throw new InvalidOperationException();
        root = System.IO.Path.GetFullPath(root);
        Console.WriteLine("NON-DESTRUCTIVE preflight only. No uninstall is available.");
        Console.WriteLine($"Selected instance: {instance}; database: {DemoTarget.Database}");
        Console.WriteLine($"Selected private root: {root}");
        Console.WriteLine($"Selected backup: {System.IO.Path.GetFullPath(backup)}");
        Console.WriteLine("Stop the app and all writers. A real uninstall would destroy the database and private store; preserve the separate credential handover and backup.");
        Console.Write("Type OFFLINE to confirm the maintenance window: ");
        var offline = Console.ReadLine();
        Console.Write("Type the exact instance name: ");
        var typedInstance = Console.ReadLine();
        Console.Write("Type the exact private root shown above: ");
        var typedRoot = Console.ReadLine();
        if (!DemoUninstallPreflight.Confirmed(instance, root, offline, typedInstance, typedRoot))
            throw new InvalidOperationException();
        var inventory = await DemoUninstallPreflight.RunAsync(root, AppContext.BaseDirectory, instance, credentialFile, backup, default);
        Console.WriteLine("Current private-store entries (not a deletion plan):");
        foreach (var path in inventory) Console.WriteLine(path);
        Console.WriteLine("Preflight completed. Database freshness and exclusive ownership are not established; no deletion or uninstall was performed.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine("Uninstall preflight declined, refused, or failed. No source deletion or uninstall was performed.");
        return 2;
    }
}

if (Array.IndexOf(args, "--verify-demo-backup-restore") >= 0)
{
    try
    {
        if (args.Length != 1) throw new InvalidOperationException();
        await DemoRestoreVerification.RunAsync(Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT"), default);
        Console.WriteLine("Selected backup restored and checked in a disposable instance; verification resources were cleaned up. Source installation was not changed.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine("Backup restore verification refused or failed. It does not authorize removal. See any disposable-resource inspection message above.");
        return 2;
    }
}

if (Array.IndexOf(args, "--verify-demo-backup-files") >= 0)
{
    try
    {
        if (args.Length != 1) throw new InvalidOperationException();
        var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT");
        var entries = await DemoBackupVerification.VerifyFilesAsync(root, default);
        Console.WriteLine($"Backup file inventory and {entries.Count} checksums verified. No database restore was attempted; this does not authorize removal.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine("Backup file verification refused or failed. No restore or removal was attempted.");
        return 2;
    }
}

if (Array.IndexOf(args, "--backup-demo") >= 0)
{
    try
    {
        if (args.Length != 2 || Array.IndexOf(args, "--confirm-dedicated-localdb") < 0 || Console.IsInputRedirected)
            throw new InvalidOperationException();
        var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
        var instance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
        var credentialFile = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_CREDENTIAL_FILE");
        var destination = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_BACKUP_ROOT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(destination))
            throw new InvalidOperationException();
        Console.WriteLine($"Source instance: {instance}; database: {DemoTarget.Database}");
        Console.WriteLine($"Source root: {root}");
        Console.WriteLine($"New protected backup root: {destination}");
        Console.WriteLine("Close the app and all writers. Ensure credentials are only in the separate handover, not in the source store.");
        Console.WriteLine("The backup contains sensitive database and private-store data; the selected credential file is not copied.");
        Console.Write("Type OFFLINE to confirm an offline maintenance window: ");
        if (Console.ReadLine() != "OFFLINE") throw new InvalidOperationException();
        Console.Write("Type the exact source instance name to continue: ");
        if (Console.ReadLine() != instance) throw new InvalidOperationException();
        await DemoProtectedBackup.RunAsync(root, AppContext.BaseDirectory, instance, credentialFile, destination, default);
        Console.WriteLine("Backup verified and marked COMPLETE. The source was not removed.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine("Backup refused or failed. The source was not removed. A new incomplete destination may remain; inspect it before any separately approved cleanup.");
        return 2;
    }
}

if (Array.IndexOf(args, "--preview-demo-removal") >= 0)
{
    try
    {
        if (args.Length != 2 || Array.IndexOf(args, "--confirm-dedicated-localdb") < 0)
            throw new InvalidOperationException();
        var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
        var instance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
        var credentialFile = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_CREDENTIAL_FILE");
        var inventory = await DemoRemovalPreview.RunAsync(root, AppContext.BaseDirectory, instance, credentialFile, default);
        Console.Out.WriteLine($"Read-only snapshot: LocalDB instance {instance}, database {DemoTarget.Database}.");
        Console.Out.WriteLine($"Private root: {System.IO.Path.GetFullPath(root)}");
        Console.Out.WriteLine("Store entries observed (not a deletion authorization):");
        foreach (var path in inventory) Console.Out.WriteLine(path);
        Console.Out.WriteLine("Separate credential file validated. No backup or removal was performed. Reverification is required; this snapshot can change.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine("Removal preview refused or incomplete. No backup or removal was performed.");
        return 2;
    }
}

if (Array.IndexOf(args, "--inspect-demo-ownership") >= 0)
{
    try
    {
        if (args.Length != 2 || Array.IndexOf(args, "--confirm-dedicated-localdb") < 0)
            throw new InvalidOperationException();
        await DemoOwnership.InspectAsync(Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT"),
            AppContext.BaseDirectory, Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE"), default);
        Console.Out.WriteLine("Ownership evidence matches. This is not authorization to reset; no changes were made.");
        return 0;
    }
    catch
    {
        Console.Error.WriteLine("Ownership verification refused; no changes were made.");
        return 2;
    }
}

// Validation is deliberately separate from provisioning: this command never opens a database.
// Redirect a protected private file to stdin; do not place credentials in arguments or environment.
if (Array.IndexOf(args, "--validate-demo-credentials") >= 0 || Array.IndexOf(args, "--setup-demo") >= 0)
{
    try
    {
        var setup = Array.IndexOf(args, "--setup-demo") >= 0;
        if (setup && Array.IndexOf(args, "--confirm-dedicated-localdb") < 0)
            throw new InvalidOperationException();
        if (!Console.IsInputRedirected)
            throw new InvalidOperationException();
        var input = new char[16385];
        System.Collections.Generic.IReadOnlyDictionary<string, string> credentials;
        try
        {
            var count = 0;
            int read;
            while (count < input.Length && (read = await Console.In.ReadAsync(input.AsMemory(count))) > 0)
                count += read;
            if (count == input.Length)
                throw new InvalidOperationException();
            credentials = DemoCredentials.Parse(new string(input, 0, count));
        }
        finally { Array.Clear(input); }
        if (setup)
        {
            var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
            await DemoSetup.RunAsync(credentials, root, AppContext.BaseDirectory, default);
            Console.Out.WriteLine("Fresh demo setup completed.");
        }
        else
            Console.Out.WriteLine("Demo credential input is valid; no database changes were made.");
        return 0;
    }
    catch (DemoSetupException exception)
    {
        Console.Error.WriteLine($"Demo setup failed during {exception.Stage}. " +
            (exception.StoreCreated
                ? "Partial resources may exist; inspect the dedicated target and private store before retrying. No automatic cleanup was performed."
                : "No setup store or database was created."));
        return 2;
    }
    catch
    {
        Console.Error.WriteLine("Demo input validation failed; no setup store or database was created.");
        return 2;
    }
}

if (Array.IndexOf(args, "--demo-readiness") >= 0)
{
    return await ASI.Basecode.WebApp.Readiness.DemoReadiness.RunAsync();
}

if (Array.IndexOf(args, "--hash-password-env") >= 0)
{
    var secret = Environment.GetEnvironmentVariable("ATTENDTA_HASH_SECRET");
    if (string.IsNullOrEmpty(secret))
    {
        Console.Error.WriteLine("ATTENDTA_HASH_SECRET is required.");
        return 2;
    }
    var timer = Stopwatch.StartNew();
    var hash = new Argon2PasswordHasher().Hash(secret);
    timer.Stop();
    Console.Out.WriteLine(hash);
    Console.Error.WriteLine($"Argon2id hash generated in {timer.ElapsedMilliseconds} ms.");
    return 0;
}

var hashPasswordIndex = Array.IndexOf(args, HashPasswordArgument);
if (hashPasswordIndex >= 0)
{
    if (hashPasswordIndex + 1 >= args.Length || string.IsNullOrEmpty(args[hashPasswordIndex + 1]))
    {
        Console.Error.WriteLine($"Usage: {HashPasswordArgument} <secret>");
        return 2;
    }

    var stopwatch = Stopwatch.StartNew();
    IPasswordHasher passwordHasher = new Argon2PasswordHasher();
    var hash = passwordHasher.Hash(args[hashPasswordIndex + 1]);
    stopwatch.Stop();

    // Only the PHC hash goes to stdout so the setup script can capture it verbatim.
    Console.Out.WriteLine(hash);
    Console.Error.WriteLine($"Argon2id hash generated in {stopwatch.ElapsedMilliseconds} ms.");
    return 0;
}

if (Array.IndexOf(args, HotspotCertificateCommand.Argument) >= 0)
{
    return HotspotCertificateCommand.Run(args, Console.Out, Console.Error);
}

var demoLauncher = Array.IndexOf(args, "--demo-launcher") >= 0;
if (demoLauncher)
{
    try
    {
        if (args.Length != 1) throw new InvalidOperationException();
        var root = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
        var instance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
        if (string.IsNullOrWhiteSpace(instance)) throw new InvalidOperationException();
        await DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, default);
    }
    catch
    {
        Console.Error.WriteLine("Startup refused: the selected root and database are not a verified v1.1.1 clean installation. Investigate the old installation and use the approved removal and clean setup procedure.");
        return 2;
    }
}
var appBuilder = WebApplication.CreateBuilder(args);
if (demoLauncher)
{
    // Bind the running application to precisely the selection just verified.
    var instance = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
    var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(DemoSetup.MasterConnection(instance))
        { InitialCatalog = DemoTarget.Database };
    appBuilder.Configuration["ConnectionStrings:DefaultConnection"] = connection.ConnectionString;
    appBuilder.Configuration["Common:SetupRoot"] = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_SETUP_ROOT");
    // Replace configuration-backed endpoints, including inherited Kestrel__Endpoints entries.
    // Kestrel binds the fixed loopback port itself; an occupied port fails startup.
    appBuilder.WebHost.ConfigureKestrel(options =>
    {
        options.Configure(new ConfigurationBuilder().Build());
        options.Listen(IPAddress.Loopback, 5000);
    });
}

// PLAIN ENGLISH: The next block is optional. When the demo host turns on "Hotspot" mode, the app
// serves HTTPS on the hotspot address so phones can use the camera. When it is off (the normal
// development case), the app starts exactly as before.
// Hotspot mode binds HTTPS directly on the hotspot adapter IP and keeps HTTP on 127.0.0.1 for
// the readiness check. It is off unless the host enables it, so the default startup is unchanged.
HotspotHttpsOptions hotspotOptions;
try
{
    hotspotOptions = HotspotHttpsOptions.FromConfiguration(appBuilder.Configuration);
    if (hotspotOptions.Enabled)
    {
        HotspotHttps.Apply(appBuilder.WebHost, hotspotOptions);
    }
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

appBuilder.Logging
    .AddConfiguration(appBuilder.Configuration.GetLoggingSection())
    .AddConsole()
    .AddDebug();

var configurer = new StartupConfigurer(appBuilder.Configuration);
configurer.ConfigureServices(appBuilder.Services);

var app = appBuilder.Build();

configurer.ConfigureApp(app, app.Environment);

if (demoLauncher)
{
    var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    app.MapGet("/_demo-launcher-ready", () => nonce).AllowAnonymous();
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is not null && addresses.Count == 1)
            Console.WriteLine($"ATTENDTA_LAUNCH_READY {addresses.Single()} {nonce}");
    });
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}");
app.MapControllers();
app.MapRazorPages();

// Run application
app.Run();
return 0;

/// <summary>
/// Entry point marker so integration tests can host the application with <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program
{
}
