using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Restores a verified backup to a new private root and an empty dedicated instance.</summary>
internal static class DemoBackupRecovery
{
    internal static async Task RunAsync(string backupRoot, string root, string deployment, string instance,
        CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(instance) ||
            string.IsNullOrWhiteSpace(backupRoot) || !Path.IsPathFullyQualified(backupRoot))
            throw new InvalidOperationException("Explicit Windows backup and instance are required.");
        backupRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupRoot));
        root = DemoSetup.ValidateRoot(root, deployment);
        if (root.StartsWith(@"\\", StringComparison.Ordinal) ||
            DemoRemovalPreview.ContainsPath(root, backupRoot) || DemoRemovalPreview.ContainsPath(backupRoot, root) ||
            DemoRemovalPreview.ContainsPath(root, deployment) || DemoRemovalPreview.ContainsPath(deployment, root))
            throw new InvalidOperationException("Recovery root overlaps another target or is not local.");
        var masterString = DemoSetup.MasterConnection(instance);
        var entries = await DemoBackupVerification.VerifyFilesAsync(backupRoot, ct);
        foreach (var entry in entries.Where(e => e.Path.StartsWith("store/", StringComparison.Ordinal)))
            ValidateStorePath(entry.Path.Substring("store/".Length));
        var databaseBackup = Path.Combine(backupRoot, "database.bak");
        using var backupHandle = DemoBackupSourceFile.Open(databaseBackup);
        var backupEntry = entries.Single(e => e.Path == "database.bak");
        if (await DemoBackupManifest.MeasureAsync("database.bak", backupHandle, ct) != backupEntry)
            throw new InvalidDataException("Database backup changed.");

        await using var master = new SqlConnection(masterString);
        await master.OpenAsync(ct);
        await using (var guard = new SqlCommand("""
            DECLARE @lock int;
            EXEC @lock=sys.sp_getapplock @Resource=N'AttendTaDemoFreshSetup',
                @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0;
            IF @lock < 0 OR ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),0) <> 1
               OR EXISTS (SELECT 1 FROM sys.databases WHERE database_id>4)
                THROW 51240, 'Recovery instance is not empty and dedicated.', 1;
            """, master))
            await guard.ExecuteNonQueryAsync(ct);
        root = DemoSetup.ValidateRoot(root, deployment);
        DemoPrivateDirectory.CreateNew(root);
        var incomplete = Path.Combine(root, ".attendta-recovery-incomplete");
        await using (var marker = new FileStream(incomplete, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await marker.FlushAsync(ct);
        foreach (var area in new[] { "database", "keys", "logs", "temporary", "uploads/venues", "uploads/events", "certificates" })
            Directory.CreateDirectory(Path.Combine(root, area));
        foreach (var entry in entries.Where(e => e.Path.StartsWith("store/", StringComparison.Ordinal)))
        {
            var relative = entry.Path.Substring("store/".Length);
            var source = Path.Combine(backupRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            DemoRemovalPreview.RejectReparseAncestors(source);
            using var input = DemoBackupSourceFile.Open(source);
            if (await DemoBackupManifest.MeasureAsync(entry.Path, input, ct) != entry)
                throw new InvalidDataException("Backup file changed.");
            input.Position = 0;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await input.CopyToAsync(output, ct);
                await output.FlushAsync(ct);
            }
            using var check = DemoBackupSourceFile.Open(target);
            if (await DemoBackupManifest.MeasureAsync(entry.Path, check, ct) != entry)
                throw new InvalidDataException("Restored file checksum mismatch.");
        }
        await using (var restore = new SqlCommand("""
            RESTORE DATABASE [AttendTa_Demo] FROM DISK=@backup
            WITH CHECKSUM, MOVE N'AttendTa_Demo' TO @data,
            MOVE N'AttendTa_Demo_log' TO @log;
            """, master) { CommandTimeout = 0 })
        {
            restore.Parameters.Add("@backup", SqlDbType.NVarChar, 4000).Value = databaseBackup;
            restore.Parameters.Add("@data", SqlDbType.NVarChar, 4000).Value = Path.Combine(root, "database", "AttendTa_Demo.mdf");
            restore.Parameters.Add("@log", SqlDbType.NVarChar, 4000).Value = Path.Combine(root, "database", "AttendTa_Demo_log.ldf");
            await restore.ExecuteNonQueryAsync(ct);
        }
        using var markerInput = DemoBackupSourceFile.Open(Path.Combine(root, DemoOwnership.MarkerFile));
        using var reader = new StreamReader(markerInput);
        var markerText = DemoOwnership.ValidateMarker(await reader.ReadToEndAsync(ct));
        var connectionString = new SqlConnectionStringBuilder(masterString) { InitialCatalog = DemoTarget.Database };
        await using (var database = new SqlConnection(connectionString.ConnectionString))
        {
            await database.OpenAsync(ct);
            await DemoRestoreVerification.ValidateRestoredAsync(database, markerText, entries, ct);
        }
        await DemoOwnership.RevalidateLockedAsync(master, root, ct);
        await DemoBackupVerification.VerifyFilesAsync(backupRoot, ct);
        File.Delete(incomplete);
    }

    private static void ValidateStorePath(string path)
    {
        if (DemoUninstallInventory.SupportedStoreFile(path)) return;
        throw new InvalidDataException("Unsupported backup store path.");
    }
}
