using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Demo;

// No production implementation is supplied: destructive operations are tested only with fakes.
internal interface IDemoUninstallOperations
{
    Task VerifyFreshBackupAsync(CancellationToken ct);
    Task RevalidateTargetsAsync(CancellationToken ct);
    Task DropDatabaseAsync(CancellationToken ct);
    Task RemoveInventoriedStoreAsync(CancellationToken ct);
    Task RecoverAsync(CancellationToken ct);
}

internal static class DemoUninstallOrchestrator
{
    internal static async Task RunAsync(bool confirmed, IDemoUninstallOperations operations,
        Action<DemoUninstallJournal.Stage> record, CancellationToken ct)
    {
        if (!confirmed) throw new OperationCanceledException("Target confirmation declined.");
        ct.ThrowIfCancellationRequested();
        await operations.VerifyFreshBackupAsync(ct);
        record(DemoUninstallJournal.Stage.BackupVerified);
        ct.ThrowIfCancellationRequested();
        await operations.RevalidateTargetsAsync(ct);
        ct.ThrowIfCancellationRequested();
        record(DemoUninstallJournal.Stage.DatabaseDropStarting);
        await operations.DropDatabaseAsync(ct);
        record(DemoUninstallJournal.Stage.DatabaseDropped);
        ct.ThrowIfCancellationRequested();
        record(DemoUninstallJournal.Stage.StoreRemovalStarting);
        await operations.RemoveInventoriedStoreAsync(ct);
        record(DemoUninstallJournal.Stage.Completed);
    }

    // Recovery is explicit, never automatically performed over a partially removed target.
    internal static Task RecoverAsync(IDemoUninstallOperations operations, CancellationToken ct) =>
        operations.RecoverAsync(ct);
}
