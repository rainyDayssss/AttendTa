using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoUninstallInventoryTests
{
    [Fact]
    public async Task KnownFilesMustMatchManifestAndUnknownLogsAreRefused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "attendta-inventory-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var marker = Path.Combine(root, DemoOwnership.MarkerFile);
        var image = Path.Combine(root, "uploads", "events", "test.png");
        var data = Path.Combine(root, "database", "AttendTa_Demo.mdf");
        var log = Path.Combine(root, "database", "AttendTa_Demo_log.ldf");
        var unknown = Path.Combine(root, "logs", "unknown.txt");
        var knownLog = Path.Combine(root, "logs", "202611", "Warning_20261119_001.txt");
        var ct = TestContext.Current.CancellationToken;
        try
        {
            foreach (var dir in new[] { "database", "keys", "logs", "temporary", "uploads/venues", "uploads/events", "certificates" })
                Directory.CreateDirectory(Path.Combine(root, dir));
            await File.WriteAllTextAsync(marker, "marker", ct);
            await File.WriteAllTextAsync(image, "fixture", ct);
            await File.WriteAllTextAsync(data, "data", ct);
            await File.WriteAllTextAsync(log, "log", ct);
            Directory.CreateDirectory(Path.GetDirectoryName(knownLog)!);
            await File.WriteAllTextAsync(knownLog, "warning fixture", ct);
            var entries = new List<DemoBackupManifest.Entry>();
            entries.Add(new DemoBackupManifest.Entry("database.bak", 0, new string('0', 64)));
            using (var file = File.OpenRead(marker))
                entries.Add(await DemoBackupManifest.MeasureAsync("store/" + DemoOwnership.MarkerFile, file, ct));
            using (var file = File.OpenRead(image))
                entries.Add(await DemoBackupManifest.MeasureAsync("store/uploads/events/test.png", file, ct));
            using (var file = File.OpenRead(knownLog))
                entries.Add(await DemoBackupManifest.MeasureAsync("store/logs/202611/Warning_20261119_001.txt", file, ct));
            Assert.Contains("logs/202611/Warning_20261119_001.txt",
                DemoProtectedBackup.SelectFiles(root, DemoRemovalPreview.Inventory(root)));
            using (var inventory = await DemoUninstallInventory.AcquireAsync(root, entries, ct))
            {
                Assert.Equal(5, inventory.Files.Count);
                Assert.ThrowsAny<IOException>(() => File.Open(image, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
            }
            foreach (var directory in new[] { root, Path.Combine(root, "uploads", "events"), Path.Combine(root, "logs", "202611") })
            {
                var stream = directory + ":fixture";
                await File.WriteAllTextAsync(stream, "directory stream", ct);
                var error = await Assert.ThrowsAsync<InvalidDataException>(() => DemoUninstallInventory.AcquireAsync(root, entries, ct));
                Assert.Contains("alternate data stream", error.Message);
                Assert.Throws<InvalidDataException>(() => DemoBackupSourceFile.OpenDirectory(directory, delete: true).Dispose());
                Assert.Equal("directory stream", await File.ReadAllTextAsync(stream, ct));
                Assert.Equal("fixture", await File.ReadAllTextAsync(image, ct));
                File.Delete(stream);
            }
            await File.WriteAllTextAsync(unknown, "unknown", ct);
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoUninstallInventory.AcquireAsync(root, entries, ct));
            Assert.True(File.Exists(unknown));
            File.Delete(unknown);
            var temporary = Path.Combine(root, "temporary", "unknown.tmp");
            await File.WriteAllTextAsync(temporary, "preserve", ct);
            Assert.Throws<InvalidDataException>(() => DemoProtectedBackup.SelectFiles(root, DemoRemovalPreview.Inventory(root)));
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoUninstallInventory.AcquireAsync(root, entries, ct));
            File.Delete(temporary);
            await File.WriteAllTextAsync(knownLog, "tampered", ct);
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoUninstallInventory.AcquireAsync(root, entries, ct));
            await File.WriteAllTextAsync(knownLog, "warning fixture", ct);
            await File.WriteAllTextAsync(image, "tampered", ct);
            await Assert.ThrowsAsync<InvalidDataException>(() => DemoUninstallInventory.AcquireAsync(root, entries, ct));
            Assert.True(File.Exists(image));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("logs/202611/log-20261119.txt", true)]
    [InlineData("logs/202611/Debug_20261119.txt", true)]
    [InlineData("logs/202611/Error_20261119_001.txt", true)]
    [InlineData("logs/202611/Services_20261119_1000.txt", true)]
    [InlineData("logs/202611/other.txt", false)]
    [InlineData("logs/202611/log-20261119.txt.bak", false)]
    [InlineData("logs/202611/nested/log-20261119.txt", false)]
    [InlineData("logs/other/log-20261119.txt", false)]
    public void LogNamesAreRestricted(string path, bool supported) =>
        Assert.Equal(supported, DemoUninstallInventory.SupportedStoreFile(path));
}
