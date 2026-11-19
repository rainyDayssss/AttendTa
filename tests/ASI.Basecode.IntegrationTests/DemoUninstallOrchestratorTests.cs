using ASI.Basecode.WebApp.Demo;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoUninstallOrchestratorTests
{
    private sealed class FakeOperations : IDemoUninstallOperations
    {
        internal readonly List<string> Calls = [];
        internal string FailAt = "";
        private Task Call(string name)
        {
            Calls.Add(name);
            if (name == FailAt) throw new IOException("Injected failure");
            return Task.CompletedTask;
        }
        public Task VerifyFreshBackupAsync(CancellationToken ct) => Call("backup");
        public Task RevalidateTargetsAsync(CancellationToken ct) => Call("revalidate");
        public Task DropDatabaseAsync(CancellationToken ct) => Call("drop");
        public Task RemoveInventoriedStoreAsync(CancellationToken ct) => Call("store");
        public Task RecoverAsync(CancellationToken ct) => Call("recover");
    }

    [Fact]
    public async Task DeclineAndCancellationDoNotInvokeOperations()
    {
        var fake = new FakeOperations();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DemoUninstallOrchestrator.RunAsync(false, fake, _ => { }, TestContext.Current.CancellationToken));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DemoUninstallOrchestrator.RunAsync(true, fake, _ => { }, cancelled.Token));
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task InterruptedDropStopsAndRecoveryIsExplicit()
    {
        var fake = new FakeOperations { FailAt = "drop" };
        var records = new List<DemoUninstallJournal.Stage>();
        await Assert.ThrowsAsync<IOException>(() =>
            DemoUninstallOrchestrator.RunAsync(true, fake, records.Add, TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "backup", "revalidate", "drop" }, fake.Calls);
        Assert.Equal(new[] { DemoUninstallJournal.Stage.BackupVerified, DemoUninstallJournal.Stage.DatabaseDropStarting }, records);
        await DemoUninstallOrchestrator.RecoverAsync(fake, TestContext.Current.CancellationToken);
        Assert.Equal("recover", fake.Calls.Last());
        Assert.DoesNotContain("store", fake.Calls);
    }

    [Fact]
    public async Task RevalidationOrJournalFailureStopsBeforeDrop()
    {
        var fake = new FakeOperations { FailAt = "revalidate" };
        await Assert.ThrowsAsync<IOException>(() =>
            DemoUninstallOrchestrator.RunAsync(true, fake, _ => { }, TestContext.Current.CancellationToken));
        Assert.DoesNotContain("drop", fake.Calls);
        var second = new FakeOperations();
        await Assert.ThrowsAsync<IOException>(() => DemoUninstallOrchestrator.RunAsync(true, second,
            stage => { if (stage == DemoUninstallJournal.Stage.DatabaseDropStarting) throw new IOException("Journal failure"); },
            TestContext.Current.CancellationToken));
        Assert.DoesNotContain("drop", second.Calls);
    }

    [Fact]
    public async Task SuccessfulFakeRunRecordsEveryStageInOrder()
    {
        var fake = new FakeOperations();
        var records = new List<DemoUninstallJournal.Stage>();
        await DemoUninstallOrchestrator.RunAsync(true, fake, records.Add, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "backup", "revalidate", "drop", "store" }, fake.Calls);
        Assert.Equal(new[] { DemoUninstallJournal.Stage.BackupVerified, DemoUninstallJournal.Stage.DatabaseDropStarting,
            DemoUninstallJournal.Stage.DatabaseDropped, DemoUninstallJournal.Stage.StoreRemovalStarting,
            DemoUninstallJournal.Stage.Completed }, records);
    }

    [Fact]
    public async Task InjectedStoreFailureLeavesDurableJournalForExplicitRecovery()
    {
        if (!OperatingSystem.IsWindows()) return;
        var parent = Path.Combine(Path.GetTempPath(), "attendta-orchestrator-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var journalRoot = Path.Combine(parent, "journal");
        var journalFile = Path.Combine(journalRoot, "uninstall.jsonl");
        try
        {
            var fake = new FakeOperations { FailAt = "store" };
            using (var journal = DemoUninstallJournal.Create(journalRoot, Path.Combine(parent, "source"),
                Path.Combine(parent, "backup"), Path.Combine(parent, "credentials.json"),
                AppContext.BaseDirectory, "AttendTaDemo"))
            {
                await Assert.ThrowsAsync<IOException>(() => DemoUninstallOrchestrator.RunAsync(
                    true, fake, journal.Record, TestContext.Current.CancellationToken));
            }
            var contents = await File.ReadAllTextAsync(journalFile, TestContext.Current.CancellationToken);
            Assert.Contains("DatabaseDropped", contents);
            Assert.Contains("StoreRemovalStarting", contents);
            Assert.DoesNotContain("Completed", contents);
            Assert.DoesNotContain("recover", fake.Calls);
            await DemoUninstallOrchestrator.RecoverAsync(fake, TestContext.Current.CancellationToken);
            Assert.Equal("recover", fake.Calls.Last());
        }
        finally
        {
            if (File.Exists(journalFile)) File.Delete(journalFile);
            if (Directory.Exists(journalRoot)) Directory.Delete(journalRoot);
            Directory.Delete(parent);
        }
    }
}
