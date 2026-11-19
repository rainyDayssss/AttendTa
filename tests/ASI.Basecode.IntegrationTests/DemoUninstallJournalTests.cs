using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoUninstallJournalTests
{
    [Fact]
    public void JournalIsNewExternalAndDoesNotRecordCredentialPath()
    {
        if (!OperatingSystem.IsWindows()) return;
        var parent = Path.Combine(Path.GetTempPath(), "attendta-journal-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var journalRoot = Path.Combine(parent, "journal");
        var source = Path.Combine(parent, "source");
        var backup = Path.Combine(parent, "backup");
        var credential = Path.Combine(parent, "private-credential-file.json");
        var file = Path.Combine(journalRoot, "uninstall.jsonl");
        try
        {
            using (var journal = DemoUninstallJournal.Create(journalRoot, source, backup, credential,
                AppContext.BaseDirectory, "AttendTaDemo_0123456789abcdef0123456789abcdef"))
            {
                Assert.Throws<InvalidOperationException>(() => journal.Record(DemoUninstallJournal.Stage.Completed));
                journal.Record(DemoUninstallJournal.Stage.BackupVerified);
                journal.Record(DemoUninstallJournal.Stage.DatabaseDropStarting);
            }
            var text = File.ReadAllText(file);
            Assert.Contains("Planned", text);
            Assert.Contains("BackupVerified", text);
            Assert.Contains("DatabaseDropStarting", text);
            Assert.DoesNotContain("private-credential-file.json", text);
            Assert.Throws<InvalidOperationException>(() => DemoUninstallJournal.Create(journalRoot,
                source, backup, credential, AppContext.BaseDirectory, "AttendTaDemo"));
            Assert.Throws<InvalidOperationException>(() => DemoUninstallJournal.Create(Path.Combine(source, "journal"),
                source, backup, credential, AppContext.BaseDirectory, "AttendTaDemo"));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(journalRoot)) Directory.Delete(journalRoot);
            Directory.Delete(parent);
        }
    }
}
