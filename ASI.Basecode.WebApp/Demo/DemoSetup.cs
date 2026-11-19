using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

internal static class DemoSetup
{
    internal static string MasterConnection(string instanceName)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\" + (string.IsNullOrWhiteSpace(instanceName) ? "AttendTaDemo" : instanceName),
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Pooling = false
        };
        DemoTarget.ValidateMasterConnection(builder.ConnectionString);
        return builder.ConnectionString;
    }
    private static readonly string[] Areas = ["keys", "logs", "temporary", "uploads/venues", "uploads/events", "certificates", "database"];

    internal static string ValidateRoot(string root, string deploymentRoot)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("Setup root must be an absolute new directory.");
        var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var deployment = Path.GetFullPath(deploymentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(full, deployment, comparison)
            || full.StartsWith(deployment + Path.DirectorySeparatorChar, comparison)
            || Directory.Exists(full) || File.Exists(full))
            throw new InvalidOperationException("Setup root must be new and outside the deployment tree.");
        var parent = Directory.GetParent(full);
        if (parent is null || !parent.Exists)
            throw new InvalidOperationException("Setup root parent must already exist.");
        for (var part = parent; part is not null; part = part.Parent)
            if ((part.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Setup root parent cannot contain symbolic links.");
        return full;
    }

    internal static async Task RunAsync(IReadOnlyDictionary<string, string> credentials, string root,
        string deploymentRoot, CancellationToken cancellationToken, string instanceName = null,
        Func<Task> afterVersionMarkerForTest = null)
    {
        var stage = "input validation";
        var storeCreated = false;
        try
        {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Fresh setup requires Windows LocalDB.");
        // Repeat validation at the setup boundary, before any database or file writes.
        credentials = DemoCredentials.Parse(System.Text.Json.JsonSerializer.Serialize(credentials));
        root = ValidateRoot(root, deploymentRoot);
        var masterConnection = MasterConnection(instanceName ?? Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE"));
        stage = "bundled asset validation";
        var (schema, venues, cebuVenue, covers) = await LoadAssetsAsync(deploymentRoot, cancellationToken);

        stage = "LocalDB preflight";
        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync(cancellationToken);
        // Session lock coordinates simultaneous invocations. Never operate on an instance
        // with any user database; the operator must separately confirm the instance is dedicated.
        await using (var preflight = new SqlCommand("""
            DECLARE @lock int;
            EXEC @lock = sp_getapplock @Resource = 'AttendTaDemoFreshSetup', @LockMode = 'Exclusive',
                @LockOwner = 'Session', @LockTimeout = 0;
            IF @lock < 0 THROW 51210, 'Another demo setup is running.', 1;
            IF ISNULL(IS_SRVROLEMEMBER(N'sysadmin'), 0) <> 1
               OR EXISTS (SELECT 1 FROM sys.databases WHERE database_id > 4)
                THROW 51211, 'LocalDB instance must be dedicated and contain no user databases.', 1;
            """, master))
            await preflight.ExecuteNonQueryAsync(cancellationToken);

        // No overwrite and no automatic cleanup: on failure an operator must inspect the partial
        // database/store and obtain separate authorization before any removal or reset.
        root = ValidateRoot(root, deploymentRoot);
        stage = "private store creation";
        DemoPrivateDirectory.CreateNew(root);
        storeCreated = true;
        foreach (var area in Areas) Directory.CreateDirectory(Path.Combine(root, area));
        await WriteNewAsync(Path.Combine(root, "uploads/venues/5e6f7a8b9c0d41e2f3a4b5c6d7e8f9a0.png"), venues[0], cancellationToken);
        await WriteNewAsync(Path.Combine(root, "uploads/venues/aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa.png"), venues[1], cancellationToken);
        await WriteNewAsync(Path.Combine(root, "uploads/venues/bbbbbbbbbbbb4bbb8bbbbbbbbbbbbbbb.png"), venues[2], cancellationToken);
        await WriteNewAsync(Path.Combine(root, "uploads/venues/cccccccccccc4ccc8ccccccccccccccc.png"), venues[3], cancellationToken);
        await WriteNewAsync(Path.Combine(root, "uploads/venues/dddddddddddd4ddd8ddddddddddddddd.png"), cebuVenue, cancellationToken);
        string[] eventKeys = [
            "1a2b3c4d5e6f47a8b9c0d1e2f3a4b5c6", "2b3c4d5e6f7a48b9c0d1e2f3a4b5c6d7",
            "3c4d5e6f7a8b49c0d1e2f3a4b5c6d7e8", "4d4d4d4d4d4d44d4a4d4d4d4d4d4d4d4",
            "5e5e5e5e5e5e45e5a5e5e5e5e5e5e5e5", "6f6f6f6f6f6f46f6a6f6f6f6f6f6f6f6",
            "7a7a7a7a7a7a47a7a7a7a7a7a7a7a7a7", "8b8b8b8b8b8b48b8a8b8b8b8b8b8b8b8"];
        for (var i = 0; i < eventKeys.Length; i++)
            await WriteNewAsync(Path.Combine(root, "uploads/events", eventKeys[i] + ".png"), covers[i], cancellationToken);

        stage = "database creation";
        await using (var create = new SqlCommand("""
            DECLARE @sql nvarchar(max) = N'CREATE DATABASE [AttendTa_Demo] ON PRIMARY '
                + N'(NAME = N''AttendTa_Demo'', FILENAME = N''' + REPLACE(@data, '''', '''''') + N''') '
                + N'LOG ON (NAME = N''AttendTa_Demo_log'', FILENAME = N''' + REPLACE(@log, '''', '''''') + N''')';
            EXEC (@sql);
            """, master))
        {
            create.CommandTimeout = 120;
            create.Parameters.Add("@data", System.Data.SqlDbType.NVarChar, 260).Value = Path.Combine(root, "database", "AttendTa_Demo.mdf");
            create.Parameters.Add("@log", System.Data.SqlDbType.NVarChar, 260).Value = Path.Combine(root, "database", "AttendTa_Demo_log.ldf");
            await create.ExecuteNonQueryAsync(cancellationToken);
        }
        var builder = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = DemoTarget.Database };
        stage = "schema installation";
        await using var database = new SqlConnection(builder.ConnectionString);
        await database.OpenAsync(cancellationToken);
        foreach (var batch in Regex.Split(schema, @"(?im)^\s*GO\s*(?:\r?\n|$)").Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            await using var command = new SqlCommand(batch, database) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        stage = "demo seeding";
        await DemoAccountSeeder.SeedAsync(database, credentials, cancellationToken);
        stage = "ownership marking";
        await DemoOwnership.MarkNewAsync(database, root, cancellationToken);
        stage = "install version marking";
        await DemoInstallVersion.MarkNewAsync(database, cancellationToken);
        if (afterVersionMarkerForTest is not null) await afterVersionMarkerForTest();
        stage = "seed verification";
        await DemoSeedVerification.VerifyAsync(root, instanceName ?? Environment.GetEnvironmentVariable("ATTENDTA_DEMO_LOCALDB_INSTANCE"),
            credentials, (schema, venues, cebuVenue, covers), cancellationToken);
        stage = "completion marking";
        await DemoInstallVersion.MarkCompleteAsync(database, cancellationToken);
        }
        catch (Exception)
        {
            throw new DemoSetupException(stage, storeCreated);
        }
    }

    internal static async Task<(string Schema, byte[][] Venues, byte[] CebuVenue, byte[][] Covers)> LoadAssetsAsync(
        string deploymentRoot, CancellationToken cancellationToken)
    {
        var assetDirectory = Path.Combine(deploymentRoot, "demo");
        var schema = await File.ReadAllTextAsync(Path.Combine(assetDirectory, "attendta-mvp-schema.sql"), cancellationToken);
        if (string.IsNullOrWhiteSpace(schema)) throw new InvalidDataException("Bundled schema is empty.");
        var venues = new byte[4][];
        for (var i = 0; i < venues.Length; i++)
            venues[i] = await File.ReadAllBytesAsync(Path.Combine(assetDirectory, $"venue-{i + 1}.png"), cancellationToken);
        var cebuVenue = await File.ReadAllBytesAsync(Path.Combine(assetDirectory, "venue-cebu.png"), cancellationToken);
        var covers = new byte[8][];
        for (var i = 0; i < covers.Length; i++)
            covers[i] = await File.ReadAllBytesAsync(Path.Combine(assetDirectory, $"event-{i + 1}.png"), cancellationToken);
        foreach (var image in venues.Concat([cebuVenue]).Concat(covers)) DemoImage.ValidatePng(image);
        return (schema, venues, cebuVenue, covers);
    }

    private static async Task WriteNewAsync(string path, byte[] content, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await file.WriteAsync(content, cancellationToken);
    }
}

internal sealed class DemoSetupException(string stage, bool storeCreated) : Exception
{
    internal string Stage { get; } = stage;
    internal bool StoreCreated { get; } = storeCreated;
}
