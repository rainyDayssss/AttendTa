using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Offline, non-destructive backup for an explicitly selected paired-marker install.</summary>
internal static class DemoProtectedBackup
{
    internal static async Task RunAsync(string root, string deployment, string instance, string credentialFile,
        string destination, CancellationToken cancellationToken,
        Func<SqlConnection, Task> beforeBackup = null, Func<SqlConnection, Task> afterBackup = null)
    {
        await DemoRemovalPreview.ValidateInputsAsync(root, deployment, instance, credentialFile, cancellationToken);
        if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathFullyQualified(destination))
            throw new InvalidOperationException("An absolute new backup destination is required.");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        deployment = Path.TrimEndingDirectorySeparator(Path.GetFullPath(deployment));
        destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        credentialFile = Path.GetFullPath(credentialFile);
        foreach (var other in new[] { root, deployment, credentialFile })
            if (DemoRemovalPreview.ContainsPath(other, destination) || DemoRemovalPreview.ContainsPath(destination, other))
                throw new InvalidOperationException("Backup destination overlaps another target.");
        var parent = Directory.GetParent(destination);
        if (parent is null || !parent.Exists || Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidOperationException("Backup destination must be a new child of an existing directory.");
        DemoRemovalPreview.RejectReparseAncestors(parent.FullName);

        await DemoOwnership.InspectAsync(root, deployment, instance, cancellationToken, async master =>
        {
            await DemoRemovalPreview.ValidateInputsAsync(root, deployment, instance, credentialFile, cancellationToken);
            DemoRemovalPreview.RejectReparseAncestors(parent.FullName);
            var inventory = DemoRemovalPreview.Inventory(root);
            var copies = SelectFiles(root, inventory);
            using (DemoBackupSourceFile.OpenDirectory(root)) { }
            foreach (var directory in inventory.Where(p => (File.GetAttributes(p) & FileAttributes.Directory) != 0))
                using (DemoBackupSourceFile.OpenDirectory(directory)) { }
            foreach (var relative in copies)
            {
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                DemoRemovalPreview.RejectReparseAncestors(path);
                using var source = DemoBackupSourceFile.Open(path);
            }
            foreach (var name in new[] { "AttendTa_Demo.mdf", "AttendTa_Demo_log.ldf" })
            {
                var path = Path.Combine(root, "database", name);
                DemoRemovalPreview.RejectReparseAncestors(path);
                DemoBackupSourceFile.CheckMetadata(path);
            }
            if (beforeBackup is not null) await beforeBackup(master);
            DemoPrivateDirectory.CreateNew(destination);
            var incomplete = Path.Combine(destination, "INCOMPLETE");
            await File.WriteAllTextAsync(incomplete, "Backup is incomplete until renamed COMPLETE.\n", cancellationToken);
            var databaseBackup = Path.Combine(destination, "database.bak");
            await using (var backup = new SqlCommand("""
                BACKUP DATABASE [AttendTa_Demo] TO DISK = @path WITH COPY_ONLY, CHECKSUM;
                RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;
                """, master) { CommandTimeout = 0 })
            {
                backup.Parameters.Add("@path", SqlDbType.NVarChar, 4000).Value = databaseBackup;
                await backup.ExecuteNonQueryAsync(cancellationToken);
            }
            var entries = new List<DemoBackupManifest.Entry>();
            using (var stream = DemoBackupSourceFile.Open(databaseBackup))
                entries.Add(await DemoBackupManifest.MeasureAsync("database.bak", stream, cancellationToken));

            foreach (var relative in copies)
            {
                var source = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                DemoRemovalPreview.RejectReparseAncestors(source);
                using var input = DemoBackupSourceFile.Open(source);
                var expected = await DemoBackupManifest.MeasureAsync(relative, input, cancellationToken);
                input.Position = 0;
                var outputPath = Path.Combine(destination, "store", relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                await using (var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await input.CopyToAsync(output, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }
                using var verify = DemoBackupSourceFile.Open(outputPath);
                var actual = await DemoBackupManifest.MeasureAsync("store/" + relative, verify, cancellationToken);
                if (expected.Length != actual.Length || expected.Sha256 != actual.Sha256)
                    throw new IOException("Copied file verification failed.");
                entries.Add(actual);
            }
            var manifest = DemoBackupManifest.Serialize(entries);
            var manifestPath = Path.Combine(destination, "manifest.json");
            await using (var output = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await output.WriteAsync(manifest, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            var readBack = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
            if (!manifest.SequenceEqual(readBack) || DemoBackupManifest.Parse(readBack).Count != entries.Count)
                throw new IOException("Manifest verification failed.");
            File.Move(incomplete, Path.Combine(destination, "COMPLETE"));
            if (afterBackup is not null) await afterBackup(master);
        });
    }

    internal static IReadOnlyList<string> SelectFiles(string root, IReadOnlyList<string> inventory)
    {
        var result = new List<string>();
        foreach (var path in inventory)
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var parts = relative.Split('/');
            var directory = (File.GetAttributes(path) & FileAttributes.Directory) != 0;
            var area = parts[0].ToLowerInvariant();
            if (area == "logs")
            {
                if (parts.Length == 1 && directory) continue;
                if (directory && DemoUninstallInventory.IsLogDirectory(relative)) continue;
                if (!directory && DemoUninstallInventory.SupportedStoreFile(relative))
                { result.Add(relative); continue; }
                throw new InvalidDataException("Unexpected logs entry.");
            }
            if (area == "temporary")
            {
                if (parts.Length == 1 && directory) continue;
                throw new InvalidDataException("Temporary store must be empty.");
            }
            if (area == "database")
            {
                if (parts.Length == 1 && !directory) throw new InvalidOperationException("Unexpected database store entry.");
                if (parts.Length > 2 || (parts.Length == 2 && (directory ||
                    (parts[1] != "AttendTa_Demo.mdf" && parts[1] != "AttendTa_Demo_log.ldf"))))
                    throw new InvalidOperationException("Unexpected database store entry.");
                continue;
            }
            if (area == "uploads")
            {
                if (parts.Length == 1 && directory) continue;
                if (parts.Length == 2 && directory && (parts[1] == "events" || parts[1] == "venues")) continue;
                if (parts.Length == 3 && !directory && (parts[1] == "events" || parts[1] == "venues"))
                { result.Add(relative); continue; }
                throw new InvalidOperationException("Unexpected uploads entry.");
            }
            if (area is "keys" or "certificates")
            {
                if (parts.Length == 1 && directory) continue;
                if (parts.Length == 2 && !directory) { result.Add(relative); continue; }
                throw new InvalidOperationException("Unexpected private store entry.");
            }
            if (relative == DemoOwnership.MarkerFile && !directory) { result.Add(relative); continue; }
            throw new InvalidOperationException("Unexpected private store entry.");
        }
        return result;
    }
}
