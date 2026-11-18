using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoTargetTests
{
    [Fact]
    public void DedicatedLocalDbMasterWithIntegratedAuthenticationIsAccepted()
    {
        DemoTarget.ValidateMasterConnection(@"Server=(localdb)\AttendTaDemo;Database=master;Integrated Security=true");
        DemoTarget.ValidateMasterConnection(@"Server=(localdb)\AttendTaDemo_1234567890abcdef1234567890abcdef;Database=master;Integrated Security=true");
    }

    [Theory]
    [InlineData(@"Server=localhost;Database=master;Integrated Security=true")]
    [InlineData(@"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true")]
    [InlineData(@"Server=(localdb)\AttendTaDemo_shared;Database=master;Integrated Security=true")]
    [InlineData(@"Server=(localdb)\AttendTaDemo;Database=AttendTa_Demo;Integrated Security=true")]
    [InlineData(@"Server=(localdb)\AttendTaDemo;Database=master;User ID=sa;Password=secret")]
    [InlineData(@"Server=(localdb)\AttendTaDemo;Database=master;Integrated Security=true;AttachDbFilename=C:\demo.mdf")]
    [InlineData("not a connection string SECRET")]
    public void UnsafeOrAmbiguousTargetIsRejectedWithoutLeakingConnectionString(string connectionString)
    {
        var error = Assert.Throws<ArgumentException>(() => DemoTarget.ValidateMasterConnection(connectionString));
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
