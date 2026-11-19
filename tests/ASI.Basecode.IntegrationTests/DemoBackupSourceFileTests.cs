using System.ComponentModel;
using System.Runtime.InteropServices;
using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoBackupSourceFileTests
{
    [Fact]
    public void DirectoryIsRefused()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(), "attendta-backup-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try { Assert.Throws<InvalidOperationException>(() => DemoBackupSourceFile.Open(path)); }
        finally { Directory.Delete(path); }
    }

    [Fact]
    public void SingleLinkFileCanBeReadButNotOpenedForWritingWhileHeld()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(), "attendta-backup-source-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "owned fixture");
            using var stream = DemoBackupSourceFile.Open(path);
            using var reader = new StreamReader(stream);
            Assert.Equal("owned fixture", reader.ReadToEnd());
            Assert.ThrowsAny<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void HardLinkedFileIsRefusedAndBothLinksRemain()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(), "attendta-backup-source-" + Guid.NewGuid().ToString("N"));
        var link = path + "-link";
        try
        {
            File.WriteAllText(path, "owned fixture");
            if (!CreateHardLinkW(link, path, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Assert.Throws<InvalidOperationException>(() => DemoBackupSourceFile.Open(path));
            Assert.Throws<InvalidOperationException>(() => DemoBackupSourceFile.CheckMetadata(path));
            Assert.Equal("owned fixture", File.ReadAllText(link));
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(link)) File.Delete(link);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("hidden data")]
    public void NamedStreamIsRefusedByAllSourceOpenModes(string content)
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(), "attendta-stream-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "default");
            File.WriteAllText(path + ":fixture", content);
            Assert.Throws<InvalidDataException>(() => DemoBackupSourceFile.Open(path));
            Assert.Throws<InvalidDataException>(() => DemoBackupSourceFile.OpenForDeletion(path));
            Assert.Throws<InvalidDataException>(() => DemoBackupSourceFile.CheckMetadata(path));
            Assert.Equal("default", File.ReadAllText(path));
            Assert.Equal(content, File.ReadAllText(path + ":fixture"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void ValidatedDeletionDeletesOnlyTheOpenedFixture()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(Path.GetTempPath(), "attendta-delete-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "fixture");
            using (var file = DemoBackupSourceFile.OpenForDeletion(path))
                DemoBackupSourceFile.DeleteOpened(file);
            Assert.False(File.Exists(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string newName, string existingName, IntPtr securityAttributes);
}
