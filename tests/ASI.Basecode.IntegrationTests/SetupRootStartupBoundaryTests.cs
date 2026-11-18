using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class SetupRootStartupBoundaryTests
{
    private const string Connection = "Server=(localdb)\\MSSQLLocalDB;Database=AttendTa_Guard_Test;Trusted_Connection=True;TrustServerCertificate=True";

    [Theory]
    [InlineData("web")]
    [InlineData("web-child")]
    [InlineData("app")]
    [InlineData("app-child")]
    [InlineData("ancestor")]
    public void HostRejectsOverlappingRootBeforeCreatingKeysOrLogs(string placement)
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-startup-boundary", Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        var web = Path.Combine(app, "wwwroot");
        Directory.CreateDirectory(web);
        var setup = placement switch
        {
            "web" => web,
            "web-child" => Path.Combine(web, "private"),
            "app" => app,
            "app-child" => Path.Combine(app, "private"),
            _ => root
        };
        try
        {
            using var baseFactory = new AttendTaWebApplicationFactory(Connection);
            using var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(app);
                builder.UseWebRoot(web);
                builder.UseSetting("Common:SetupRoot", setup);
                builder.UseSetting("Common:LogFileSize", "5000000");
            });
            var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("Common:SetupRoot must be outside wwwroot and the application deployment directory.", error.ToString(), StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(setup, "keys")));
            Assert.False(Directory.Exists(Path.Combine(setup, "logs")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HostStartsWithAnExternalSiblingRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-startup-boundary", Guid.NewGuid().ToString("N"));
        var app = Path.Combine(root, "app");
        var web = Path.Combine(app, "wwwroot");
        var setup = Path.Combine(root, "private");
        Directory.CreateDirectory(web);
        try
        {
            using var baseFactory = new AttendTaWebApplicationFactory(Connection);
            using var factory = baseFactory.WithWebHostBuilder(builder =>
            {
                builder.UseContentRoot(app);
                builder.UseWebRoot(web);
                builder.UseSetting("Common:SetupRoot", setup);
                builder.UseSetting("Common:LogFileSize", "5000000");
            });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            Assert.True(Directory.Exists(Path.Combine(setup, "keys")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
