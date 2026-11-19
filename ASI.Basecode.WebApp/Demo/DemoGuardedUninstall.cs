using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Offline uninstall of a paired-marker demo. Never removes application binaries.</summary>
internal static class DemoGuardedUninstall
{
    internal static async Task RunAsync(bool confirmed, string root, string deployment, string instance,
        string credentialFile, string backupRoot, string journalRoot, IReadOnlyList<string> preview,
        CancellationToken ct, Func<Task> afterDropForTest = null, Func<Task> beforeRemoval = null,
        IReadOnlyDictionary<string, string> preserveCredentials = null)
    {
        if (!confirmed) throw new OperationCanceledException("Uninstall declined.");
        ct.ThrowIfCancellationRequested();
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var approved = new HashSet<string>(preview.Select(p => Path.GetFullPath(p)), StringComparer.OrdinalIgnoreCase);
        if (approved.Count != preview.Count) throw new InvalidDataException("Ambiguous preview.");
        using var journal = DemoUninstallJournal.Create(journalRoot, root, backupRoot, credentialFile, deployment, instance);
        await DemoLockedBackupVerification.RunAsync(root, deployment, instance, credentialFile, backupRoot, ct,
            afterVerified: async master =>
            {
                if (beforeRemoval is not null) await beforeRemoval();
                var adapter = new Operations(root, deployment, instance, credentialFile, backupRoot, master,
                    journal, approved, afterDropForTest, preserveCredentials);
                await DemoUninstallOrchestrator.RunAsync(true, adapter, journal.Record, ct);
            }, beforeBackup: preserveCredentials is null ? null :
                master => DemoExistingCredentials.VerifyAsync(master, preserveCredentials, ct));
    }

    private sealed class Operations(string root, string deployment, string instance, string credentialFile,
        string backupRoot, SqlConnection master, DemoUninstallJournal journal, HashSet<string> approved,
        Func<Task> afterDropForTest, IReadOnlyDictionary<string, string> preserveCredentials) : IDemoUninstallOperations
    {
        private IReadOnlyList<DemoBackupManifest.Entry> manifest;
        private string[] sourceFiles;
        private string[] sourceDirectories;

        public async Task VerifyFreshBackupAsync(CancellationToken ct) =>
            manifest = await DemoBackupVerification.VerifyFilesAsync(backupRoot, ct);

        public async Task RevalidateTargetsAsync(CancellationToken ct)
        {
            await DemoOwnership.RevalidateLockedAsync(master, root, ct);
            await DemoUninstallPreflight.CheckLockedSourceAsync(root, deployment, instance, credentialFile, master, manifest, ct);
            if (preserveCredentials is not null)
                await DemoExistingCredentials.VerifyAsync(master, preserveCredentials, ct);
            using var inventory = await DemoUninstallInventory.AcquireAsync(root, manifest, ct);
            var current = new HashSet<string>(inventory.Files.Concat(inventory.DirectoryPaths)
                .Select(p => Path.GetFullPath(Path.Combine(root, p.Replace('/', Path.DirectorySeparatorChar)))),
                StringComparer.OrdinalIgnoreCase);
            if (!current.SetEquals(approved))
                throw new InvalidDataException("Current source differs from the explicitly previewed targets.");
            sourceFiles = inventory.Files.ToArray();
            sourceDirectories = inventory.DirectoryPaths.ToArray();
        }

        public async Task DropDatabaseAsync(CancellationToken ct)
        {
            // The preceding journal flush may take time: recheck immediately before DROP.
            await VerifyFreshBackupAsync(ct);
            await RevalidateTargetsAsync(ct);
            await using (var drop = new SqlCommand("DROP DATABASE [AttendTa_Demo];", master) { CommandTimeout = 30 })
                await drop.ExecuteNonQueryAsync(ct);
            if (afterDropForTest is not null) await afterDropForTest();
            await using var check = new SqlCommand("SELECT DB_ID(N'AttendTa_Demo');", master);
            if (await check.ExecuteScalarAsync(ct) is not DBNull)
                throw new InvalidOperationException("Database removal could not be verified.");
        }

        public async Task RemoveInventoriedStoreAsync(CancellationToken ct)
        {
            await VerifyFreshBackupAsync(ct);
            var databaseFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "database/AttendTa_Demo.mdf", "database/AttendTa_Demo_log.ldf" };
            var present = DemoRemovalPreview.Inventory(root)
                .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expected = new HashSet<string>(sourceFiles.Concat(sourceDirectories), StringComparer.OrdinalIgnoreCase);
            foreach (var dbFile in databaseFiles)
                if (!present.Contains(dbFile)) expected.Remove(dbFile);
            if (!present.SetEquals(expected))
                throw new InvalidDataException("Store changed after database removal; manual recovery is required.");

            using (DemoBackupSourceFile.OpenDirectory(root)) { }
            foreach (var relative in sourceDirectories)
                using (DemoBackupSourceFile.OpenDirectory(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)))) { }
            var entries = manifest.Where(e => e.Path.StartsWith("store/", StringComparison.Ordinal))
                .ToDictionary(e => e.Path.Substring("store/".Length), StringComparer.OrdinalIgnoreCase);
            // Check the entire remaining file set before deleting any one of them.
            foreach (var relative in sourceFiles.Where(present.Contains))
            {
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                DemoRemovalPreview.RejectReparseAncestors(path);
                using var file = DemoBackupSourceFile.Open(path);
            }
            // Marker last: preserve ownership evidence for as long as possible.
            var files = sourceFiles.Where(p => !databaseFiles.Contains(p))
                .OrderBy(p => p == DemoOwnership.MarkerFile ? 1 : 0).ThenBy(p => p, StringComparer.Ordinal).ToArray();
            foreach (var relative in files)
            {
                ct.ThrowIfCancellationRequested();
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                DemoRemovalPreview.RejectReparseAncestors(path);
                using (var handle = DemoBackupSourceFile.OpenForDeletion(path))
                {
                    if (await DemoBackupManifest.MeasureAsync(entries[relative].Path, handle, ct) != entries[relative])
                        throw new InvalidDataException("Store file changed after backup.");
                    journal.RecordTarget("delete-file", relative);
                    DemoBackupSourceFile.DeleteOpened(handle);
                }
                if (File.Exists(path)) throw new IOException("File removal could not be verified.");
            }
            foreach (var relative in databaseFiles.Where(present.Contains))
            {
                ct.ThrowIfCancellationRequested();
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                DemoRemovalPreview.RejectReparseAncestors(path);
                using (var handle = DemoBackupSourceFile.OpenForDeletion(path))
                {
                    journal.RecordTarget("delete-file", relative);
                    DemoBackupSourceFile.DeleteOpened(handle);
                }
                if (File.Exists(path)) throw new IOException("Database file removal could not be verified.");
            }
            foreach (var relative in sourceDirectories)
            {
                ct.ThrowIfCancellationRequested();
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                journal.RecordTarget("delete-directory", relative);
                using (var handle = DemoBackupSourceFile.OpenDirectory(path, delete: true))
                    DemoBackupSourceFile.DeleteOpened(handle);
                if (Directory.Exists(path)) throw new IOException("Directory removal could not be verified.");
            }
            journal.RecordTarget("delete-directory", ".");
            using (var handle = DemoBackupSourceFile.OpenDirectory(root, delete: true))
                DemoBackupSourceFile.DeleteOpened(handle);
            if (Directory.Exists(root)) throw new IOException("Directory removal could not be verified.");
        }

        public Task RecoverAsync(CancellationToken ct) =>
            throw new InvalidOperationException("Use --recover-demo-backup into a new dedicated instance and new root.");
    }
}
