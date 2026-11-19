using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Security;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>Verify the handover against the live source, never emitting credentials or hashes.</summary>
internal static class DemoExistingCredentials
{
    internal static async Task VerifyAsync(SqlConnection master, IReadOnlyDictionary<string, string> credentials,
        CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = DemoTarget.Database };
        await using var database = new SqlConnection(builder.ConnectionString);
        await database.OpenAsync(ct);
        var hasher = new Argon2PasswordHasher();
        for (var i = 0; i < DemoCredentials.AccountIds.Length; i++)
        {
            await using var query = new SqlCommand(
                "SELECT password_hash, is_active FROM dbo.users WHERE email=@email;", database);
            query.Parameters.Add("@email", SqlDbType.NVarChar, 255).Value = DemoAccountSeeder.Accounts[i].Email;
            await using var reader = await query.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || reader.IsDBNull(0) || !reader.GetBoolean(1) ||
                !credentials.TryGetValue(DemoCredentials.AccountIds[i], out var password) ||
                !hasher.Verify(password, reader.GetString(0)) || await reader.ReadAsync(ct))
                throw new InvalidDataException("Existing presenter credentials cannot be verified.");
        }

        // A decided request has its hash cleared. Refuse rather than silently inventing
        // a credential or assuming that a later account has the original password.
        await using var applicant = new SqlCommand("""
            SELECT password_hash, account_request_status_id FROM dbo.account_requests
            WHERE email=N'applicant@gmail.com';
            """, database);
        await using var requests = await applicant.ExecuteReaderAsync(ct);
        if (!await requests.ReadAsync(ct) || requests.IsDBNull(0) || requests.GetByte(1) != 1 ||
            !credentials.TryGetValue("applicant", out var applicantPassword) ||
            !hasher.Verify(applicantPassword, requests.GetString(0)) || await requests.ReadAsync(ct))
            throw new InvalidDataException("The original pending applicant credential cannot be verified.");
    }
}
