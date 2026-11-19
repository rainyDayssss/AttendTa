using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

// The version property is meaningful only after the root/database ownership pair is verified.
internal static class DemoInstallVersion
{
    private const string Property = "AttendTaDemoCleanInstallVersion";
    private const string Current = "1.1.1";
    private const string CompletionProperty = "AttendTaDemoSeedVerificationComplete";

    internal static async Task MarkCompleteAsync(SqlConnection database, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "EXEC sys.sp_addextendedproperty @name=@name, @value=@version;", database);
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = CompletionProperty;
        command.Parameters.Add("@version", SqlDbType.NVarChar, 32).Value = Current;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static void EnsureNoIncompleteMarkers(string root)
    {
        foreach (var name in new[] { ".attendta-recovery-incomplete", ".attendta-reinstall-incomplete",
                     ".attendta-setup-incomplete" })
        {
            try
            {
                _ = File.GetAttributes(Path.Combine(root, name));
                throw new InvalidOperationException("An incomplete operation marker exists.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    internal static async Task MarkNewAsync(SqlConnection database, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "EXEC sys.sp_addextendedproperty @name=@name, @value=@version;", database);
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = Property;
        command.Parameters.Add("@version", SqlDbType.NVarChar, 32).Value = Current;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static Task VerifyAsync(string root, string deployment, string instance, CancellationToken cancellationToken)
        => DemoOwnership.InspectAsync(root, deployment, instance, cancellationToken, async master =>
        {
            EnsureNoIncompleteMarkers(root);
            var builder = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = DemoTarget.Database };
            await using var database = new SqlConnection(builder.ConnectionString);
            await database.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("""
                SELECT CONVERT(nvarchar(128), value) FROM sys.extended_properties
                WHERE class=0 AND name=@name
                """, database);
            command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = Property;
            if (!string.Equals(await command.ExecuteScalarAsync(cancellationToken) as string, Current, StringComparison.Ordinal))
                throw new InvalidOperationException("Selected demo is not a completed v1.1.1 clean installation.");
            command.Parameters["@name"].Value = CompletionProperty;
            if (!string.Equals(await command.ExecuteScalarAsync(cancellationToken) as string, Current, StringComparison.Ordinal))
                throw new InvalidOperationException("Seed verification completion evidence is missing.");
            EnsureNoIncompleteMarkers(root);
        });
}
