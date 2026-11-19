using System.Text;
using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoBackupManifestTests
{
    [Fact]
    public async Task MeasuresKnownBytesAndRoundTripsDeterministically()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        var entry = await DemoBackupManifest.MeasureAsync("uploads/events/a.png", stream, TestContext.Current.CancellationToken);
        Assert.Equal(3, entry.Length);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", entry.Sha256);
        var json = DemoBackupManifest.Serialize([entry]);
        Assert.Equal(entry, Assert.Single(DemoBackupManifest.Parse(json)));
        Assert.Equal(json, DemoBackupManifest.Serialize(DemoBackupManifest.Parse(json)));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("uploads/../secret")]
    [InlineData("/absolute")]
    [InlineData("C:/secret")]
    [InlineData("uploads\\secret")]
    [InlineData("uploads//secret")]
    public async Task RefusesUnsafePaths(string path)
    {
        await using var stream = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DemoBackupManifest.MeasureAsync(path, stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RefusesCaseInsensitiveDuplicatesAndInvalidHashes()
    {
        var hash = new string('a', 64);
        Assert.Throws<InvalidDataException>(() => DemoBackupManifest.Serialize([
            new("uploads/a", 1, hash), new("UPLOADS/A", 1, hash)]));
        Assert.Throws<InvalidDataException>(() => DemoBackupManifest.Serialize([new("uploads/a", 1, "invalid")]));
    }
}
