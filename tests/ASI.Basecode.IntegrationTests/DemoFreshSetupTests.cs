using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using ASI.Basecode.WebApp.Demo;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoFreshSetupTests
{
    public static bool LocalDbTestEnabled =>
        OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("ATTENDTA_TEST_DEMO_LOCALDB") == "1";

    [Fact(SkipUnless = nameof(LocalDbTestEnabled),
        Skip = "Requires Windows and SQL Server LocalDB; set ATTENDTA_TEST_DEMO_LOCALDB=1 to run this isolated fresh-setup test.")]
    public async Task FreshSetupProtectsStoreAndRejectsMismatchedOwnership()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var instance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), "attendta-fresh-" + Guid.NewGuid().ToString("N"));
        var prior = Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE");
        var created = false;
        try
        {
            Assert.Equal(0, await LocalDb("create", instance));
            created = true;
            Assert.Equal(0, await LocalDb("start", instance));
            Environment.SetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE", instance);
            var passwords = DemoCredentials.CredentialIds.Select((id, index) => (id, value: $"ExamplePassword{index}"))
                .ToDictionary(x => x.id, x => x.value);
            var ct = TestContext.Current.CancellationToken;
            await DemoSetup.RunAsync(passwords, root, AppContext.BaseDirectory, ct);
            var acl = new DirectoryInfo(root).GetAccessControl();
            Assert.True(acl.AreAccessRulesProtected);
            var allowed = new HashSet<string>
            {
                WindowsIdentity.GetCurrent().User!.Value,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value
            };
            var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
            Assert.NotEmpty(rules);
            foreach (var rule in rules)
            {
                Assert.Contains(rule.IdentityReference.Value, allowed);
                Assert.False(rule.IsInherited);
            }
            await DemoOwnership.InspectAsync(root, AppContext.BaseDirectory, instance, ct);
            await DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, ct);
            foreach (var name in new[] { ".attendta-recovery-incomplete", ".attendta-reinstall-incomplete", ".attendta-setup-incomplete" })
            {
                var incomplete = Path.Combine(root, name);
                await File.WriteAllTextAsync(incomplete, "", ct);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, ct));
                File.Delete(incomplete);
            }
            await DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, ct);
            var markerPath = Path.Combine(root, DemoOwnership.MarkerFile);
            await File.WriteAllTextAsync(markerPath, Guid.NewGuid().ToString("N"), ct);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DemoOwnership.InspectAsync(root, AppContext.BaseDirectory, instance, ct));
            Assert.Contains("markers do not match", error.Message);
            Assert.True(File.Exists(markerPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE", prior);
            SqlConnection.ClearAllPools();
            if (created)
            {
                Assert.Equal(0, await LocalDb("stop", instance, "-k"));
                Assert.Equal(0, await LocalDb("delete", instance));
            }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact(SkipUnless = nameof(LocalDbTestEnabled),
        Skip = "Requires explicitly enabled isolated LocalDB tests.")]
    public async Task InterruptedSetupAfterVersionMarkerCannotLaunch()
    {
        var instance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), "attendta-interrupted-" + Guid.NewGuid().ToString("N"));
        var created = false;
        try
        {
            Assert.Equal(0, await LocalDb("create", instance));
            created = true;
            Assert.Equal(0, await LocalDb("start", instance));
            var credentials = DemoCredentials.CredentialIds.Select((id, index) => (id, value: $"ExamplePassword{index}"))
                .ToDictionary(x => x.id, x => x.value);
            var ct = TestContext.Current.CancellationToken;
            var error = await Assert.ThrowsAsync<DemoSetupException>(() =>
                DemoSetup.RunAsync(credentials, root, AppContext.BaseDirectory, ct, instance,
                    () => throw new IOException("interrupted after version marker")));
            Assert.Equal("install version marking", error.Stage);
            await DemoOwnership.InspectAsync(root, AppContext.BaseDirectory, instance, ct);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, ct));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            if (created)
            {
                Assert.Equal(0, await LocalDb("stop", instance, "-k"));
                Assert.Equal(0, await LocalDb("delete", instance));
            }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task<int> LocalDb(params string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("sqllocaldb")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        return process.ExitCode;
    }
}
