using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Security;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

internal static class DemoSeedVerification
{
    internal static async Task VerifyAsync(string root, string instance,
        IReadOnlyDictionary<string, string> credentials,
        (string Schema, byte[][] Venues, byte[] CebuVenue, byte[][] Covers) assets, CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(DemoSetup.MasterConnection(instance))
            { InitialCatalog = DemoTarget.Database };
        await using var database = new SqlConnection(builder.ConnectionString);
        await database.OpenAsync(ct);
        await using (var counts = new SqlCommand("""
            SELECT (SELECT COUNT(*) FROM dbo.members), (SELECT COUNT(*) FROM dbo.events),
                   (SELECT COUNT(*) FROM dbo.venues);
            """, database))
        await using (var reader = await counts.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct) || reader.GetInt32(0) != 40 ||
                reader.GetInt32(1) != 8 || reader.GetInt32(2) != 5)
                throw new InvalidDataException("Fresh seed counts did not match.");
        }
        var hasher = new Argon2PasswordHasher();
        for (var i = 0; i < DemoCredentials.AccountIds.Length; i++)
        {
            await using var command = new SqlCommand("""
                SELECT password_hash FROM dbo.users WHERE email=@email AND is_active=1;
                """, database);
            command.Parameters.Add("@email", SqlDbType.NVarChar, 255).Value = DemoAccountSeeder.Accounts[i].Email;
            if (await command.ExecuteScalarAsync(ct) is not string hash ||
                !hasher.Verify(credentials[DemoCredentials.AccountIds[i]], hash))
                throw new InvalidDataException("A seeded presenter credential did not verify.");
        }
        await using (var applicant = new SqlCommand("""
            SELECT password_hash FROM dbo.account_requests WHERE email=N'applicant@gmail.com';
            """, database))
            if (await applicant.ExecuteScalarAsync(ct) is not string hash || !hasher.Verify(credentials["applicant"], hash))
                throw new InvalidDataException("The applicant credential did not verify.");

        await VerifyImagesAsync(database, root, "SELECT image_path FROM dbo.venues ORDER BY venue_id",
            assets.Venues.Concat([assets.CebuVenue]).ToArray(), "uploads/venues/", ct);
        await VerifyImagesAsync(database, root, "SELECT image_path FROM dbo.events ORDER BY event_id",
            assets.Covers, "uploads/events/", ct);
    }

    private static async Task VerifyImagesAsync(SqlConnection database, string root, string sql,
        byte[][] expected, string prefix, CancellationToken ct)
    {
        await using var command = new SqlCommand(sql, database);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var index = 0;
        while (await reader.ReadAsync(ct))
        {
            if (index >= expected.Length || reader.IsDBNull(0)) throw new InvalidDataException("Missing seeded banner.");
            var relative = reader.GetString(0);
            if (!relative.StartsWith(prefix, StringComparison.Ordinal) ||
                relative.Substring(prefix.Length).Contains('/') || relative.Contains('\\') || relative.Contains(':'))
                throw new InvalidDataException("Invalid seeded banner path.");
            var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!DemoRemovalPreview.ContainsPath(root, path)) throw new InvalidDataException("Invalid seeded banner path.");
            DemoRemovalPreview.RejectReparseAncestors(path);
            using var file = DemoBackupSourceFile.Open(path);
            if (file.Length != expected[index].Length) throw new InvalidDataException("Seeded banner differs from package.");
            using var copy = new MemoryStream();
            await file.CopyToAsync(copy, ct);
            if (!copy.ToArray().SequenceEqual(expected[index++])) throw new InvalidDataException("Seeded banner differs from package.");
        }
        if (index != expected.Length) throw new InvalidDataException("Seeded banner count differs.");
    }
}
