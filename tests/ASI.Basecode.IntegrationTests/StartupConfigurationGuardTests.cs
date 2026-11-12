using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// The app refuses to start when <c>Common:SetupRoot</c> is unset, because Data Protection keys
/// have nowhere safe to live. The release sweep ships that key empty, so this guard is the
/// contract a fresh clone and the acceptance host rely on.
/// </summary>
public sealed class StartupConfigurationGuardTests
{
    private const string UnusedConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=AttendTa_Guard_Test;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public void RefusesToStartWithoutASetupRoot()
    {
        using var factory = new AttendTaWebApplicationFactory(UnusedConnectionString)
            .WithWebHostBuilder(builder => builder.UseSetting("Common:SetupRoot", string.Empty));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(
            "Common:SetupRoot must be configured so Data Protection keys can be stored under its keys folder.",
            Flatten(exception),
            StringComparison.Ordinal);
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}
