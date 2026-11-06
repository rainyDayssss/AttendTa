using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class VenuePhotoFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private const string AdminEmail = "venue-photo-admin@attendta.test";
    private const string OfficerEmail = "venue-photo-officer@attendta.test";
    private const string MemberEmail = "venue-photo-member@attendta.test";
    private static readonly byte[] OldPhoto = JpegBytes(64, 0x21);
    private static readonly byte[] ReplacementPhoto = JpegBytes(96, 0x42);

    [Fact]
    public async Task ManagementCanReadPhotosAndFailedReplacementKeepsOldFileAndPath()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var hasher = new Argon2PasswordHasher();
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        int adminId;
        int officerId;
        int memberId;
        await using (var context = new AsiBasecodeDBContext(options))
        {
            var users = new[]
            {
                CreateUser(AdminEmail, RoleCode.Admin, hasher, password),
                CreateUser(OfficerEmail, RoleCode.Officer, hasher, password),
                CreateUser(MemberEmail, RoleCode.Member, hasher, password)
            };
            context.Users.AddRange(users);
            await context.SaveChangesAsync(ct);
            adminId = users[0].Id;
            officerId = users[1].Id;
            memberId = users[2].Id;
        }

        var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        string oldPath;
        string eventPath;
        string legacyPath;
        int venueId;
        using (var scope = factory.Services.CreateScope())
        {
            var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
            await using var image = new MemoryStream(OldPhoto);
            oldPath = await files.SaveAsync(PrivateFileArea.VenueImages, image, ".jpg", ct);
            await using var eventImage = new MemoryStream(ReplacementPhoto);
            eventPath = await files.SaveAsync(PrivateFileArea.EventImages, eventImage, ".jpg", ct);
            legacyPath = oldPath["uploads/".Length..];
        }

        await using (var context = new AsiBasecodeDBContext(options))
        {
            var venue = new Venue
            {
                RoomName = $"Test Venue {Guid.NewGuid():N}",
                Capacity = 30,
                IsActive = true,
                ImagePath = legacyPath,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow
            };
            context.Venues.Add(venue);
            await context.SaveChangesAsync(ct);
            venueId = venue.VenueId;
        }

        try
        {
            var migrationScriptPath = Path.Combine(AppContext.BaseDirectory, "database", "migrate-venue-image-paths.sql");
            var migrationScript = await File.ReadAllTextAsync(migrationScriptPath, ct);
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(fixture.ConnectionString))
            {
                await connection.OpenAsync(ct);
                await SqlServerDatabaseFixture.ExecuteScriptAsync(connection, migrationScript, ct);
                await SqlServerDatabaseFixture.ExecuteScriptAsync(connection, migrationScript, ct);
            }

            await using (var context = new AsiBasecodeDBContext(options))
            {
                var venue = await context.Venues.SingleAsync(item => item.VenueId == venueId, ct);
                Assert.Equal(oldPath, venue.ImagePath);
            }

            var anonymous = await officerClient.GetAsync($"/Venue/Photo?id={venueId}", ct);
            Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
            Assert.Contains("/Account/Login", anonymous.Headers.Location?.ToString(), StringComparison.Ordinal);

            Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(officerClient, OfficerEmail, password, ct)).StatusCode);
            var photo = await officerClient.GetAsync($"/Venue/Photo?id={venueId}", ct);
            Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
            Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
            Assert.Equal(OldPhoto, await photo.Content.ReadAsByteArrayAsync(ct));

            await using (var context = new AsiBasecodeDBContext(options))
            {
                var venue = await context.Venues.SingleAsync(item => item.VenueId == venueId, ct);
                venue.ImagePath = eventPath;
                await context.SaveChangesAsync(ct);
            }
            var eventPhoto = await officerClient.GetAsync($"/Venue/Photo?id={venueId}", ct);
            Assert.Equal(HttpStatusCode.NotFound, eventPhoto.StatusCode);
            Assert.DoesNotContain(eventPath, await eventPhoto.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

            await using (var context = new AsiBasecodeDBContext(options))
            {
                var venue = await context.Venues.SingleAsync(item => item.VenueId == venueId, ct);
                venue.ImagePath = "uploads/venues/../private.jpg";
                await context.SaveChangesAsync(ct);
            }
            var unsafePhoto = await officerClient.GetAsync($"/Venue/Photo?id={venueId}", ct);
            Assert.Equal(HttpStatusCode.NotFound, unsafePhoto.StatusCode);
            Assert.DoesNotContain("private.jpg", await unsafePhoto.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
            await using (var context = new AsiBasecodeDBContext(options))
            {
                var venue = await context.Venues.SingleAsync(item => item.VenueId == venueId, ct);
                venue.ImagePath = "uploads/venues/bad\0.jpg";
                await context.SaveChangesAsync(ct);
            }
            var malformedPhoto = await officerClient.GetAsync($"/Venue/Photo?id={venueId}", ct);
            Assert.Equal(HttpStatusCode.NotFound, malformedPhoto.StatusCode);
            await using (var context = new AsiBasecodeDBContext(options))
            {
                var venue = await context.Venues.SingleAsync(item => item.VenueId == venueId, ct);
                venue.ImagePath = oldPath;
                await context.SaveChangesAsync(ct);
            }

            Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(memberClient, MemberEmail, password, ct)).StatusCode);
            var denied = await memberClient.GetAsync($"/Venue/Photo?id={venueId}", ct);
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/Account/AccessDenied", denied.Headers.Location?.ToString(), StringComparison.Ordinal);

            Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(adminClient, AdminEmail, password, ct)).StatusCode);
            var managementProfile = await adminClient.GetAsync("/Profile", ct);
            Assert.Equal(HttpStatusCode.Redirect, managementProfile.StatusCode);
            Assert.Contains("/Account/AccessDenied", managementProfile.Headers.Location?.ToString(), StringComparison.Ordinal);
            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(fixture.ConnectionString))
            {
                await connection.OpenAsync(ct);
                await using var trigger = connection.CreateCommand();
                trigger.CommandText = "CREATE TRIGGER dbo.TR_TestFailVenueUpdate ON dbo.venues AFTER UPDATE AS BEGIN SET NOCOUNT ON; THROW 51044, 'Test-only venue update failure.', 1; END;";
                await trigger.ExecuteNonQueryAsync(ct);
            }

            try
            {
                var editPage = await adminClient.GetAsync($"/Venue/Edit?id={venueId}", ct);
                Assert.Equal(HttpStatusCode.OK, editPage.StatusCode);
                var editHtml = await editPage.Content.ReadAsStringAsync(ct);
                Assert.Contains($"/Venue/Photo?id={venueId}", editHtml, StringComparison.Ordinal);
                var tokenMatch = Regex.Match(editHtml, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
                Assert.True(tokenMatch.Success, "No antiforgery token was rendered on the venue edit form.");

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(tokenMatch.Groups[1].Value), "__RequestVerificationToken");
                form.Add(new StringContent(venueId.ToString()), "VenueId");
                form.Add(new StringContent("Test Venue Updated"), "RoomName");
                form.Add(new StringContent("30"), "Capacity");
                form.Add(new StringContent(""), "Location");
                form.Add(new StringContent(""), "AvailabilityNotes");
                form.Add(new StringContent(""), "ContactPerson");
                form.Add(new StringContent("true"), "IsActive");
                var replacement = new ByteArrayContent(ReplacementPhoto);
                replacement.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(replacement, "photo", "replacement.jpg");

                var failedUpdate = await adminClient.PostAsync("/Venue/Edit", form, ct);
                Assert.Equal(HttpStatusCode.InternalServerError, failedUpdate.StatusCode);

                await using (var context = new AsiBasecodeDBContext(options))
                {
                    var venue = await context.Venues.SingleAsync(item => item.VenueId == venueId, ct);
                    Assert.Equal(oldPath, venue.ImagePath);
                }

                var photoDirectory = Path.Combine(factory.SetupRoot, "uploads", "venues");
                Assert.Single(Directory.GetFiles(photoDirectory));
                Assert.Equal(OldPhoto, await File.ReadAllBytesAsync(Path.Combine(factory.SetupRoot, oldPath.Replace('/', Path.DirectorySeparatorChar)), ct));
            }
            finally
            {
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(fixture.ConnectionString);
                await connection.OpenAsync(ct);
                await using var dropTrigger = connection.CreateCommand();
                dropTrigger.CommandText = "IF OBJECT_ID(N'dbo.TR_TestFailVenueUpdate', N'TR') IS NOT NULL DROP TRIGGER dbo.TR_TestFailVenueUpdate;";
                await dropTrigger.ExecuteNonQueryAsync(ct);
            }

            var createPage = await officerClient.GetAsync("/Venue/Create", ct);
            Assert.Equal(HttpStatusCode.OK, createPage.StatusCode);
            var createToken = Regex.Match(await createPage.Content.ReadAsStringAsync(ct), "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            Assert.True(createToken.Success, "No antiforgery token was rendered on the venue create form.");

            using (var invalidForm = new MultipartFormDataContent())
            {
                invalidForm.Add(new StringContent(createToken.Groups[1].Value), "__RequestVerificationToken");
                invalidForm.Add(new StringContent("Invalid image venue"), "RoomName");
                invalidForm.Add(new StringContent("40"), "Capacity");
                invalidForm.Add(new ByteArrayContent([1, 2, 3, 4]), "photo", "invalid.jpg");
                var invalidResult = await officerClient.PostAsync("/Venue/Create", invalidForm, ct);
                Assert.Equal(HttpStatusCode.OK, invalidResult.StatusCode);
                Assert.Contains("The uploaded file content does not match a supported image format.", await invalidResult.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
            }

            var uploadedName = $"Officer upload {Guid.NewGuid():N}";
            var uploadedPhoto = await LoadPaddedJpegAsync(2 * 1024 * 1024, ct);
            using var uploadForm = new MultipartFormDataContent();
            uploadForm.Add(new StringContent(createToken.Groups[1].Value), "__RequestVerificationToken");
            uploadForm.Add(new StringContent(uploadedName), "RoomName");
            uploadForm.Add(new StringContent("40"), "Capacity");
            uploadForm.Add(new StringContent(""), "Location");
            uploadForm.Add(new StringContent(""), "AvailabilityNotes");
            uploadForm.Add(new StringContent(""), "ContactPerson");
            var uploadedFile = new ByteArrayContent(uploadedPhoto);
            uploadedFile.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            uploadForm.Add(uploadedFile, "photo", "venue-2mib.jpg");

            var uploadResult = await officerClient.PostAsync("/Venue/Create", uploadForm, ct);
            Assert.Equal(HttpStatusCode.Redirect, uploadResult.StatusCode);

            int uploadedVenueId;
            await using (var context = new AsiBasecodeDBContext(options))
            {
                var uploadedVenue = await context.Venues.SingleAsync(item => item.RoomName == uploadedName, ct);
                Assert.Equal(officerId, uploadedVenue.CreatedBy);
                Assert.StartsWith("uploads/venues/", uploadedVenue.ImagePath, StringComparison.Ordinal);
                uploadedVenueId = uploadedVenue.VenueId;
            }

            var fetchedUpload = await officerClient.GetAsync($"/Venue/Photo?id={uploadedVenueId}", ct);
            Assert.Equal(HttpStatusCode.OK, fetchedUpload.StatusCode);
            Assert.Equal("image/jpeg", fetchedUpload.Content.Headers.ContentType?.MediaType);
            Assert.Equal(uploadedPhoto, await fetchedUpload.Content.ReadAsByteArrayAsync(ct));

            var officerEditPage = await officerClient.GetAsync($"/Venue/Edit?id={uploadedVenueId}", ct);
            Assert.Equal(HttpStatusCode.OK, officerEditPage.StatusCode);
            var officerEditToken = Regex.Match(await officerEditPage.Content.ReadAsStringAsync(ct), "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            Assert.True(officerEditToken.Success);
            using var officerEdit = new MultipartFormDataContent();
            officerEdit.Add(new StringContent(officerEditToken.Groups[1].Value), "__RequestVerificationToken");
            officerEdit.Add(new StringContent(uploadedVenueId.ToString()), "VenueId");
            officerEdit.Add(new StringContent("Officer edited venue"), "RoomName");
            officerEdit.Add(new StringContent("45"), "Capacity");
            var officerEditResult = await officerClient.PostAsync("/Venue/Edit", officerEdit, ct);
            Assert.Equal(HttpStatusCode.Redirect, officerEditResult.StatusCode);
            var fetchedAfterEdit = await officerClient.GetAsync($"/Venue/Photo?id={uploadedVenueId}", ct);
            Assert.Equal(HttpStatusCode.OK, fetchedAfterEdit.StatusCode);
            Assert.Equal(uploadedPhoto, await fetchedAfterEdit.Content.ReadAsByteArrayAsync(ct));
            await using (var context = new AsiBasecodeDBContext(options))
            {
                var edited = await context.Venues.AsNoTracking().SingleAsync(v => v.VenueId == uploadedVenueId, ct);
                Assert.Equal("Officer edited venue", edited.RoomName);
                Assert.Equal(45, edited.Capacity);
                var audits = await context.ActivityLogs.AsNoTracking().Where(a => a.EntityType == "venues" && a.EntityId == uploadedVenueId).ToListAsync(ct);
                Assert.Equal(2, audits.Count);
                Assert.All(audits, audit =>
                {
                    Assert.Equal(officerId, audit.ActorUserId);
                    Assert.DoesNotContain("uploads/", audit.OldValues ?? "", StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("uploads/", audit.NewValues ?? "", StringComparison.OrdinalIgnoreCase);
                });
            }
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    private static User CreateUser(string email, RoleCode role, Argon2PasswordHasher hasher, string password) => new()
    {
        Name = "Venue photo test user",
        UserId = email,
        Password = hasher.Hash(password),
        RoleId = role,
        IsActive = true
    };

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password, CancellationToken ct)
    {
        var loginPage = await client.GetAsync("/Account/Login", ct);
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var tokenMatch = Regex.Match(await loginPage.Content.ReadAsStringAsync(ct), "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(tokenMatch.Success, "No antiforgery token was rendered on the login form.");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = tokenMatch.Groups[1].Value
        }), ct);
    }

    private static byte[] JpegBytes(int length, byte fill)
    {
        var bytes = Enumerable.Repeat(fill, length).ToArray();
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static async Task<byte[]> LoadPaddedJpegAsync(int targetLength, CancellationToken cancellationToken)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "venue-upload.jpg");
        var jpeg = await File.ReadAllBytesAsync(fixturePath, cancellationToken);
        if (jpeg.Length < 4 || jpeg[^2] != 0xFF || jpeg[^1] != 0xD9)
        {
            throw new InvalidDataException("The test JPEG fixture must end with a JPEG EOI marker.");
        }

        var paddingLength = targetLength - jpeg.Length;
        if (paddingLength < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLength), "The target must leave room for at least one JPEG comment segment.");
        }

        var segmentCount = (paddingLength + 65536) / 65537;
        var baseSegmentLength = paddingLength / segmentCount;
        var longerSegmentCount = paddingLength % segmentCount;
        if (baseSegmentLength < 4 || baseSegmentLength > 65537)
        {
            throw new InvalidOperationException("The requested JPEG padding cannot be represented by valid comment segments.");
        }

        using var padded = new MemoryStream(targetLength);
        var eoiOffset = jpeg.Length - 2;
        await padded.WriteAsync(jpeg.AsMemory(0, eoiOffset), cancellationToken);
        for (var index = 0; index < segmentCount; index++)
        {
            var segmentLength = baseSegmentLength + (index < longerSegmentCount ? 1 : 0);
            var jpegLengthField = segmentLength - 2;
            padded.WriteByte(0xFF);
            padded.WriteByte(0xFE);
            padded.WriteByte((byte)(jpegLengthField >> 8));
            padded.WriteByte((byte)jpegLengthField);
            var commentPayload = new byte[segmentLength - 4];
            await padded.WriteAsync(commentPayload.AsMemory(), cancellationToken);
        }

        await padded.WriteAsync(jpeg.AsMemory(eoiOffset, 2), cancellationToken);
        if (padded.Length != targetLength)
        {
            throw new InvalidOperationException("The padded JPEG fixture did not reach the requested size.");
        }

        return padded.ToArray();
    }
}
