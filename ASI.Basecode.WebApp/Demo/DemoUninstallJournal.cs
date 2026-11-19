using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>
/// Durable intent/progress records for a future uninstall. This class never removes a resource
/// and is not yet connected to a destructive workflow.
/// </summary>
internal sealed class DemoUninstallJournal : IDisposable
{
    internal enum Stage { Planned, BackupVerified, DatabaseDropStarting, DatabaseDropped, StoreRemovalStarting, Completed }
    private readonly FileStream stream;
    private Stage current = Stage.Planned;

    private DemoUninstallJournal(FileStream stream) => this.stream = stream;

    internal static DemoUninstallJournal Create(string journalRoot, string sourceRoot, string backupRoot,
        string credentialFile, string deployment, string instance)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(instance) ||
            string.IsNullOrWhiteSpace(journalRoot) || !Path.IsPathFullyQualified(journalRoot))
            throw new InvalidOperationException("An explicit new Windows journal root is required.");
        journalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(journalRoot));
        foreach (var other in new[] { sourceRoot, backupRoot, credentialFile, deployment })
        {
            if (string.IsNullOrWhiteSpace(other) || !Path.IsPathFullyQualified(other) ||
                DemoRemovalPreview.ContainsPath(other, journalRoot) || DemoRemovalPreview.ContainsPath(journalRoot, other))
                throw new InvalidOperationException("Journal target overlaps or is ambiguous.");
        }
        var parent = Directory.GetParent(journalRoot);
        if (parent is null || !parent.Exists || Directory.Exists(journalRoot) || File.Exists(journalRoot))
            throw new InvalidOperationException("Journal root must be new.");
        DemoRemovalPreview.RejectReparseAncestors(parent.FullName);
        DemoPrivateDirectory.CreateNew(journalRoot);
        var stream = new FileStream(Path.Combine(journalRoot, "uninstall.jsonl"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var journal = new DemoUninstallJournal(stream);
        try
        {
            journal.Write(new { stage = Stage.Planned.ToString(), instance,
                sourceRoot = Path.GetFullPath(sourceRoot), backupRoot = Path.GetFullPath(backupRoot) });
            return journal;
        }
        catch
        {
            journal.Dispose();
            throw;
        }
    }

    internal void Record(Stage stage)
    {
        if (!Enum.IsDefined(stage) || (int)stage != (int)current + 1)
            throw new InvalidOperationException("Invalid progress stage.");
        Write(new { stage = stage.ToString() });
        current = stage;
    }

    internal void RecordTarget(string action, string relativePath)
    {
        if (current != Stage.StoreRemovalStarting ||
            (action != "delete-file" && action != "delete-directory") ||
            string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\') || relativePath.Contains(':') ||
            (relativePath != "." && Array.Exists(relativePath.Split('/'), part => part.Length == 0 || part == "." || part == "..")))
            throw new InvalidOperationException("Invalid journal target.");
        Write(new { stage = current.ToString(), action, relativePath });
    }

    private void Write<T>(T record)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record) + "\n");
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    public void Dispose() => stream.Dispose();
}
