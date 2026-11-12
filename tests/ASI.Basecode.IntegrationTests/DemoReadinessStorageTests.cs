using ASI.Basecode.WebApp.Readiness;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public class DemoReadinessStorageTests
{
    private static readonly string[] RequiredAreas =
        ["keys", "logs", "temporary", "uploads/venues", "uploads/events", "certificates"];

    [Fact]
    public void ExistingWritableAreasPassAndLeaveNoProbeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-readiness-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var area in RequiredAreas)
                Directory.CreateDirectory(Path.Combine(root, area));
            DemoReadiness.ValidateStorage(root);
            Assert.Empty(Directory.GetFiles(root, ".readiness-*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingMonthDirectoryIsCreatedAndProbedWithoutLeavingProbes()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-readiness-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var area in RequiredAreas)
                Directory.CreateDirectory(Path.Combine(root, area));
            var month = Path.Combine(root, "logs", DateTime.Today.ToString("yyyyMM"));
            Assert.False(Directory.Exists(month));

            DemoReadiness.ValidateStorage(root);

            Assert.True(Directory.Exists(month));
            Assert.Empty(Directory.GetFiles(root, ".readiness-*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingLogsDirectoryFailsWithoutBeingCreated()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-readiness-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var area in RequiredAreas.Where(area => area != "logs"))
                Directory.CreateDirectory(Path.Combine(root, area));

            Assert.Throws<DirectoryNotFoundException>(() => DemoReadiness.ValidateStorage(root));
            Assert.False(Directory.Exists(Path.Combine(root, "logs")));
            Assert.Empty(Directory.GetFiles(root, ".readiness-*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingAreaFailsAndCleansEarlierProbes()
    {
        var root = Path.Combine(Path.GetTempPath(), "attendta-readiness-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "keys"));
            Assert.Throws<DirectoryNotFoundException>(() => DemoReadiness.ValidateStorage(root));
            Assert.Empty(Directory.GetFiles(root, ".readiness-*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
