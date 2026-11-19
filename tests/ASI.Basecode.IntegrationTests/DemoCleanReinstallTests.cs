using System.Data;
using System.Diagnostics;
using System.Text.Json;
using ASI.Basecode.Services.Security;
using ASI.Basecode.WebApp.Demo;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoCleanReinstallTests
{
    public static bool Enabled => OperatingSystem.IsWindows() &&
        Environment.GetEnvironmentVariable("ATTENDTA_TEST_DEMO_BACKUP") == "1";

    [Fact]
    public void KnownLegacyHintBlocksWithoutRemovingIt()
    {
        var temp = Path.Combine(Path.GetTempPath(), "attendta-legacy-hint-test-" + Guid.NewGuid().ToString("N"));
        var hint = Path.Combine(temp, "AttendTa", "app");
        Directory.CreateDirectory(temp);
        try
        {
            DemoCleanReinstall.EnsureKnownLegacyPathClear(temp);
            Directory.CreateDirectory(hint);
            Assert.Throws<InvalidOperationException>(() => DemoCleanReinstall.EnsureKnownLegacyPathClear(temp));
            Assert.True(Directory.Exists(hint));
        }
        finally
        {
            if (Directory.Exists(hint)) Directory.Delete(hint);
            if (Directory.Exists(Path.Combine(temp, "AttendTa"))) Directory.Delete(Path.Combine(temp, "AttendTa"));
            Directory.Delete(temp);
        }
    }

    [Fact(SkipUnless = nameof(Enabled), Skip = "Requires explicitly enabled isolated LocalDB tests.")]
    public async Task ReinstallReseedsAndFailureCanRecoverOldData()
    {
        var instance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var recoveryInstance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var parent = Path.Combine(Path.GetTempPath(), "attendta-reinstall-test-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "source");
        var credentialFile = Path.Combine(parent, "credentials.json");
        var backup = Path.Combine(parent, "backup");
        var journal = Path.Combine(parent, "journal");
        var created = false;
        var recoveryCreated = false;
        var ct = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(parent);
        try
        {
            Assert.Equal(0, await LocalDb("create", instance));
            created = true;
            Assert.Equal(0, await LocalDb("start", instance));
            var credentials = DemoCredentials.CredentialIds.Select((id, index) => (id, value: $"ReinstallPassword{index}"))
                .ToDictionary(item => item.id, item => item.value);
            var credentialText = JsonSerializer.Serialize(credentials);
            await File.WriteAllTextAsync(credentialFile, credentialText, ct);
            await DemoSetup.RunAsync(credentials, root, AppContext.BaseDirectory, ct, instance);
            var logRelative = Path.Combine("logs", "202611", "log-20261119_001.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, logRelative))!);
            await File.WriteAllTextAsync(Path.Combine(root, logRelative), "representative application log", ct);
            var db = new SqlConnectionStringBuilder(DemoSetup.MasterConnection(instance)) { InitialCatalog = DemoTarget.Database };
            await DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, ct);
            // Simulate the selected v1.1.0 installation: valid ownership, no new version marker.
            await using (var oldDatabase = new SqlConnection(db.ConnectionString))
            {
                await oldDatabase.OpenAsync(ct);
                await using var removeVersion = new SqlCommand(
                    "EXEC sys.sp_dropextendedproperty @name=N'AttendTaDemoCleanInstallVersion';", oldDatabase);
                await removeVersion.ExecuteNonQueryAsync(ct);
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, ct));
            // The version restriction must not prevent the old installation's removal path.
            await DemoOwnership.InspectAsync(root, AppContext.BaseDirectory, instance, ct);
            await SetFirstTitle(db.ConnectionString, "Old test modification", ct);
            var preview = await DemoRemovalPreview.RunAsync(root, AppContext.BaseDirectory, instance, credentialFile, ct);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DemoCleanReinstall.RunAsync(false,
                root, AppContext.BaseDirectory, instance, credentialFile, backup, journal, preview, ct, () => { }));
            Assert.True(Directory.Exists(root));
            Assert.False(Directory.Exists(backup));
            var refused = await Assert.ThrowsAsync<DemoReinstallException>(() => DemoCleanReinstall.RunAsync(true,
                root, AppContext.BaseDirectory, instance, credentialFile, backup, journal, preview, ct,
                () => throw new InvalidOperationException("legacy hint")));
            Assert.Equal("input validation", refused.Stage);
            Assert.False(Directory.Exists(backup));
            Assert.True(Directory.Exists(root));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var error = await Assert.ThrowsAsync<DemoReinstallException>(() => DemoCleanReinstall.RunAsync(true,
                    root, AppContext.BaseDirectory, instance, credentialFile, backup, journal, preview,
                    cancelled.Token, () => { }));
                Assert.Equal("input validation", error.Stage);
            }
            var missingAssets = await Assert.ThrowsAsync<DemoReinstallException>(() => DemoCleanReinstall.RunAsync(true,
                root, Path.Combine(parent, "missing-deployment"), instance, credentialFile, backup, journal, preview, ct, () => { }));
            Assert.Equal("input validation", missingAssets.Stage);
            await File.WriteAllTextAsync(credentialFile, "{}", ct);
            var invalidCredentials = await Assert.ThrowsAsync<DemoReinstallException>(() => DemoCleanReinstall.RunAsync(true,
                root, AppContext.BaseDirectory, instance, credentialFile, backup, journal, preview, ct, () => { }));
            Assert.Equal("input validation", invalidCredentials.Stage);
            await File.WriteAllTextAsync(credentialFile, credentialText, ct);
            Assert.True(Directory.Exists(root));
            Assert.False(Directory.Exists(backup));
            Assert.False(Directory.Exists(journal));

            await DemoCleanReinstall.RunAsync(true, root, AppContext.BaseDirectory, instance, credentialFile,
                backup, journal, preview, ct, () => { });
            await DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, ct);
            Assert.True(File.Exists(Path.Combine(backup, "COMPLETE")));
            Assert.Equal("representative application log", await File.ReadAllTextAsync(Path.Combine(backup, "store", logRelative), ct));
            await DemoBackupVerification.VerifyFilesAsync(backup, ct);
            Assert.True(File.Exists(Path.Combine(journal, "uninstall.jsonl")));
            Assert.Equal(credentialText, await File.ReadAllTextAsync(credentialFile, ct));
            var assets = await DemoSetup.LoadAssetsAsync(AppContext.BaseDirectory, ct);
            await DemoSeedVerification.VerifyAsync(root, instance, credentials, assets, ct);
            await using (var connection = new SqlConnection(db.ConnectionString))
            {
                await connection.OpenAsync(ct);
                await using var title = new SqlCommand("SELECT TOP(1) title FROM dbo.events ORDER BY event_id", connection);
                Assert.NotEqual("Old test modification", await title.ExecuteScalarAsync(ct));
                var hasher = new Argon2PasswordHasher();
                for (var i = 0; i < DemoCredentials.AccountIds.Length; i++)
                {
                    await using var query = new SqlCommand("SELECT password_hash FROM dbo.users WHERE email=@email", connection);
                    query.Parameters.Add("@email", SqlDbType.NVarChar, 255).Value = DemoAccountSeeder.Accounts[i].Email;
                    var hash = Assert.IsType<string>(await query.ExecuteScalarAsync(ct));
                    Assert.True(hasher.Verify(credentials[DemoCredentials.AccountIds[i]], hash));
                }
            }

            await SetFirstTitle(db.ConnectionString, "Recover this old data", ct);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, logRelative))!);
            await File.WriteAllTextAsync(Path.Combine(root, logRelative), "recovery log", ct);
            var secondPreview = await DemoRemovalPreview.RunAsync(root, AppContext.BaseDirectory, instance, credentialFile, ct);
            var secondBackup = Path.Combine(parent, "failure-backup");
            var failure = await Assert.ThrowsAsync<DemoReinstallException>(() => DemoCleanReinstall.RunAsync(true,
                root, AppContext.BaseDirectory, instance, credentialFile, secondBackup, Path.Combine(parent, "failure-journal"),
                secondPreview, ct, () => { }, null, () => throw new IOException("injected after version marker")));
            Assert.Equal("fresh setup", failure.Stage);
            Assert.True(Directory.Exists(root));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DemoInstallVersion.VerifyAsync(root, AppContext.BaseDirectory, instance, ct));
            Assert.True(File.Exists(Path.Combine(secondBackup, "COMPLETE")));
            Assert.Equal(credentialText, await File.ReadAllTextAsync(credentialFile, ct));
            Assert.Equal(0, await LocalDb("create", recoveryInstance));
            recoveryCreated = true;
            Assert.Equal(0, await LocalDb("start", recoveryInstance));
            var recoveryRoot = Path.Combine(parent, "recovery");
            await DemoBackupRecovery.RunAsync(secondBackup, recoveryRoot, AppContext.BaseDirectory, recoveryInstance, ct);
            await DemoInstallVersion.VerifyAsync(recoveryRoot, AppContext.BaseDirectory, recoveryInstance, ct);
            var recoveryIncomplete = Path.Combine(recoveryRoot, ".attendta-recovery-incomplete");
            await File.WriteAllTextAsync(recoveryIncomplete, "", ct);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DemoInstallVersion.VerifyAsync(recoveryRoot, AppContext.BaseDirectory, recoveryInstance, ct));
            File.Delete(recoveryIncomplete);
            await DemoInstallVersion.VerifyAsync(recoveryRoot, AppContext.BaseDirectory, recoveryInstance, ct);
            Assert.Equal("recovery log", await File.ReadAllTextAsync(Path.Combine(recoveryRoot, logRelative), ct));
            var recovered = new SqlConnectionStringBuilder(DemoSetup.MasterConnection(recoveryInstance)) { InitialCatalog = DemoTarget.Database };
            await using var recoveredDb = new SqlConnection(recovered.ConnectionString);
            await recoveredDb.OpenAsync(ct);
            await using var recoveredTitle = new SqlCommand("SELECT TOP(1) title FROM dbo.events ORDER BY event_id", recoveredDb);
            Assert.Equal("Recover this old data", await recoveredTitle.ExecuteScalarAsync(ct));
            Assert.True(File.Exists(Path.Combine(recoveryRoot, "uploads/events/1a2b3c4d5e6f47a8b9c0d1e2f3a4b5c6.png")));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            if (recoveryCreated)
            {
                Assert.Equal(0, await LocalDb("stop", recoveryInstance, "-k"));
                Assert.Equal(0, await LocalDb("delete", recoveryInstance));
            }
            if (created)
            {
                Assert.Equal(0, await LocalDb("stop", instance, "-k"));
                Assert.Equal(0, await LocalDb("delete", instance));
            }
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    private static async Task SetFirstTitle(string connectionString, string title, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand("""
            UPDATE dbo.events SET title=@title WHERE event_id=(SELECT MIN(event_id) FROM dbo.events);
            """, connection);
        command.Parameters.Add("@title", SqlDbType.NVarChar, 255).Value = title;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> LocalDb(params string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("sqllocaldb")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(output, error);
        return process.ExitCode;
    }
}
