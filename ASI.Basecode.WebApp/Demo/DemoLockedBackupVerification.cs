using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Creates and verifies a fresh backup while retaining the source SQL ownership lock.</summary>
internal static class DemoLockedBackupVerification
{
    internal static async Task RunAsync(string root, string deployment, string instance, string credentialFile,
        string destination, CancellationToken ct, Func<SqlConnection, Task> whileLocked = null,
        Func<SqlConnection, Task> afterVerified = null, Func<SqlConnection, Task> beforeBackup = null)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        await DemoProtectedBackup.RunAsync(root, deployment, instance, credentialFile, destination, ct,
            async master =>
            {
                await DemoOwnership.RevalidateLockedAsync(master, root, ct);
                await DemoUninstallPreflight.CheckLockedSourceAsync(root, deployment, instance, credentialFile, master, null, ct);
                if (beforeBackup is not null) await beforeBackup(master);
            },
            async master =>
            {
                await DemoRestoreVerification.RunAsync(destination, ct);
                var manifest = await DemoBackupVerification.VerifyFilesAsync(destination, ct);
                if (whileLocked is not null) await whileLocked(master);
                await DemoOwnership.RevalidateLockedAsync(master, root, ct);
                await DemoUninstallPreflight.CheckLockedSourceAsync(root, deployment, instance, credentialFile, master, manifest, ct);
                if (afterVerified is not null) await afterVerified(master);
            });
    }
}
