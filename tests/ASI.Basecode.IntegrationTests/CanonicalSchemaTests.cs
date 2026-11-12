using ASI.Basecode.Data;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public class CanonicalSchemaTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture fixture;

    public CanonicalSchemaTests(SqlServerDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task CanonicalSchemaMapsAllBusinessAndLookupTables()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var context = new AsiBasecodeDBContext(options);
        await context.Users.ToListAsync(cancellationToken);
        await context.AccountRequests.ToListAsync(cancellationToken);
        await context.Members.ToListAsync(cancellationToken);
        await context.EventTypes.ToListAsync(cancellationToken);
        await context.Venues.ToListAsync(cancellationToken);
        await context.Events.ToListAsync(cancellationToken);
        await context.Registrations.ToListAsync(cancellationToken);
        await context.AttendanceRecords.ToListAsync(cancellationToken);
        await context.Certificates.ToListAsync(cancellationToken);
        await context.ActivityLogs.ToListAsync(cancellationToken);
        await context.Roles.ToListAsync(cancellationToken);
        await context.AccountRequestStatuses.ToListAsync(cancellationToken);
        await context.MembershipStatuses.ToListAsync(cancellationToken);
        await context.EventStatuses.ToListAsync(cancellationToken);
        await context.RegistrationStatuses.ToListAsync(cancellationToken);
        await context.CheckInMethods.ToListAsync(cancellationToken);
        await context.CertificateStatuses.ToListAsync(cancellationToken);
        await context.ActorTypes.ToListAsync(cancellationToken);

        Assert.Equal(3, await context.Roles.CountAsync(cancellationToken));
        Assert.Equal(3, await context.AccountRequestStatuses.CountAsync(cancellationToken));
        Assert.Equal(2, await context.MembershipStatuses.CountAsync(cancellationToken));
        Assert.Equal(4, await context.EventStatuses.CountAsync(cancellationToken));
        Assert.Equal(4, await context.RegistrationStatuses.CountAsync(cancellationToken));
        Assert.Equal(3, await context.CheckInMethods.CountAsync(cancellationToken));
        Assert.Equal(4, await context.CertificateStatuses.CountAsync(cancellationToken));
        Assert.Equal(2, await context.ActorTypes.CountAsync(cancellationToken));

        Assert.Equal("admin", (await context.Roles.SingleAsync(role => role.RoleId == RoleCode.Admin, cancellationToken)).Code);
        Assert.Equal("officer", (await context.Roles.SingleAsync(role => role.RoleId == RoleCode.Officer, cancellationToken)).Code);
        Assert.Equal("member", (await context.Roles.SingleAsync(role => role.RoleId == RoleCode.Member, cancellationToken)).Code);
        Assert.Equal("pending", (await context.AccountRequestStatuses.SingleAsync(status => status.AccountRequestStatusId == AccountRequestStatusCode.Pending, cancellationToken)).Code);
        Assert.Equal("approved", (await context.AccountRequestStatuses.SingleAsync(status => status.AccountRequestStatusId == AccountRequestStatusCode.Approved, cancellationToken)).Code);
        Assert.Equal("rejected", (await context.AccountRequestStatuses.SingleAsync(status => status.AccountRequestStatusId == AccountRequestStatusCode.Rejected, cancellationToken)).Code);
        Assert.Equal("active", (await context.MembershipStatuses.SingleAsync(status => status.MembershipStatusId == MembershipStatusCode.Active, cancellationToken)).Code);
        Assert.Equal("inactive", (await context.MembershipStatuses.SingleAsync(status => status.MembershipStatusId == MembershipStatusCode.Inactive, cancellationToken)).Code);
        Assert.Equal("draft", (await context.EventStatuses.SingleAsync(status => status.EventStatusId == EventStatusCode.Draft, cancellationToken)).Code);
        Assert.Equal("published", (await context.EventStatuses.SingleAsync(status => status.EventStatusId == EventStatusCode.Published, cancellationToken)).Code);
        Assert.Equal("cancelled", (await context.EventStatuses.SingleAsync(status => status.EventStatusId == EventStatusCode.Cancelled, cancellationToken)).Code);
        Assert.Equal("completed", (await context.EventStatuses.SingleAsync(status => status.EventStatusId == EventStatusCode.Completed, cancellationToken)).Code);
        Assert.Equal("pending", (await context.RegistrationStatuses.SingleAsync(status => status.RegistrationStatusId == RegistrationStatusCode.Pending, cancellationToken)).Code);
        Assert.Equal("confirmed", (await context.RegistrationStatuses.SingleAsync(status => status.RegistrationStatusId == RegistrationStatusCode.Confirmed, cancellationToken)).Code);
        Assert.Equal("waitlisted", (await context.RegistrationStatuses.SingleAsync(status => status.RegistrationStatusId == RegistrationStatusCode.Waitlisted, cancellationToken)).Code);
        Assert.Equal("cancelled", (await context.RegistrationStatuses.SingleAsync(status => status.RegistrationStatusId == RegistrationStatusCode.Cancelled, cancellationToken)).Code);
        Assert.Equal("qr_code", (await context.CheckInMethods.SingleAsync(method => method.CheckInMethodId == CheckInMethodCode.QrCode, cancellationToken)).Code);
        Assert.Equal("manual_search", (await context.CheckInMethods.SingleAsync(method => method.CheckInMethodId == CheckInMethodCode.ManualSearch, cancellationToken)).Code);
        Assert.Equal("student_number", (await context.CheckInMethods.SingleAsync(method => method.CheckInMethodId == CheckInMethodCode.StudentNumber, cancellationToken)).Code);
        Assert.Equal("pending_approval", (await context.CertificateStatuses.SingleAsync(status => status.CertificateStatusId == CertificateStatusCode.PendingApproval, cancellationToken)).Code);
        Assert.Equal("eligible", (await context.CertificateStatuses.SingleAsync(status => status.CertificateStatusId == CertificateStatusCode.Eligible, cancellationToken)).Code);
        Assert.Equal("not_eligible", (await context.CertificateStatuses.SingleAsync(status => status.CertificateStatusId == CertificateStatusCode.NotEligible, cancellationToken)).Code);
        Assert.Equal("issued", (await context.CertificateStatuses.SingleAsync(status => status.CertificateStatusId == CertificateStatusCode.Issued, cancellationToken)).Code);
        Assert.Equal("user", (await context.ActorTypes.SingleAsync(actorType => actorType.ActorTypeId == ActorTypeCode.User, cancellationToken)).Code);
        Assert.Equal("system", (await context.ActorTypes.SingleAsync(actorType => actorType.ActorTypeId == ActorTypeCode.System, cancellationToken)).Code);
    }

    [Fact]
    public async Task CanonicalSchemaCreatesAllBusinessAndLookupTables()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'users', N'account_requests', N'members', N'event_types', N'venues', N'events', N'registrations', N'attendance', N'certificates', N'activity_logs', N'roles', N'account_request_statuses', N'membership_statuses', N'event_statuses', N'registration_statuses', N'check_in_methods', N'certificate_statuses', N'actor_types')",
            connection);
        command.CommandTimeout = 120;
        Assert.Equal(18, (int)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SchemaScriptRejectsUnapprovedDatabase()
    {
        await using var connection = new SqlConnection(fixture.MasterConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "database", "attendta-mvp-schema.sql");
        var script = await File.ReadAllTextAsync(scriptPath, TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<SqlException>(() =>
            SqlServerDatabaseFixture.ExecuteScriptAsync(connection, script, TestContext.Current.CancellationToken));

        Assert.Equal(51000, error.Number);
    }

    [Fact]
    public async Task ActivityLogRejectsDirectUpdatesAndDeletes()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var insert = new SqlCommand(
            "INSERT INTO dbo.activity_logs (actor_type_id, action, entity_type, entity_id, description) VALUES (2, 'test', 'test', 1, N'test'); SELECT CAST(SCOPE_IDENTITY() AS bigint);",
            connection);
        var logId = (long)await insert.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        await using var update = new SqlCommand(
            "UPDATE dbo.activity_logs SET description = N'changed' WHERE log_id = @logId",
            connection);
        update.Parameters.AddWithValue("@logId", logId);
        var updateError = await Assert.ThrowsAsync<SqlException>(() => update.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(51002, updateError.Number);

        await using var delete = new SqlCommand(
            "DELETE FROM dbo.activity_logs WHERE log_id = @logId",
            connection);
        delete.Parameters.AddWithValue("@logId", logId);
        var deleteError = await Assert.ThrowsAsync<SqlException>(() => delete.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(51002, deleteError.Number);

        await using var verify = new SqlCommand(
            "SELECT description FROM dbo.activity_logs WHERE log_id = @logId",
            connection);
        verify.Parameters.AddWithValue("@logId", logId);
        Assert.Equal("test", (string)await verify.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EventTypeSeedProvidesRequiredDescriptions()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var adminSeedPath = Path.Combine(AppContext.BaseDirectory, "database", "seed-local-admin.sql");
        var adminSeed = SqlServerDatabaseFixture.ApplyAdminSeedVariables(
            await File.ReadAllTextAsync(adminSeedPath, TestContext.Current.CancellationToken),
            "AttendTa Schema Test Admin",
            "schema-test-admin@attendta.test",
            new Argon2PasswordHasher().Hash("SchemaTest#1"));
        await SqlServerDatabaseFixture.ExecuteScriptAsync(connection, adminSeed, TestContext.Current.CancellationToken);

        var seedPath = Path.Combine(AppContext.BaseDirectory, "database", "seed-event-types.sql");
        await SqlServerDatabaseFixture.ExecuteScriptAsync(connection, await File.ReadAllTextAsync(seedPath, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        await using var check = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.event_types WHERE description IS NULL OR LEN(LTRIM(RTRIM(description))) = 0",
            connection);
        check.CommandTimeout = 120;
        Assert.Equal(0, (int)await check.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        await using var count = new SqlCommand("SELECT COUNT(*) FROM dbo.event_types", connection);
        count.CommandTimeout = 120;
        Assert.Equal(6, (int)await count.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SchemaEnablesReadCommittedSnapshot()
    {
        await using var connection = new SqlConnection(fixture.MasterConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(
            "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = @databaseName",
            connection);
        command.Parameters.AddWithValue("@databaseName", fixture.DatabaseName);

        Assert.True(Convert.ToBoolean(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task CanonicalSchemaHasRequiredColumnsAndForeignKeys()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id WHERE (t.name = N'event_types' AND c.name = N'description' AND c.is_nullable = 0) OR (t.name = N'certificates' AND c.name = N'issuer_name_snapshot' AND c.is_nullable = 1)",
            connection);
        command.CommandTimeout = 120;
        Assert.Equal(2, (int)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        await using var roleColumn = new SqlCommand(
            "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id WHERE t.name = N'users' AND c.name = N'role_id' AND TYPE_NAME(c.user_type_id) = N'tinyint'",
            connection);
        roleColumn.CommandTimeout = 120;
        Assert.Equal(1, (int)await roleColumn.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        await using var foreignKeys = new SqlCommand(
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN (N'FK_users_roles', N'FK_account_requests_statuses', N'FK_members_statuses', N'FK_events_statuses', N'FK_registrations_statuses', N'FK_attendance_methods', N'FK_certificates_statuses', N'FK_activity_logs_actor_types')",
            connection);
        foreignKeys.CommandTimeout = 120;
        Assert.Equal(8, (int)await foreignKeys.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        await using var businessAndAuditForeignKeys = new SqlCommand(
            """
            WITH expected(parent_table, parent_column, referenced_table, referenced_column) AS (
                SELECT * FROM (VALUES
                    (N'members', N'user_id', N'users', N'user_id'),
                    (N'events', N'type_id', N'event_types', N'type_id'),
                    (N'events', N'venue_id', N'venues', N'venue_id'),
                    (N'registrations', N'event_id', N'events', N'event_id'),
                    (N'attendance', N'registration_id', N'registrations', N'registration_id'),
                    (N'certificates', N'registration_id', N'registrations', N'registration_id'),
                    (N'users', N'created_by', N'users', N'user_id'),
                    (N'users', N'updated_by', N'users', N'user_id'),
                    (N'account_requests', N'reviewed_by', N'users', N'user_id'),
                    (N'account_requests', N'created_user_id', N'users', N'user_id'),
                    (N'members', N'created_by', N'users', N'user_id'),
                    (N'members', N'updated_by', N'users', N'user_id'),
                    (N'event_types', N'created_by', N'users', N'user_id'),
                    (N'event_types', N'updated_by', N'users', N'user_id'),
                    (N'venues', N'created_by', N'users', N'user_id'),
                    (N'venues', N'updated_by', N'users', N'user_id'),
                    (N'events', N'published_by', N'users', N'user_id'),
                    (N'events', N'check_in_opened_by', N'users', N'user_id'),
                    (N'events', N'check_in_closed_by', N'users', N'user_id'),
                    (N'events', N'completed_by', N'users', N'user_id'),
                    (N'events', N'cancelled_by', N'users', N'user_id'),
                    (N'events', N'created_by', N'users', N'user_id'),
                    (N'events', N'updated_by', N'users', N'user_id'),
                    (N'registrations', N'reviewed_by', N'users', N'user_id'),
                    (N'registrations', N'confirmed_by', N'users', N'user_id'),
                    (N'registrations', N'cancelled_by', N'users', N'user_id'),
                    (N'registrations', N'created_by', N'users', N'user_id'),
                    (N'registrations', N'updated_by', N'users', N'user_id'),
                    (N'attendance', N'recorded_by', N'users', N'user_id'),
                    (N'attendance', N'invalidated_by', N'users', N'user_id'),
                    (N'certificates', N'reviewed_by', N'users', N'user_id'),
                    (N'certificates', N'issued_by', N'users', N'user_id'),
                    (N'certificates', N'created_by', N'users', N'user_id'),
                    (N'certificates', N'updated_by', N'users', N'user_id'),
                    (N'activity_logs', N'actor_user_id', N'users', N'user_id')
                ) AS v(parent_table, parent_column, referenced_table, referenced_column)
            ),
            actual(parent_table, parent_column, referenced_table, referenced_column) AS (
                SELECT parent.name, parent_column.name, referenced.name, referenced_column.name
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                JOIN sys.tables parent ON parent.object_id = fk.parent_object_id
                JOIN sys.columns parent_column ON parent_column.object_id = fkc.parent_object_id AND parent_column.column_id = fkc.parent_column_id
                JOIN sys.tables referenced ON referenced.object_id = fk.referenced_object_id
                JOIN sys.columns referenced_column ON referenced_column.object_id = fkc.referenced_object_id AND referenced_column.column_id = fkc.referenced_column_id
            )
            SELECT COUNT(*)
            FROM expected
            WHERE NOT EXISTS (
                SELECT 1 FROM actual
                WHERE actual.parent_table = expected.parent_table
                  AND actual.parent_column = expected.parent_column
                  AND actual.referenced_table = expected.referenced_table
                  AND actual.referenced_column = expected.referenced_column)
            """,
            connection);
        businessAndAuditForeignKeys.CommandTimeout = 120;
        Assert.Equal(0, (int)await businessAndAuditForeignKeys.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanonicalSchemaHasRequiredUniqueKeysAndFilteredIndexes()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var uniqueKeys = new SqlCommand(
            """
            WITH unique_indexes AS (
                SELECT i.object_id,
                       STUFF((SELECT ',' + c.name
                              FROM sys.index_columns ic
                              JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                              ORDER BY ic.key_ordinal
                              FOR XML PATH('')), 1, 1, '') AS columns
                FROM sys.indexes i
                WHERE i.is_unique = 1 AND i.is_primary_key = 0
            )
            SELECT COUNT(*) FROM unique_indexes
            WHERE (object_id = OBJECT_ID(N'dbo.users') AND columns = N'email')
               OR (object_id = OBJECT_ID(N'dbo.members') AND columns = N'student_number')
               OR (object_id = OBJECT_ID(N'dbo.members') AND columns = N'user_id')
               OR (object_id = OBJECT_ID(N'dbo.registrations') AND columns = N'event_id,member_id')
               OR (object_id = OBJECT_ID(N'dbo.attendance') AND columns = N'registration_id')
               OR (object_id = OBJECT_ID(N'dbo.certificates') AND columns = N'registration_id')
            """,
            connection);
        uniqueKeys.CommandTimeout = 120;
        Assert.Equal(6, (int)await uniqueKeys.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        await using var filteredIndexes = new SqlCommand(
            "SELECT COUNT(*) FROM sys.indexes WHERE is_unique = 1 AND has_filter = 1 AND name IN (N'UX_account_requests_pending_email', N'UX_account_requests_pending_student_number', N'UX_registrations_qr_token_not_null', N'UX_certificates_certificate_number_not_null')",
            connection);
        filteredIndexes.CommandTimeout = 120;
        Assert.Equal(4, (int)await filteredIndexes.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }
}
