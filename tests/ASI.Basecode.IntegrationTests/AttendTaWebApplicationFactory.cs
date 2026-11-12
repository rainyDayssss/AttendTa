using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Hosts the real WebApp pipeline against an isolated integration database.
/// </summary>
public sealed class AttendTaWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString;
    private readonly string setupRoot;
    private readonly string logLevel;
    private readonly string environmentName;

    public AttendTaWebApplicationFactory(string connectionString, string logLevel = "Warning", string environmentName = "Development")
    {
        // Each host holds inotify instances for file watching, and the integration suite builds many
        // hosts. Disabling configuration reload removes one watcher per host so the CI runner's small
        // inotify budget is not exhausted.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        this.connectionString = connectionString;
        this.logLevel = logLevel;
        this.environmentName = environmentName;
        setupRoot = Path.Combine(Path.GetTempPath(), "attendta-integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(setupRoot);
    }

    public string SetupRoot => setupRoot;
    public Action<IServiceCollection>? ConfigureServicesForTesting { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.UseContentRoot(FindWebAppProjectDirectory());
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Common:SetupRoot", setupRoot);
        builder.UseSetting("Logging:LogLevel:Default", logLevel);
        builder.UseSetting("Logging:LogLevel:Microsoft", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        builder.ConfigureTestServices(services => ConfigureServicesForTesting?.Invoke(services));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            Directory.Delete(setupRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string FindWebAppProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ASI.Basecode.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate ASI.Basecode.sln above the test output directory.");
        }

        var projectDirectory = Path.Combine(directory.FullName, "ASI.Basecode.WebApp");
        if (!Directory.Exists(projectDirectory))
        {
            throw new DirectoryNotFoundException($"Could not locate the WebApp project at '{projectDirectory}'.");
        }

        return projectDirectory;
    }
}
