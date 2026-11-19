using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>A read-only snapshot, never authority to delete or overwrite anything.</summary>
internal static class DemoRemovalPreview
{
    private static readonly HashSet<string> RootEntries = new(StringComparer.OrdinalIgnoreCase)
        { DemoOwnership.MarkerFile, "keys", "logs", "temporary", "uploads", "certificates", "database" };

    internal static bool ContainsPath(string parent, string child)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        child = Path.TrimEndingDirectorySeparator(Path.GetFullPath(child));
        return child.Equals(parent, comparison) ||
            child.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar, comparison);
    }

    internal static void RejectReparseAncestors(string path)
    {
        FileSystemInfo part = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        for (; part is not null; part = part is FileInfo file ? file.Directory : ((DirectoryInfo)part).Parent)
            if ((part.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Reparse points are not accepted.");
    }

    internal static IReadOnlyList<string> Inventory(string root)
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count != 0)
        {
            var directory = pending.Pop();
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                if (directory == root && !RootEntries.Contains(Path.GetFileName(path)))
                    throw new InvalidOperationException("Unknown top-level store entry.");
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Reparse points are not accepted.");
                result.Add(path);
                if (result.Count > 10000)
                    throw new InvalidOperationException("Store inventory is too large to review.");
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(path);
            }
        }
        return result.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static async Task<IReadOnlyList<string>> RunAsync(string root, string deployment, string instance,
        string credentialFile, CancellationToken cancellationToken)
    {
        await ValidateInputsAsync(root, deployment, instance, credentialFile, cancellationToken);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        IReadOnlyList<string> inventory = null;
        await DemoOwnership.InspectAsync(root, deployment, instance, cancellationToken, _ =>
        {
            inventory = Inventory(root);
            return Task.CompletedTask;
        });
        return inventory;
    }

    internal static async Task ValidateInputsAsync(string root, string deployment, string instance,
        string credentialFile, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)
            || string.IsNullOrWhiteSpace(instance) || string.IsNullOrWhiteSpace(credentialFile)
            || !Path.IsPathFullyQualified(credentialFile))
            throw new InvalidOperationException("Explicit Windows targets and credential file are required.");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        deployment = Path.TrimEndingDirectorySeparator(Path.GetFullPath(deployment));
        credentialFile = Path.GetFullPath(credentialFile);
        if (!Directory.Exists(root) || !File.Exists(credentialFile)
            || ContainsPath(root, deployment) || ContainsPath(deployment, root)
            || ContainsPath(root, credentialFile) || ContainsPath(deployment, credentialFile))
            throw new InvalidOperationException("Targets overlap or are missing.");
        RejectReparseAncestors(root);
        RejectReparseAncestors(credentialFile);
        // Bound secret input and do not print it or propagate parser details.
        using (var stream = DemoBackupSourceFile.Open(credentialFile))
        using (var reader = new StreamReader(stream))
        {
            var input = new char[16385];
            try
            {
                var count = 0;
                int read;
                while (count < input.Length && (read = await reader.ReadAsync(input.AsMemory(count), cancellationToken)) > 0)
                    count += read;
                if (count == input.Length) throw new InvalidOperationException("Credential file is too large.");
                _ = DemoCredentials.Parse(new string(input, 0, count));
            }
            finally { Array.Clear(input); }
        }
    }
}
