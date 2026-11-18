using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoSetupGuardTests
{
    [Fact]
    public void ExistingAndDeploymentPathsAreRejected()
    {
        var deployment = Path.GetFullPath(AppContext.BaseDirectory);
        Assert.Throws<InvalidOperationException>(() => DemoSetup.ValidateRoot(deployment, deployment));
        Assert.Throws<InvalidOperationException>(() => DemoSetup.ValidateRoot(Path.Combine(deployment, "new-root"), deployment));
        Assert.Throws<InvalidOperationException>(() => DemoSetup.ValidateRoot(Path.GetTempPath(), deployment));
        Assert.Throws<InvalidOperationException>(() => DemoSetup.ValidateRoot("relative", deployment));
    }

    [Fact]
    public void NewSiblingOfDeploymentIsAcceptedWithoutCreatingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-test-" + Guid.NewGuid().ToString("N"));
        var result = DemoSetup.ValidateRoot(root, AppContext.BaseDirectory);
        Assert.Equal(Path.GetFullPath(root), result);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task InvalidCredentialsDoNotCreatePrivateStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-test-" + Guid.NewGuid().ToString("N"));
        var error = await Assert.ThrowsAsync<DemoSetupException>(() => DemoSetup.RunAsync(
            new Dictionary<string, string>(), root, AppContext.BaseDirectory, TestContext.Current.CancellationToken));
        Assert.Equal("input validation", error.Stage);
        Assert.False(error.StoreCreated);
        Assert.Null(error.InnerException);
        Assert.False(Directory.Exists(root));
    }
}
