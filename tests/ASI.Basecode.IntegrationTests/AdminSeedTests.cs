using ASI.Basecode.Services.Security;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Exercises the one-time administrator seed on its own fresh database so the
/// "exactly one active administrator and one system audit row" rule can be asserted.
/// </summary>
public class AdminSeedTests : IClassFixture<SqlServerDatabaseFixture>
{
    private const string AdminName = "AttendTa Seed Admin";
    private const string AdminEmail = "seed-admin@attendta.test";
    private const string AdminPassword = "SeedAdmin#2026";

    private readonly SqlServerDatabaseFixture fixture;

    public AdminSeedTests(SqlServerDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task SeedCreatesOneActiveAdminAndOneSystemAuditRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var passwordHash = new Argon2PasswordHasher().Hash(AdminPassword);
        var script = SqlServerDatabaseFixture.ApplyAdminSeedVariables(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database", "seed-local-admin.sql"), cancellationToken),
            AdminName,
            AdminEmail,
            passwordHash);

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await SqlServerDatabaseFixture.ExecuteScriptAsync(connection, script, cancellationToken);

        await using (var admin = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.users WHERE [email] = @email AND [role_id] = 1 AND [is_active] = 1",
            connection))
        {
            admin.Parameters.AddWithValue("@email", AdminEmail);
            Assert.Equal(1, (int)await admin.ExecuteScalarAsync(cancellationToken));
        }

        await using (var activeAdmins = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.users WHERE [role_id] = 1 AND [is_active] = 1",
            connection))
        {
            Assert.Equal(1, (int)await activeAdmins.ExecuteScalarAsync(cancellationToken));
        }

        long logId;
        await using (var audit = new SqlCommand(
            "SELECT TOP (1) [log_id] FROM dbo.activity_logs WHERE [actor_type_id] = 2 AND [entity_type] = 'users' AND [action] = 'user.provisioned'",
            connection))
        {
            var value = await audit.ExecuteScalarAsync(cancellationToken);
            Assert.NotNull(value);
            logId = (long)value;
        }

        await using (var auditCount = new SqlCommand("SELECT COUNT(*) FROM dbo.activity_logs", connection))
        {
            Assert.Equal(1, (int)await auditCount.ExecuteScalarAsync(cancellationToken));
        }

        await using (var auditContent = new SqlCommand(
            "SELECT CONCAT([description], '|', ISNULL([reason], ''), '|', ISNULL([old_values], ''), '|', ISNULL([new_values], '')) FROM dbo.activity_logs WHERE [log_id] = @logId",
            connection))
        {
            auditContent.Parameters.AddWithValue("@logId", logId);
            var content = (string)await auditContent.ExecuteScalarAsync(cancellationToken);
            Assert.DoesNotContain(passwordHash, content, StringComparison.Ordinal);
            Assert.DoesNotContain(AdminPassword, content, StringComparison.Ordinal);
        }

        // Re-running the seed must not create a second administrator or a second audit row.
        await SqlServerDatabaseFixture.ExecuteScriptAsync(connection, script, cancellationToken);

        await using (var adminsAfterRerun = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.users WHERE [role_id] = 1 AND [is_active] = 1",
            connection))
        {
            Assert.Equal(1, (int)await adminsAfterRerun.ExecuteScalarAsync(cancellationToken));
        }

        await using (var auditAfterRerun = new SqlCommand("SELECT COUNT(*) FROM dbo.activity_logs", connection))
        {
            Assert.Equal(1, (int)await auditAfterRerun.ExecuteScalarAsync(cancellationToken));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("plaintext-password")]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c2FsdHNhbHRzYWx0c2E$aGFzaGhhc2hoYXNoaGFzaGhhc2hoYXNoaGFzaGhhc2g")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task SeedRejectsMissingOrInvalidHash(string passwordHash)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var script = SqlServerDatabaseFixture.ApplyAdminSeedVariables(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database", "seed-local-admin.sql"), cancellationToken),
            AdminName,
            AdminEmail,
            passwordHash);

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var error = await Assert.ThrowsAsync<SqlException>(() =>
            SqlServerDatabaseFixture.ExecuteScriptAsync(connection, script, cancellationToken));
        Assert.Equal(51018, error.Number);
    }
}
