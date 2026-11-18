using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoOwnershipTests
{
    [Fact]
    public void OnlyCanonicalMarkerIsAccepted()
    {
        const string valid = "abcdef0123456789abcdef0123456789";
        Assert.Equal(valid, DemoOwnership.ValidateMarker(valid));
        Assert.Throws<InvalidDataException>(() => DemoOwnership.ValidateMarker(valid.ToUpperInvariant()));
        Assert.Throws<InvalidDataException>(() => DemoOwnership.ValidateMarker(valid + "\n"));
        Assert.Throws<InvalidDataException>(() => DemoOwnership.ValidateMarker("missing"));
    }

    [Fact]
    public async Task MissingOrUnmarkedRootIsRefusedWithoutCreatingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-owner-test-" + Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAnyAsync<Exception>(() => DemoOwnership.InspectAsync(root, AppContext.BaseDirectory,
            "AttendTaDemo", TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root));
    }
}
