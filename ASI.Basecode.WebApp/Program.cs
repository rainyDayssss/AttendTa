using System;
using System.Diagnostics;
using ASI.Basecode.Data;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using ASI.Basecode.WebApp;
using ASI.Basecode.WebApp.Extensions.Configuration;
using ASI.Basecode.WebApp.Hotspot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

const string HashPasswordArgument = "--hash-password";

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
