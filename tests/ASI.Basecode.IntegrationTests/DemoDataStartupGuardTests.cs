using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// The one automated test for the demo data seam (register M-01): a wrongly enabled switch on a
/// live host must refuse to start rather than fake check-ins and certificates.
/// </summary>
public sealed class DemoDataStartupGuardTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public void StartupRefusesWhenDemoDataIsEnabledAgainstANonDemoDatabase()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var guarded = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DemoData:Enabled", "true");
            builder.UseSetting("DemoData:TokenKey", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        });

        var exception = Assert.ThrowsAny<Exception>(() => guarded.CreateClient());

        Assert.Contains(fixture.DatabaseName, exception.ToString(), StringComparison.Ordinal);
        Assert.Contains("Demo data refuses to start", exception.ToString(), StringComparison.Ordinal);
    }
}
