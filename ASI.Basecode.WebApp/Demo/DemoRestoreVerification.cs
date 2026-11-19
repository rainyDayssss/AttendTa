using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Tests a selected backup in a newly created disposable LocalDB instance.</summary>
internal static class DemoRestoreVerification
{
    internal static async Task<(string Instance, string TemporaryRoot)> RunAsync(string backupRoot, CancellationToken cancellationToken)
    {
        var entries = await DemoBackupVerification.VerifyFilesAsync(backupRoot, cancellationToken);
        backupRoot = Path.GetFullPath(backupRoot);
        var backupPath = Path.Combine(backupRoot, "database.bak");
        var markerPath = Path.Combine(backupRoot, "store", DemoOwnership.MarkerFile);
        using var backupHandle = DemoBackupSourceFile.Open(backupPath);
        using var markerHandle = DemoBackupSourceFile.Open(markerPath);
        using var markerReader = new StreamReader(markerHandle);
        var marker = DemoOwnership.ValidateMarker(await markerReader.ReadToEndAsync(cancellationToken));
        var backupEntry = entries.Single(e => e.Path == "database.bak");
        if (await DemoBackupManifest.MeasureAsync("database.bak", backupHandle, cancellationToken) != backupEntry)
            throw new InvalidDataException("Database backup changed after verification.");
        var markerEntry = entries.Single(e => e.Path == "store/" + DemoOwnership.MarkerFile);
        if (await DemoBackupManifest.MeasureAsync(markerEntry.Path, markerHandle, cancellationToken) != markerEntry)
            throw new InvalidDataException("Ownership marker changed after verification.");

        var instance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var tempParent = Path.GetFullPath(Path.GetTempPath());
        DemoRemovalPreview.RejectReparseAncestors(tempParent);
        var root = Path.Combine(tempParent, "attendta-restore-check-" + Guid.NewGuid().ToString("N"));
        if (DemoRemovalPreview.ContainsPath(backupRoot, root) || DemoRemovalPreview.ContainsPath(root, backupRoot))
            throw new InvalidOperationException("Restore target overlaps backup.");
        DemoPrivateDirectory.CreateNew(root);
        var created = false;
        var cleanupSafe = false;
        try
        {
            var before = await InstanceNamesAsync(cancellationToken);
            if (before.Contains(instance)) throw new InvalidOperationException("Generated instance already exists.");
            var creation = await LocalDbAsync(cancellationToken, "create", instance);
            // SqlLocalDB can return exit code zero for errors. Only recognize its explicit
            // success message; unknown/localized output leaves any resources untouched.
            if (creation.ExitCode != 0 || !creation.Output.Contains(
                    $"LocalDB instance \"{instance}\" created with version", StringComparison.Ordinal))
                throw new InvalidOperationException("Could not prove disposable instance creation.");
            created = true;
            var start = await LocalDbAsync(cancellationToken, "start", instance);
            if (start.ExitCode != 0 || !start.Output.Contains(
                    $"LocalDB instance \"{instance}\" started.", StringComparison.Ordinal))
                throw new InvalidOperationException("Could not start disposable instance.");

            var masterString = DemoSetup.MasterConnection(instance);
            await using (var master = new SqlConnection(masterString))
            {
                await master.OpenAsync(cancellationToken);
                await using (var guard = new SqlCommand("""
                    IF ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),0) <> 1
                       OR EXISTS (SELECT 1 FROM sys.databases WHERE database_id > 4)
                       THROW 51230, 'Disposable instance is not empty.', 1;
                    """, master))
                    await guard.ExecuteNonQueryAsync(cancellationToken);
                await using var restore = new SqlCommand("""
                    RESTORE DATABASE [AttendTa_RestoreTest] FROM DISK=@backup
                    WITH CHECKSUM, MOVE N'AttendTa_Demo' TO @data,
                    MOVE N'AttendTa_Demo_log' TO @log;
                    """, master) { CommandTimeout = 0 };
                restore.Parameters.Add("@backup", SqlDbType.NVarChar, 4000).Value = backupPath;
                restore.Parameters.Add("@data", SqlDbType.NVarChar, 4000).Value = Path.Combine(root, "restored.mdf");
                restore.Parameters.Add("@log", SqlDbType.NVarChar, 4000).Value = Path.Combine(root, "restored.ldf");
                await restore.ExecuteNonQueryAsync(cancellationToken);
            }
            var db = new SqlConnectionStringBuilder(masterString) { InitialCatalog = "AttendTa_RestoreTest" };
            await using (var restored = new SqlConnection(db.ConnectionString))
            {
                await restored.OpenAsync(cancellationToken);
                await ValidateRestoredAsync(restored, marker, entries, cancellationToken);
            }
            // Detect changes to any backup file during the restore.
            await DemoBackupVerification.VerifyFilesAsync(backupRoot, cancellationToken);
            cleanupSafe = true;
        }
        finally
        {
            // Failure can leave an instance or restore files in an unknown state. Never
            // guess in that case; report exact disposable targets for operator inspection.
            if (created && cleanupSafe)
            {
                try
                {
                    var stop = await LocalDbAsync(CancellationToken.None, "stop", instance, "-k");
                    if (stop.ExitCode != 0 || !stop.Output.Contains($"LocalDB instance \"{instance}\" stopped.", StringComparison.Ordinal))
                        throw new IOException("Could not stop verification instance.");
                    var delete = await LocalDbAsync(CancellationToken.None, "delete", instance);
                    if (delete.ExitCode != 0 || !delete.Output.Contains($"LocalDB instance \"{instance}\" deleted.", StringComparison.Ordinal)
                        || (await InstanceNamesAsync(CancellationToken.None)).Contains(instance))
                        throw new IOException("Could not verify instance cleanup.");
                    DemoRemovalPreview.RejectReparseAncestors(root);
                    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "restored.mdf", "restored.ldf" };
                    var actual = Directory.GetFileSystemEntries(root);
                    if (actual.Any(p => !allowed.Contains(Path.GetFileName(p)) ||
                        (File.GetAttributes(p) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0))
                        throw new IOException("Unexpected verification files.");
                    foreach (var path in actual) { DemoBackupSourceFile.CheckMetadata(path); File.Delete(path); }
                    Directory.Delete(root);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"Restore verification cleanup requires inspection. Instance: {instance}; temporary root: {root}");
                    throw new IOException($"Restore verification cleanup requires inspection. Instance: {instance}; temporary root: {root}", exception);
                }
            }
            else
                Console.Error.WriteLine($"Restore verification did not complete; inspect disposable instance {instance} and temporary root {root}. No automatic cleanup was attempted.");
        }
        return (instance, root);
    }

    internal static async Task ValidateRestoredAsync(SqlConnection restored, string marker,
        IReadOnlyList<DemoBackupManifest.Entry> entries, CancellationToken cancellationToken)
    {
        await using (var read = new SqlCommand("""
            SELECT CONVERT(nvarchar(32), value) FROM sys.extended_properties
            WHERE class=0 AND name=N'AttendTaDemoOwnershipV1';
            """, restored))
            if (!string.Equals(await read.ExecuteScalarAsync(cancellationToken) as string, marker, StringComparison.Ordinal))
                throw new InvalidDataException("Restored database marker does not match backup store.");
        var files = new HashSet<string>(entries.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
        await using var references = new SqlCommand("""
            SELECT N'venue', image_path FROM dbo.venues
            UNION ALL SELECT N'event', image_path FROM dbo.events
            UNION ALL SELECT N'certificate', file_path FROM dbo.certificates;
            """, restored);
        await using var reader = await references.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var kind = reader.GetString(0);
            var value = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (kind == "venue") throw new InvalidDataException("Venue image is missing.");
                continue;
            }
            var parts = value.Split('/');
            var valid = kind switch
            {
                "venue" => parts.Length == 3 && parts[0] == "uploads" && parts[1] == "venues",
                "event" => parts.Length == 3 && parts[0] == "uploads" && parts[1] == "events",
                "certificate" => parts.Length == 2 && parts[0] == "certificates",
                _ => false
            };
            if (!valid || parts.Any(p => string.IsNullOrWhiteSpace(p) || p is "." or "..") ||
                value.Contains('\\') || value.Contains(':') || !files.Contains("store/" + value))
                throw new InvalidDataException("Restored database references a missing or unsafe private file.");
        }
    }

    internal static async Task<HashSet<string>> InstanceNamesAsync(CancellationToken ct)
    {
        var result = await LocalDbAsync(ct, "info");
        if (result.ExitCode != 0) throw new IOException("Cannot enumerate LocalDB instances.");
        return new HashSet<string>(result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<(int ExitCode, string Output)> LocalDbAsync(CancellationToken ct, params string[] arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("SqlLocalDB.exe")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new IOException("Cannot start SqlLocalDB.");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, (await output) + (await error));
    }
}
