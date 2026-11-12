using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class VenueImagePathMigrationTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task MigrationIsIdempotentAndLongKeysAbortBeforeAnyRowChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var randomPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        int actorId;
        await using (var context = new AsiBasecodeDBContext(options))
        {
            var actor = new User
            {
                Name = "Migration Test Admin",
                UserId = $"migration-{Guid.NewGuid():N}@attendta.test",
                Password = new Argon2PasswordHasher().Hash(randomPassword),
                RoleId = RoleCode.Admin,
                IsActive = true
            };
            context.Users.Add(actor);
            await context.SaveChangesAsync(ct);
            actorId = actor.Id;
        }

        int ordinaryVenueId;
        int longPathVenueId;
        var legacyPath = "venues/legacy-photo.jpg";
        var tooLongPath = "venues/" + new string('x', 482) + ".jpg";
        await using (var connection = new SqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(ct);
            ordinaryVenueId = await InsertVenueAsync(connection, actorId, legacyPath, ct);
            longPathVenueId = await InsertVenueAsync(connection, actorId, tooLongPath, ct);
        }

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "database", "migrate-venue-image-paths.sql");
        var script = await File.ReadAllTextAsync(scriptPath, ct);
        await using (var connection = new SqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(ct);
            var error = await Assert.ThrowsAsync<SqlException>(() => SqlServerDatabaseFixture.ExecuteScriptAsync(connection, script, ct));
            Assert.Contains("one or more prefixed paths exceed", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(legacyPath, await ReadVenuePathAsync(fixture.ConnectionString, ordinaryVenueId, ct));
        Assert.Equal(tooLongPath, await ReadVenuePathAsync(fixture.ConnectionString, longPathVenueId, ct));

        await using (var connection = new SqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM dbo.venues WHERE venue_id = @venueId";
            delete.Parameters.AddWithValue("@venueId", longPathVenueId);
            await delete.ExecuteNonQueryAsync(ct);
        }

        await using (var connection = new SqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await SqlServerDatabaseFixture.ExecuteScriptAsync(connection, script, ct);
            await SqlServerDatabaseFixture.ExecuteScriptAsync(connection, script, ct);
        }

        Assert.Equal("uploads/" + legacyPath, await ReadVenuePathAsync(fixture.ConnectionString, ordinaryVenueId, ct));
    }

    private static async Task<int> InsertVenueAsync(SqlConnection connection, int actorId, string path, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO dbo.venues (room_name, capacity, image_path, created_by) OUTPUT INSERTED.venue_id VALUES (@name, 20, @path, @actorId)";
        command.Parameters.AddWithValue("@name", $"Migration venue {Guid.NewGuid():N}");
        command.Parameters.AddWithValue("@path", path);
        command.Parameters.AddWithValue("@actorId", actorId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<string> ReadVenuePathAsync(string connectionString, int venueId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT image_path FROM dbo.venues WHERE venue_id = @venueId";
        command.Parameters.AddWithValue("@venueId", venueId);
        return (string)(await command.ExecuteScalarAsync(ct))!;
    }
}
