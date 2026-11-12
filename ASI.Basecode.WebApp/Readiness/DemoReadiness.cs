using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;

namespace ASI.Basecode.WebApp.Readiness;

/// <summary>Operator-only check; run using the same environment and OS identity as the app.</summary>
public static class DemoReadiness
{
    public static Task<int> RunAsync() => RunAsync(
        new ReadinessChecks(CheckSqlAsync, CheckStorageAsync, CheckBrowserAsync, CheckLoginAsync),
        Console.Out, Console.Error);

    internal sealed record ReadinessChecks(Func<Task> Sql, Func<Task> Storage, Func<Task> Browser, Func<Task> Login);

    internal static async Task<int> RunAsync(ReadinessChecks checks, TextWriter output, TextWriter error)
    {
        var failed = false;
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); await output.WriteLineAsync($"PASS {name}"); }
            catch { await error.WriteLineAsync($"FAIL {name}"); failed = true; }
        }

        await Check("runtime SQL identity and audit permissions", checks.Sql);
        await Check("private storage", checks.Storage);
        await Check("Playwright Chromium PDF rendering", checks.Browser);
        await Check("localhost login HTTP 200", checks.Login);
        await output.WriteLineAsync("INFO Offline status not verified; a disconnected-host run requires separate operator evidence.");
        return failed ? 1 : 0;
    }

    private static async Task CheckSqlAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var expectedLogin = Environment.GetEnvironmentVariable("ATTENDTA_RUNTIME_SQL_LOGIN");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(expectedLogin))
            throw new InvalidOperationException();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await CheckSqlConnectionAsync(connection, expectedLogin);
    }

    internal static async Task CheckSqlConnectionAsync(SqlConnection connection, string expectedLogin)
    {
        await using (var databaseCommand = connection.CreateCommand())
        {
            databaseCommand.CommandText = "SELECT DB_NAME()";
            var database = (string)await databaseCommand.ExecuteScalarAsync();
            if (!string.Equals(database, "AttendTa_Demo", StringComparison.Ordinal))
                throw new InvalidOperationException("Wrong runtime database.");
        }
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"SELECT CASE WHEN ORIGINAL_LOGIN() = @login AND USER_NAME() NOT IN (N'dbo', N'guest')
              AND IS_SRVROLEMEMBER('sysadmin') = 0 AND IS_SRVROLEMEMBER('securityadmin') = 0
              AND IS_SRVROLEMEMBER('serveradmin') = 0 AND IS_SRVROLEMEMBER('dbcreator') = 0
              AND IS_SRVROLEMEMBER('processadmin') = 0 AND IS_SRVROLEMEMBER('setupadmin') = 0
              AND IS_SRVROLEMEMBER('bulkadmin') = 0 AND IS_SRVROLEMEMBER('diskadmin') = 0
              AND HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CONTROL') = 0
              AND HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER') = 0
              THEN 1 ELSE 0 END";
            command.Parameters.AddWithValue("@login", expectedLogin);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException();
        }
        await CheckDatabaseRolesAsync(connection);
        await CheckServerPermissionsAsync(connection);
        await CheckAuditAsync(connection);
    }

    // Any database role membership is unsupported; this matches the provisioning script's guard.
    internal static async Task CheckDatabaseRolesAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT CASE WHEN EXISTS (
            SELECT 1 FROM sys.database_role_members WHERE member_principal_id = DATABASE_PRINCIPAL_ID())
            THEN 0 ELSE 1 END";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException();
    }

    // Effective server permissions catch direct and custom-role grants that IS_SRVROLEMEMBER misses.
    internal static async Task CheckServerPermissionsAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COUNT(*) FROM sys.fn_my_permissions(NULL, 'SERVER')
            WHERE permission_name NOT IN (N'CONNECT SQL', N'VIEW ANY DATABASE')";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new InvalidOperationException();
    }

    // Separated from the immutable demo identity guard so disposable test databases can
    // exercise the actual permission query and transaction under restricted SQL users.
    internal static async Task CheckAuditAsync(SqlConnection connection)
    {
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = @"SELECT CASE WHEN
              HAS_PERMS_BY_NAME('dbo.activity_logs', 'OBJECT', 'CONTROL') = 0
              AND HAS_PERMS_BY_NAME('dbo.activity_logs', 'OBJECT', 'ALTER') = 0
              AND HAS_PERMS_BY_NAME('dbo.activity_logs', 'OBJECT', 'SELECT') = 1
              AND HAS_PERMS_BY_NAME('dbo.activity_logs', 'OBJECT', 'INSERT') = 1
              AND HAS_PERMS_BY_NAME('dbo.activity_logs', 'OBJECT', 'UPDATE') = 0
              AND HAS_PERMS_BY_NAME('dbo.activity_logs', 'OBJECT', 'DELETE') = 0
              AND NOT EXISTS (
                  SELECT 1 FROM sys.columns AS column_definition
                  WHERE column_definition.object_id = OBJECT_ID(N'dbo.activity_logs')
                    AND HAS_PERMS_BY_NAME(N'dbo.activity_logs', N'OBJECT', N'UPDATE', column_definition.name, N'COLUMN') = 1)
              THEN 1 ELSE 0 END";
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException();
        }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT dbo.activity_logs (actor_type_id, action, entity_type, entity_id, description) VALUES (2, 'readiness.probe', 'readiness', 0, N'Rolled back readiness probe')";
            await command.ExecuteNonQueryAsync();
        }
        finally { await transaction.RollbackAsync(); }
    }

    private static Task CheckStorageAsync()
    {
        var root = Environment.GetEnvironmentVariable("Common__SetupRoot");
        ValidateStorage(root);
        return Task.CompletedTask;
    }

    public static void ValidateStorage(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) throw new InvalidOperationException();
        foreach (var area in new[] { "keys", "temporary", "uploads/venues", "uploads/events", "certificates" })
            ProbeWritableDirectory(Path.Combine(root, area), create: false);

        // The logger writes beneath logs/<yyyyMM>, so probe that directory and never create a missing logs/ silently.
        var logs = Path.Combine(root, "logs");
        if (!Directory.Exists(logs)) throw new DirectoryNotFoundException();
        ProbeWritableDirectory(Path.Combine(logs, DateTime.Today.ToString("yyyyMM")), create: true);
    }

    private static void ProbeWritableDirectory(string directory, bool create)
    {
        if (!Directory.Exists(directory))
        {
            if (!create) throw new DirectoryNotFoundException();
            Directory.CreateDirectory(directory);
        }
        var probe = Path.Combine(directory, $".readiness-{Guid.NewGuid():N}");
        try { using var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose); file.WriteByte(1); }
        finally { if (File.Exists(probe)) File.Delete(probe); }
    }

    internal static async Task CheckBrowserAsync()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><html><body><p>AttendTa readiness PDF</p></body></html>");
        var pdf = await page.PdfAsync();
        if (pdf is null || pdf.Length < 4 || pdf[0] != (byte)'%' || pdf[1] != (byte)'P' || pdf[2] != (byte)'D' || pdf[3] != (byte)'F')
            throw new InvalidOperationException();
    }

    private static Task CheckLoginAsync() => CheckLoginAsync(Environment.GetEnvironmentVariable("ATTENDTA_READINESS_LOGIN_URL"));

    internal static async Task CheckLoginAsync(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp
            || !(uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "[::1]")
            || uri.AbsolutePath != "/Account/Login" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
            throw new InvalidOperationException();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        using var response = await client.GetAsync(uri);
        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidOperationException();
    }
}
