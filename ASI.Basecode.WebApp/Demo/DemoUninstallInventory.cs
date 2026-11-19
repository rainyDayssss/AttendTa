using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>
/// Snapshot of known store files held open against writes/deletes. This is not
/// deletion authority; callers must revalidate after releasing these handles.
/// </summary>
internal sealed class DemoUninstallInventory : IDisposable
{
    private static readonly string[] Directories =
        ["database", "keys", "logs", "temporary", "uploads", "uploads/venues", "uploads/events", "certificates"];
    private readonly List<FileStream> handles;
    internal IReadOnlyList<string> Files { get; }
    internal IReadOnlyList<string> DirectoryPaths { get; }

    private DemoUninstallInventory(List<FileStream> handles, IReadOnlyList<string> files, IReadOnlyList<string> directories)
    {
        this.handles = handles;
        Files = files;
        DirectoryPaths = directories;
    }

    internal static async Task<DemoUninstallInventory> AcquireAsync(string root,
        IReadOnlyList<DemoBackupManifest.Entry> manifest, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) || !Directory.Exists(root))
            throw new InvalidOperationException("An existing absolute source root is required.");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        DemoRemovalPreview.RejectReparseAncestors(root);
        var expected = new Dictionary<string, DemoBackupManifest.Entry>(StringComparer.OrdinalIgnoreCase);
        var databaseBackupCount = 0;
        foreach (var entry in manifest)
        {
            if (entry.Path == "database.bak") { databaseBackupCount++; continue; }
            if (!entry.Path.StartsWith("store/", StringComparison.Ordinal))
                throw new InvalidDataException("Unsupported backup entry.");
            var relative = entry.Path.Substring("store/".Length);
            if (!SupportedStoreFile(relative) || !expected.TryAdd(relative, entry))
                throw new InvalidDataException("Unsupported or duplicate store entry.");
        }
        if (databaseBackupCount != 1 || !expected.ContainsKey(DemoOwnership.MarkerFile))
            throw new InvalidDataException("Database backup or ownership marker is absent.");
        var databaseFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "database/AttendTa_Demo.mdf", "database/AttendTa_Demo_log.ldf" };
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in DemoRemovalPreview.Inventory(root))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
            {
                if (!directories.Add(relative)) throw new InvalidDataException("Duplicate directory.");
            }
            else if (!files.Add(relative)) throw new InvalidDataException("Duplicate file.");
        }
        if (!Directories.All(directories.Contains) ||
            directories.Any(d => !Directories.Contains(d, StringComparer.OrdinalIgnoreCase) && !IsLogDirectory(d)) ||
            !files.SetEquals(expected.Keys.Concat(databaseFiles)))
            throw new InvalidDataException("Store contains missing or unknown resources.");
        using (DemoBackupSourceFile.OpenDirectory(root)) { }
        foreach (var directory in directories)
            using (DemoBackupSourceFile.OpenDirectory(Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar)))) { }
        foreach (var name in databaseFiles)
            DemoBackupSourceFile.CheckMetadata(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)));
        var handles = new List<FileStream>();
        try
        {
            foreach (var item in expected.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var path = Path.Combine(root, item.Key.Replace('/', Path.DirectorySeparatorChar));
                DemoRemovalPreview.RejectReparseAncestors(path);
                var handle = DemoBackupSourceFile.Open(path);
                handles.Add(handle);
                var actual = await DemoBackupManifest.MeasureAsync(item.Value.Path, handle, ct);
                if (actual != item.Value) throw new InvalidDataException("Store file differs from backup.");
            }
            return new DemoUninstallInventory(handles,
                files.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
                directories.OrderByDescending(p => p.Count(c => c == '/')).ThenBy(p => p, StringComparer.Ordinal).ToArray());
        }
        catch
        {
            foreach (var handle in handles) handle.Dispose();
            throw;
        }
    }

    internal static bool IsLogDirectory(string path) =>
        Regex.IsMatch(path, @"\Alogs/[0-9]{6}\z", RegexOptions.CultureInvariant);

    internal static bool SupportedStoreFile(string path)
    {
        if (path == DemoOwnership.MarkerFile) return true;
        var parts = path.Split('/');
        return (parts.Length == 3 && IsLogDirectory(parts[0] + "/" + parts[1]) &&
                Regex.IsMatch(parts[2], @"\A(?:log-|Debug_|Warning_|Error_|Services_)[0-9]{8}(?:_[0-9]{3,})?\.txt\z",
                    RegexOptions.CultureInvariant)) ||
            (parts.Length == 2 && (parts[0] == "keys" || parts[0] == "certificates")) ||
            (parts.Length == 3 && parts[0] == "uploads" && (parts[1] == "venues" || parts[1] == "events"));
    }

    public void Dispose()
    {
        foreach (var handle in handles) handle.Dispose();
        handles.Clear();
    }
}
