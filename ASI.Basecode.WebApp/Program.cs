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

var appBuilder = WebApplication.CreateBuilder(args);
var demoLauncher = Array.IndexOf(args, "--demo-launcher") >= 0;
if (demoLauncher)
{
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
