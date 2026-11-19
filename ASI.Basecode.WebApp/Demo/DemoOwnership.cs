using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Fresh setup ownership evidence. Verification is read-only, not reset authorization.</summary>
internal static class DemoOwnership
{
    internal const string MarkerFile = ".attendta-demo-owner";
    private const string Property = "AttendTaDemoOwnershipV1";

    internal static async Task MarkNewAsync(SqlConnection database, string root, CancellationToken cancellationToken)
    {
        var marker = Guid.NewGuid().ToString("N");
        await using (var command = new SqlCommand("""
            EXEC sys.sp_addextendedproperty @name=N'AttendTaDemoOwnershipV1', @value=@marker;
            """, database))
        {
            command.Parameters.Add("@marker", SqlDbType.NVarChar, 32).Value = marker;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        // A failed or interrupted write leaves an unpaired marker, which verification refuses.
        await using var file = new FileStream(Path.Combine(root, MarkerFile), FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        var bytes = Encoding.ASCII.GetBytes(marker);
        await file.WriteAsync(bytes, cancellationToken);
        await file.FlushAsync(cancellationToken);
    }

    internal static string ValidateMarker(string text)
    {
        if (text.Length != 32 || !Guid.TryParseExact(text, "N", out _)
            || text != text.ToLowerInvariant())
            throw new InvalidDataException("Invalid ownership marker.");
        return text;
    }

    internal static async Task InspectAsync(string root, string deploymentRoot, string instance,
        CancellationToken cancellationToken, Func<SqlConnection, Task> whileLocked = null)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException("A Windows private setup root is required.");
        var masterConnection = DemoSetup.MasterConnection(instance);
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var deployment = Path.GetFullPath(deploymentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (rootFull.Equals(deployment, StringComparison.OrdinalIgnoreCase)
            || rootFull.StartsWith(deployment + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(rootFull))
            throw new InvalidOperationException("The root is missing or inside the deployment.");

        var markerPath = Path.Combine(rootFull, MarkerFile);
        var data = Path.Combine(rootFull, "database", "AttendTa_Demo.mdf");
        var log = Path.Combine(rootFull, "database", "AttendTa_Demo_log.ldf");
        foreach (var path in new[] { markerPath, data, log })
        {
            if (!File.Exists(path)) throw new InvalidOperationException("Ownership evidence is incomplete.");
            for (FileSystemInfo part = new FileInfo(path); part is not null;
                part = part is FileInfo f ? f.Directory : ((DirectoryInfo)part).Parent)
                if ((part.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Reparse points are not accepted in ownership evidence.");
        }
        if (new FileInfo(markerPath).Length != 32) throw new InvalidDataException("Invalid ownership marker.");
        var marker = ValidateMarker(await File.ReadAllTextAsync(markerPath, cancellationToken));

        await using var master = new SqlConnection(masterConnection);
        await master.OpenAsync(cancellationToken);
        await using (var guard = new SqlCommand("""
            DECLARE @lock int;
            EXEC @lock = sys.sp_getapplock @Resource=N'AttendTaDemoFreshSetup',
                @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0;
            IF @lock < 0 OR ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),0) <> 1
               OR (SELECT COUNT(*) FROM sys.databases WHERE database_id>4) <> 1
               OR DB_ID(N'AttendTa_Demo') IS NULL
                THROW 51220, 'Unsafe or ambiguous demo instance.', 1;
            SELECT physical_name FROM sys.master_files WHERE database_id=DB_ID(N'AttendTa_Demo');
            """, master))
        await using (var reader = await guard.ExecuteReaderAsync(cancellationToken))
        {
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync(cancellationToken))
                actual.Add(LongPath(reader.GetString(0)));
            if (actual.Count != 2 || !actual.SetEquals(new[] { LongPath(data), LongPath(log) }))
                throw new InvalidOperationException("Database files do not belong to the supplied root.");
        }
        var builder = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = DemoTarget.Database };
        // Close our demo-database session before invoking the callback, while retaining
        // the master session and its application lock. This permits an accurate zero-
        // session check and does not disconnect or kill any other user's session.
        await using (var database = new SqlConnection(builder.ConnectionString))
        {
            await database.OpenAsync(cancellationToken);
            await using var read = new SqlCommand("""
                SELECT CONVERT(nvarchar(32), value) FROM sys.extended_properties
                WHERE class=0 AND name=@name
                """, database);
            read.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = Property;
            var databaseMarker = await read.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(marker, databaseMarker, StringComparison.Ordinal))
                throw new InvalidOperationException("Database and private-store markers do not match.");
        }
        if (whileLocked is not null)
            await whileLocked(master);
    }

    private static string LongPath(string path)
    {
        var buffer = new StringBuilder(32768);
        var length = GetLongPathName(Path.GetFullPath(path), buffer, buffer.Capacity);
        if (length == 0 || length >= buffer.Capacity) throw new IOException("Cannot resolve an ownership path.");
        return buffer.ToString();
    }

    internal static async Task RevalidateLockedAsync(SqlConnection master, string root, CancellationToken cancellationToken)
    {
        var data = Path.Combine(root, "database", "AttendTa_Demo.mdf");
        var log = Path.Combine(root, "database", "AttendTa_Demo_log.ldf");
        await using (var command = new SqlCommand("""
            IF APPLOCK_MODE(N'public', N'AttendTaDemoFreshSetup', N'Session') <> N'Exclusive'
               OR ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),0) <> 1
               OR (SELECT COUNT(*) FROM sys.databases WHERE database_id>4) <> 1
               OR DB_ID(N'AttendTa_Demo') IS NULL
                THROW 51221, 'Ownership lock or target changed.', 1;
            SELECT physical_name FROM sys.master_files WHERE database_id=DB_ID(N'AttendTa_Demo');
            """, master))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync(cancellationToken)) actual.Add(LongPath(reader.GetString(0)));
            if (actual.Count != 2 || !actual.SetEquals(new[] { LongPath(data), LongPath(log) }))
                throw new InvalidOperationException("Database file paths changed.");
        }
        using var markerFile = DemoBackupSourceFile.Open(Path.Combine(root, MarkerFile));
        using var markerReader = new StreamReader(markerFile);
        var marker = ValidateMarker(await markerReader.ReadToEndAsync(cancellationToken));
        var builder = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = DemoTarget.Database };
        await using var database = new SqlConnection(builder.ConnectionString);
        await database.OpenAsync(cancellationToken);
        await using var read = new SqlCommand("""
            SELECT CONVERT(nvarchar(32), value) FROM sys.extended_properties
            WHERE class=0 AND name=@name
            """, database);
        read.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = Property;
        if (!string.Equals(await read.ExecuteScalarAsync(cancellationToken) as string, marker, StringComparison.Ordinal))
            throw new InvalidOperationException("Ownership markers changed.");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string shortPath, StringBuilder longPath, int bufferLength);
}
