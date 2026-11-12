using Microsoft.Data.SqlClient;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private string masterConnectionString;

    public string DatabaseName { get; } = $"AttendTa_IT_{Guid.NewGuid():N}";
    public string ConnectionString { get; private set; }
    public string MasterConnectionString => masterConnectionString;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuredConnectionString = Environment.GetEnvironmentVariable("ATTENDTA_TEST_SQL");
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException("Set ATTENDTA_TEST_SQL to a local SQL Server master connection for integration tests.");
        }

        var builder = new SqlConnectionStringBuilder(configuredConnectionString);
        if (!string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("ATTENDTA_TEST_SQL must connect to master; the fixture creates and drops only its generated AttendTa_IT_<guid> database.");
        }

        var sqlServerPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
        if (!string.IsNullOrEmpty(sqlServerPassword))
        {
            builder.Password = sqlServerPassword;
        }

        if (!IsLocalSqlServer(builder.DataSource))
        {
            throw new InvalidOperationException("Integration tests may create only AttendTa_IT_<guid> databases on a local SQL Server instance.");
        }

        if (string.IsNullOrWhiteSpace(builder.UserID) && !builder.IntegratedSecurity)
        {
            throw new InvalidOperationException("ATTENDTA_TEST_SQL must use SQL authentication or integrated security to create its isolated test database.");
        }

        builder.InitialCatalog = "master";
        masterConnectionString = builder.ConnectionString;
        ConnectionString = new SqlConnectionStringBuilder(masterConnectionString)
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;

        await using (var masterConnection = new SqlConnection(masterConnectionString))
        {
            await masterConnection.OpenAsync(cancellationToken);
            await using (var verifyPermissions = new SqlCommand("SELECT IS_SRVROLEMEMBER(N'sysadmin')", masterConnection))
            {
                if (Convert.ToInt32(await verifyPermissions.ExecuteScalarAsync(cancellationToken)) != 1)
                {
                    throw new InvalidOperationException("ATTENDTA_TEST_SQL must use a SQL Server principal allowed to create and drop isolated integration databases.");
                }
            }

            await using var createDatabase = new SqlCommand($"CREATE DATABASE [{DatabaseName}]", masterConnection);
            createDatabase.CommandTimeout = 120;
            await createDatabase.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "database", "attendta-mvp-schema.sql");
            await using var databaseConnection = new SqlConnection(ConnectionString);
            await databaseConnection.OpenAsync(cancellationToken);
            await ExecuteScriptAsync(databaseConnection, await File.ReadAllTextAsync(schemaPath, cancellationToken), cancellationToken);
        }
        catch
        {
            await DropDatabaseAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DropDatabaseAsync();
    }

    public static async Task ExecuteScriptAsync(SqlConnection connection, string script, CancellationToken cancellationToken)
    {
        foreach (var batch in SplitBatches(script))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Applies the sqlcmd variables the admin seed script expects.
    /// </summary>
    public static string ApplyAdminSeedVariables(string script, string name, string email, string passwordHash)
    {
        return script
            .Replace("$(AdminName)", name, StringComparison.Ordinal)
            .Replace("$(AdminEmail)", email, StringComparison.Ordinal)
            .Replace("$(AdminPasswordHash)", passwordHash, StringComparison.Ordinal);
    }

    private async Task DropDatabaseAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        if (string.IsNullOrEmpty(masterConnectionString)
            || !DatabaseName.StartsWith("AttendTa_IT_", StringComparison.Ordinal)
            || !Guid.TryParseExact(DatabaseName["AttendTa_IT_".Length..], "N", out _))
        {
            return;
        }

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var verifyPermissions = new SqlCommand("SELECT IS_SRVROLEMEMBER(N'sysadmin')", connection);
        if (Convert.ToInt32(await verifyPermissions.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            return;
        }

        await using var command = new SqlCommand(
            $"IF DB_ID(N'{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END",
            connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<string> SplitBatches(string script)
    {
        using var reader = new StringReader(script);
        var batch = new List<string>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                var commandText = string.Join(Environment.NewLine, batch);
                if (!string.IsNullOrWhiteSpace(commandText))
                {
                    yield return commandText;
                }

                batch.Clear();
                continue;
            }

            batch.Add(line);
        }

        var finalBatch = string.Join(Environment.NewLine, batch);
        if (!string.IsNullOrWhiteSpace(finalBatch))
        {
            yield return finalBatch;
        }
    }

    private static bool IsLocalSqlServer(string dataSource)
    {
        return dataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)
            || dataSource.Equals(".", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith(".\\", StringComparison.OrdinalIgnoreCase)
            || dataSource.Equals("(local)", StringComparison.OrdinalIgnoreCase)
            || dataSource.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase)
            || dataSource.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase)
            || dataSource.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("::1,", StringComparison.OrdinalIgnoreCase);
    }
}
