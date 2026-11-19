using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Non-destructive preflight. Its results are not authority to uninstall.</summary>
internal static class DemoUninstallPreflight
{
    internal static bool Confirmed(string instance, string root, string offlineAnswer,
        string instanceAnswer, string rootAnswer) =>
        !string.IsNullOrWhiteSpace(instance) && !string.IsNullOrWhiteSpace(root) &&
        offlineAnswer == "OFFLINE" && instanceAnswer == instance && rootAnswer == root;

    internal static async Task<IReadOnlyList<string>> RunAsync(string root, string deployment, string instance,
        string credentialFile, string backupRoot, CancellationToken ct)
    {
        await DemoRemovalPreview.ValidateInputsAsync(root, deployment, instance, credentialFile, ct);
        if (string.IsNullOrWhiteSpace(backupRoot) || !Path.IsPathFullyQualified(backupRoot))
            throw new InvalidOperationException("An explicit backup root is required.");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        backupRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupRoot));
        deployment = Path.GetFullPath(deployment);
        credentialFile = Path.GetFullPath(credentialFile);
        foreach (var other in new[] { root, deployment, credentialFile })
            if (DemoRemovalPreview.ContainsPath(other, backupRoot) || DemoRemovalPreview.ContainsPath(backupRoot, other))
                throw new InvalidOperationException("Backup overlaps another selected target.");

        // This verifies the selected backup but does not keep the source SQL lock
        // through the restore. A successful result must never authorize deletion.
        await DemoRestoreVerification.RunAsync(backupRoot, ct);
        var manifest = await DemoBackupVerification.VerifyFilesAsync(backupRoot, ct);
        IReadOnlyList<string> inventory = null;
        await DemoOwnership.InspectAsync(root, deployment, instance, ct, async master =>
        {
            inventory = await CheckLockedSourceAsync(root, deployment, instance, credentialFile, master, manifest, ct);
        });
        return inventory;
    }

    internal static async Task<IReadOnlyList<string>> CheckLockedSourceAsync(string root, string deployment,
        string instance, string credentialFile, SqlConnection master, IReadOnlyList<DemoBackupManifest.Entry> manifest,
        CancellationToken ct)
    {
        await DemoRemovalPreview.ValidateInputsAsync(root, deployment, instance, credentialFile, ct);
        var inventory = DemoRemovalPreview.Inventory(root);
        var files = DemoProtectedBackup.SelectFiles(root, inventory);
        var stored = manifest?.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
        if (stored is not null)
        {
            var expectedStore = new HashSet<string>(files.Select(p => "store/" + p), StringComparer.OrdinalIgnoreCase);
            if (!expectedStore.SetEquals(stored.Keys.Where(p => p.StartsWith("store/", StringComparison.Ordinal))))
                throw new InvalidDataException("Backup and current store inventories differ.");
        }
        foreach (var relative in files)
        {
            var source = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            DemoRemovalPreview.RejectReparseAncestors(source);
            using var input = DemoBackupSourceFile.Open(source);
            if (stored is not null)
            {
                var measured = await DemoBackupManifest.MeasureAsync("store/" + relative, input, ct);
                if (measured != stored[measured.Path])
                    throw new InvalidDataException("Backup and current store contents differ.");
            }
        }
        foreach (var name in new[] { "AttendTa_Demo.mdf", "AttendTa_Demo_log.ldf" })
            DemoBackupSourceFile.CheckMetadata(Path.Combine(root, "database", name));
        // InspectAsync has already closed its own demo-database connection.
        await using var sessions = new SqlCommand("""
            SELECT COUNT(*) FROM sys.dm_exec_sessions
            WHERE database_id=DB_ID(N'AttendTa_Demo') AND is_user_process=1;
            """, master);
        if (Convert.ToInt32(await sessions.ExecuteScalarAsync(ct)) != 0)
            throw new InvalidOperationException("Unexpected demo database sessions.");
        return inventory;
    }
}
