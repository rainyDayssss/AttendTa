using System;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The live-host safety gate for demo data. Runs during <c>ConfigureServices</c>, before any demo
/// provider is registered, and refuses to start unless the connected database is the demo database
/// (or the one named by <c>DemoData:AllowDatabase</c>) and <c>DemoData:TokenKey</c> is set.
/// Nothing about the refusal appears on screen. (Register M-01.)
/// </summary>
public static class DemoDataStartupGuard
{
    /// <summary>The only database name the switch accepts without an explicit override.</summary>
    public const string DemoDatabaseName = "AttendTa_Demo";

    private const string EnabledKey = DemoDataOptions.SectionName + ":Enabled";
    private const string AllowDatabaseKey = DemoDataOptions.SectionName + ":AllowDatabase";
    private const string TokenKeyKey = DemoDataOptions.SectionName + ":TokenKey";

    /// <summary>
    /// Validates the demo switch. Returns immediately when the switch is off; otherwise logs the
    /// reason to <paramref name="error"/> and throws <see cref="InvalidOperationException"/> when the
    /// host must not start.
    /// </summary>
    public static void Validate(IConfiguration configuration, TextWriter error = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        error ??= Console.Error;

        if (!configuration.GetValue<bool>(EnabledKey))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(configuration[TokenKeyKey]))
        {
            Refuse(error, "DemoData:TokenKey is required when DemoData:Enabled is true.");
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Refuse(error, "ConnectionStrings:DefaultConnection is not configured.");
        }

        string databaseName;
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DB_NAME()";
            databaseName = command.ExecuteScalar() as string;
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or ArgumentException)
        {
            // Never echo the connection string: it may carry credentials.
            Refuse(error, "Demo data could not verify the connected database name.");
            return;
        }

        var allowedDatabase = configuration[AllowDatabaseKey];
        if (string.Equals(databaseName, DemoDatabaseName, StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(allowedDatabase)
                && string.Equals(databaseName, allowedDatabase, StringComparison.Ordinal)))
        {
            return;
        }

        Refuse(error,
            $"Demo data refuses to start against database '{databaseName}'. "
            + $"Connect to '{DemoDatabaseName}' or set DemoData:AllowDatabase to that database name.");
    }

    private static void Refuse(TextWriter error, string reason)
    {
        error.WriteLine(reason);
        throw new InvalidOperationException(reason);
    }
}
