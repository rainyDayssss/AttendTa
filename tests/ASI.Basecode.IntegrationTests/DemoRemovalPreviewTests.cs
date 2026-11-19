using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoRemovalPreviewTests
{
    [Fact]
    public void PathContainmentUsesComponentBoundaries()
    {
        var root = Path.Combine(Path.GetTempPath(), "preview-root");
        Assert.True(DemoRemovalPreview.ContainsPath(root, root));
        Assert.True(DemoRemovalPreview.ContainsPath(root, Path.Combine(root, "credentials.json")));
        Assert.False(DemoRemovalPreview.ContainsPath(root, root + "-other"));
    }

    [Fact]
    public void InventoryRefusesUnknownTopLevelEntriesWithoutDeletingThem()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-preview-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var unknown = Path.Combine(root, "unknown.txt");
        try
        {
            File.WriteAllText(unknown, "preserve");
            Assert.Throws<InvalidOperationException>(() => DemoRemovalPreview.Inventory(root));
            Assert.Equal("preserve", File.ReadAllText(unknown));
        }
        finally
        {
            File.Delete(unknown);
            Directory.Delete(root);
        }
    }

    [Fact]
    public async Task MissingExplicitTargetsRefuseWithoutCreatingResources()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-preview-test-" + Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAnyAsync<Exception>(() => DemoRemovalPreview.RunAsync(root, AppContext.BaseDirectory,
            "", Path.Combine(root, "credentials.json"), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root));
    }
}
