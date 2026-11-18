using System.Security.Cryptography;
using Xunit;
using ASI.Basecode.WebApp.Demo;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoAssetTests
{
    [Fact]
    public void BundledIllustrationsAreDistinctSupportedPngsWithLicense()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "demo");
        var names = Enumerable.Range(1, 8).Select(i => $"event-{i}.png")
            .Concat(Enumerable.Range(1, 4).Select(i => $"venue-{i}.png"))
            .Append("venue-cebu.png");
        var hashes = new HashSet<string>();
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        foreach (var name in names)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, name));
            if (OperatingSystem.IsWindows()) DemoImage.ValidatePng(bytes);
            Assert.InRange(bytes.Length, 24, 2 * 1024 * 1024);
            Assert.Equal(signature, bytes[..8]);
            Assert.Equal(1200, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
            Assert.Equal(750, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
            Assert.True(hashes.Add(Convert.ToHexString(SHA256.HashData(bytes))), name);
        }

        var license = File.ReadAllText(Path.Combine(directory, "ASSET-LICENSE.md"));
        Assert.Contains("original", license, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Wikimedia", license, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TruncatedAndCorruptImagesAreRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var image = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "demo", "event-1.png"));
        Assert.Throws<InvalidDataException>(() => DemoImage.ValidatePng(image[..^12]));
        image[image.Length / 2] ^= 1;
        Assert.Throws<InvalidDataException>(() => DemoImage.ValidatePng(image));
        Assert.Throws<InvalidDataException>(() => DemoImage.ValidatePng([137, 80, 78, 71, 13, 10, 26, 10]));
    }
}
