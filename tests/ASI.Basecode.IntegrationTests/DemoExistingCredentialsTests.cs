using System.Data;
using System.Diagnostics;
using System.Text.Json;
using ASI.Basecode.Services.Security;
using ASI.Basecode.WebApp.Demo;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoExistingCredentialsTests
{
    public static bool Enabled => OperatingSystem.IsWindows() &&
        Environment.GetEnvironmentVariable("ATTENDTA_TEST_DEMO_BACKUP") == "1";

    [Fact(SkipUnless = nameof(Enabled), Skip = "Requires explicitly enabled isolated LocalDB tests.")]
    public async Task GuardRefusesWrongChangedInactiveAndDecidedCredentialsBeforeBackup()
    {
        var instance = "AttendTaDemo_" + Guid.NewGuid().ToString("N");
        var parent = Path.Combine(Path.GetTempPath(), "attendta-credentials-test-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "source");
        var file = Path.Combine(parent, "credentials.json");
        var backup = Path.Combine(parent, "backup");
        var journal = Path.Combine(parent, "journal");
        var ct = TestContext.Current.CancellationToken;
        var created = false;
        Directory.CreateDirectory(parent);
        try
        {
            Assert.Equal(0, await LocalDb("create", instance));
            created = true;
            Assert.Equal(0, await LocalDb("start", instance));
            var credentials = DemoCredentials.CredentialIds.Select((id, i) => (id, password: $"TestPassword{i}"))
                .ToDictionary(x => x.id, x => x.password);
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(credentials), ct);
            await DemoSetup.RunAsync(credentials, root, AppContext.BaseDirectory, ct, instance);
            var preview = await DemoRemovalPreview.RunAsync(root, AppContext.BaseDirectory, instance, file, ct);
            var connectionString = new SqlConnectionStringBuilder(DemoSetup.MasterConnection(instance))
                { InitialCatalog = DemoTarget.Database }.ConnectionString;

            async Task Refused(Dictionary<string, string> input)
            {
                // The journal is independently named because the guard creates it before backup.
                var outputJournal = journal + Guid.NewGuid().ToString("N");
                await Assert.ThrowsAsync<InvalidDataException>(() => DemoGuardedUninstall.RunAsync(true,
                    root, AppContext.BaseDirectory, instance, file, backup, outputJournal, preview, ct,
                    preserveCredentials: input));
                Assert.False(Directory.Exists(backup));
                Assert.True(File.Exists(Path.Combine(root, DemoOwnership.MarkerFile)));
                await using var db = new SqlConnection(connectionString);
                await db.OpenAsync(ct);
                await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.users", db);
                Assert.Equal(41, Convert.ToInt32(await command.ExecuteScalarAsync(ct)));
            }

            var wrong = new Dictionary<string, string>(credentials) { ["admin"] = "WrongPassword99" };
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(wrong), ct);
            await Refused(wrong);
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(credentials), ct);
            var hasher = new Argon2PasswordHasher();
            await Change(connectionString, "UPDATE dbo.users SET password_hash=@hash WHERE email=N'alexmercado@gmail.com'",
                hasher.Hash("ChangedPassword99"), ct);
            await Refused(credentials);
            await Change(connectionString, "UPDATE dbo.users SET password_hash=@hash, is_active=0 WHERE email=N'alexmercado@gmail.com'",
                hasher.Hash(credentials["admin"]), ct);
            await Refused(credentials);
            await Change(connectionString, "UPDATE dbo.users SET is_active=1 WHERE email=N'alexmercado@gmail.com'", null, ct);
            await Change(connectionString, """
                UPDATE dbo.account_requests SET account_request_status_id=3, password_hash=NULL,
                    reviewed_by=(SELECT user_id FROM dbo.users WHERE email=N'alexmercado@gmail.com'),
                    reviewed_at=SYSUTCDATETIME(), review_notes=N'Decided'
                WHERE email=N'applicant@gmail.com'
                """, null, ct);
            await Refused(credentials);
            Assert.Equal(JsonSerializer.Serialize(credentials), await File.ReadAllTextAsync(file, ct));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            if (created)
            {
                Assert.Equal(0, await LocalDb("stop", instance, "-k"));
                Assert.Equal(0, await LocalDb("delete", instance));
            }
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    private static async Task Change(string connectionString, string sql, string? hash, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        if (hash is not null) command.Parameters.Add("@hash", SqlDbType.VarChar, 255).Value = hash;
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
