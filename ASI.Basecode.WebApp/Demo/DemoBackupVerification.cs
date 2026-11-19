using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Checks backup files, not SQL restorability or authorization to remove a source.</summary>
internal static class DemoBackupVerification
{
    internal static async Task<IReadOnlyList<DemoBackupManifest.Entry>> VerifyFilesAsync(string root, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("An absolute Windows backup root is required.");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(root)) throw new InvalidOperationException("Backup is missing.");
        DemoRemovalPreview.RejectReparseAncestors(root);
        CheckAcl(new DirectoryInfo(root).GetAccessControl(), true);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        var count = 0;
        while (pending.Count != 0)
        {
            foreach (var item in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                var attributes = File.GetAttributes(item);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Backup contains a reparse point.");
                CheckAcl((attributes & FileAttributes.Directory) != 0
                    ? new DirectoryInfo(item).GetAccessControl()
                    : new FileInfo(item).GetAccessControl(), false);
                var relative = Path.GetRelativePath(root, item).Replace('\\', '/');
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!relative.Equals("store", StringComparison.Ordinal) &&
                        !relative.StartsWith("store/", StringComparison.Ordinal))
                        throw new InvalidDataException("Unexpected backup directory.");
                    pending.Push(item);
                }
                else if (!paths.Add(relative)) throw new InvalidDataException("Duplicate backup path.");
                if (++count > 10005) throw new InvalidDataException("Backup is too large to inspect.");
            }
        }
        if (!paths.Contains("COMPLETE") || paths.Contains("INCOMPLETE") || !paths.Contains("manifest.json"))
            throw new InvalidDataException("Backup is incomplete.");
        byte[] manifestBytes;
        using (var file = DemoBackupSourceFile.Open(Path.Combine(root, "manifest.json")))
        {
            if (file.Length > 1024 * 1024) throw new InvalidDataException("Manifest too large.");
            using var memory = new MemoryStream();
            await file.CopyToAsync(memory, ct);
            manifestBytes = memory.ToArray();
        }
        using (var complete = DemoBackupSourceFile.Open(Path.Combine(root, "COMPLETE")))
        {
            if (complete.Length > 256) throw new InvalidDataException("Invalid completion marker.");
        }
        var entries = DemoBackupManifest.Parse(manifestBytes);
        var expected = new HashSet<string>(entries.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
        if (!expected.Contains("database.bak") || !expected.Contains("store/" + DemoOwnership.MarkerFile))
            throw new InvalidDataException("Backup is missing required files.");
        if (entries.Any(e => e.Path != "database.bak" && !e.Path.StartsWith("store/", StringComparison.Ordinal)))
            throw new InvalidDataException("Unexpected manifest path.");
        expected.Add("COMPLETE");
        expected.Add("manifest.json");
        if (!paths.SetEquals(expected)) throw new InvalidDataException("Backup file inventory does not match manifest.");
        foreach (var entry in entries)
        {
            var path = Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            DemoRemovalPreview.RejectReparseAncestors(path);
            using var file = DemoBackupSourceFile.Open(path);
            var measured = await DemoBackupManifest.MeasureAsync(entry.Path, file, ct);
            if (measured != entry) throw new InvalidDataException("Backup checksum mismatch.");
        }
        return entries;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void CheckAcl(FileSystemSecurity security, bool root)
    {
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Current user unavailable.");
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var owner = security.GetOwner(typeof(SecurityIdentifier));
        if ((root && (!security.AreAccessRulesProtected || !user.Equals(owner))) ||
            (!root && !user.Equals(owner) && !system.Equals(owner) && !administrators.Equals(owner)))
            throw new InvalidOperationException("Backup ACL is not protected for the current owner.");
        var hasOwnerControl = false;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if ((root && rule.IsInherited) || rule.AccessControlType != AccessControlType.Allow ||
                (!user.Equals(rule.IdentityReference) && !system.Equals(rule.IdentityReference)))
                throw new InvalidOperationException("Backup ACL contains an unexpected rule.");
            if (user.Equals(rule.IdentityReference) && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
                hasOwnerControl = true;
        }
        if (!hasOwnerControl) throw new InvalidOperationException("Backup owner does not have full control.");
    }
}
