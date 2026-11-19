using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoUninstallPreflightTests
{
    [Fact]
    public void RequiresExactOfflineAndTargetConfirmation()
    {
        const string instance = "AttendTaDemo_0123456789abcdef0123456789abcdef";
        const string root = @"C:\private\demo";
        Assert.True(DemoUninstallPreflight.Confirmed(instance, root, "OFFLINE", instance, root));
        Assert.False(DemoUninstallPreflight.Confirmed(instance, root, "offline", instance, root));
        Assert.False(DemoUninstallPreflight.Confirmed(instance, root, "OFFLINE", "AttendTaDemo", root));
        Assert.False(DemoUninstallPreflight.Confirmed(instance, root, "OFFLINE", instance, root + " "));
        Assert.False(DemoUninstallPreflight.Confirmed(instance, root, null, instance, root));
    }
}
