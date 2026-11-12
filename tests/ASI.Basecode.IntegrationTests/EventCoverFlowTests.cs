using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventCoverFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task ManagementUploadsReplacesAndRemovesCoversWithValidationAndAuditing()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"event-cover-officer-{suffix}@attendta.test";
        var memberEmail = $"event-cover-member-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var officerId = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (typeId, venueId) = await SeedReferencesAsync(fixture.ConnectionString, officerId, 50, ct);
        var draftId = await SeedEventAsync(fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Draft, null, ct);
        var publishedInitial = JpegBytes(80, 0x33);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        var publishedPath = await SaveImageAsync(factory, publishedInitial, ".jpg", ct);
        var publishedId = await SeedEventAsync(fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published, publishedPath, ct);
        using var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EventDraftTestSupport.SignInAsync(officerClient, officerEmail, password, ct);

        var eventList = await officerClient.GetStringAsync("/Event", ct);
        Assert.Contains($"href=\"/Event/Cover?id={draftId}\"", eventList, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Event/Cover?id={publishedId}\"", eventList, StringComparison.Ordinal);

        var noAntiforgery = await UploadAsync(officerClient, draftId, "cover.jpg", JpegBytes(64, 0x45), null, ct);
        Assert.Equal(HttpStatusCode.BadRequest, noAntiforgery.StatusCode);

        var draftPage = await officerClient.GetAsync($"/Event/Cover?id={draftId}", ct);
        Assert.Equal(HttpStatusCode.OK, draftPage.StatusCode);
        var draftToken = AntiforgeryToken(await draftPage.Content.ReadAsStringAsync(ct));
        Assert.Contains("Upload cover", await draftPage.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        var upload = await UploadAsync(officerClient, draftId, "draft.jpg", JpegBytes(96, 0x51), draftToken, ct);
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);

        string firstDraftPath;
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var draft = await context.Events.AsNoTracking().SingleAsync(item => item.EventId == draftId, ct);
            firstDraftPath = draft.ImagePath;
            Assert.StartsWith("uploads/events/", firstDraftPath, StringComparison.Ordinal);
            Assert.Equal(officerId, draft.UpdatedBy);
            Assert.True(File.Exists(Resolve(factory.SetupRoot, firstDraftPath)));
            var audit = await context.ActivityLogs.AsNoTracking().SingleAsync(
                row => row.EntityType == "events" && row.EntityId == draftId && row.Action == AuditActions.EventUpdated, ct);
            Assert.DoesNotContain("uploads/", audit.OldValues ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("uploads/", audit.NewValues ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"HasCover\":true", audit.NewValues ?? string.Empty, StringComparison.Ordinal);
        }

        var firstImage = await officerClient.GetAsync($"/EventCover/Image?id={draftId}", ct);
        Assert.Equal(HttpStatusCode.OK, firstImage.StatusCode);
        Assert.Equal("image/jpeg", firstImage.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store, private", firstImage.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", string.Join(", ", firstImage.Headers.Pragma.Select(value => value!.ToString())));
        Assert.Equal(JpegBytes(96, 0x51), await firstImage.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal(HttpStatusCode.NotFound, (await officerClient.GetAsync($"/{firstDraftPath}", ct)).StatusCode);
        var coverManagementPage = await officerClient.GetStringAsync($"/Event/Cover?id={draftId}", ct);
        Assert.DoesNotContain(firstDraftPath, coverManagementPage, StringComparison.Ordinal);

        var managementPublishedCover = await officerClient.GetAsync($"/EventCover/Image?id={publishedId}", ct);
        Assert.Equal(HttpStatusCode.OK, managementPublishedCover.StatusCode);
        Assert.Equal(publishedInitial, await managementPublishedCover.Content.ReadAsByteArrayAsync(ct));

        var invalidToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officerClient, $"/Event/Cover?id={draftId}", ct);
        var invalidUpload = await UploadAsync(officerClient, draftId, "invalid.png", JpegBytes(80, 0x61), invalidToken, ct);
        Assert.Equal(HttpStatusCode.OK, invalidUpload.StatusCode);
        Assert.Contains("does not match a supported image format", await invalidUpload.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var oversizedToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officerClient, $"/Event/Cover?id={draftId}", ct);
        var oversized = await UploadAsync(officerClient, draftId, "oversized.jpg", JpegBytes(2 * 1024 * 1024 + 1, 0x63), oversizedToken, ct);
        Assert.Equal(HttpStatusCode.OK, oversized.StatusCode);
        Assert.Contains("2 MiB or smaller", await oversized.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var emptyToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officerClient, $"/Event/Cover?id={draftId}", ct);
        var empty = await UploadAsync(officerClient, draftId, "empty.jpg", [], emptyToken, ct);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Contains("does not match a supported image format", await empty.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(firstDraftPath, (await context.Events.AsNoTracking().SingleAsync(item => item.EventId == draftId, ct)).ImagePath);
            Assert.Equal(2, Directory.GetFiles(Path.Combine(factory.SetupRoot, "uploads", "events")).Length);
        }
        var preservedCover = await officerClient.GetAsync($"/EventCover/Image?id={draftId}", ct);
        Assert.Equal(HttpStatusCode.OK, preservedCover.StatusCode);
        Assert.Equal(JpegBytes(96, 0x51), await preservedCover.Content.ReadAsByteArrayAsync(ct));

        await CreateFailingCoverUpdateTriggerAsync(fixture.ConnectionString, ct);
        try
        {
            var failedReplacementToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officerClient, $"/Event/Cover?id={draftId}", ct);
            var failedReplacement = await UploadAsync(officerClient, draftId, "rollback.jpg", JpegBytes(88, 0x69), failedReplacementToken, ct);
            Assert.Equal(HttpStatusCode.InternalServerError, failedReplacement.StatusCode);
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            Assert.Equal(firstDraftPath, (await context.Events.AsNoTracking().SingleAsync(item => item.EventId == draftId, ct)).ImagePath);
            Assert.Equal(1, await context.ActivityLogs.CountAsync(
                row => row.EntityType == "events" && row.EntityId == draftId && row.Action == AuditActions.EventUpdated, ct));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(factory.SetupRoot, "uploads", "events")).Length);
            Assert.Equal(JpegBytes(96, 0x51), await File.ReadAllBytesAsync(Resolve(factory.SetupRoot, firstDraftPath), ct));
        }
        finally
        {
            await DropFailingCoverUpdateTriggerAsync(fixture.ConnectionString, ct);
        }

        var removeDraftToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officerClient, $"/Event/Cover?id={draftId}", ct);
        var removeDraft = await PostAsync(officerClient, "/Event/RemoveCover", draftId, removeDraftToken, ct);
        Assert.Equal(HttpStatusCode.Redirect, removeDraft.StatusCode);
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Null((await context.Events.AsNoTracking().SingleAsync(item => item.EventId == draftId, ct)).ImagePath);
            Assert.False(File.Exists(Resolve(factory.SetupRoot, firstDraftPath)));
            Assert.Equal(2, await context.ActivityLogs.CountAsync(
                row => row.EntityType == "events" && row.EntityId == draftId && row.Action == AuditActions.EventUpdated, ct));
        }

        var reuploadToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officerClient, $"/Event/Cover?id={draftId}", ct);
        var reuploadedPng = PngBytes(72, 0x62);
        var reupload = await UploadAsync(officerClient, draftId, "reupload.png", reuploadedPng, reuploadToken, ct);
        Assert.Equal(HttpStatusCode.Redirect, reupload.StatusCode);
        string reuploadedDraftPath;
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            reuploadedDraftPath = (await context.Events.AsNoTracking().SingleAsync(item => item.EventId == draftId, ct)).ImagePath;
            Assert.EndsWith(".png", reuploadedDraftPath, StringComparison.Ordinal);
            Assert.True(File.Exists(Resolve(factory.SetupRoot, reuploadedDraftPath)));
            Assert.Equal(3, await context.ActivityLogs.CountAsync(
                row => row.EntityType == "events" && row.EntityId == draftId && row.Action == AuditActions.EventUpdated, ct));
        }
        var reuploadedCover = await officerClient.GetAsync($"/EventCover/Image?id={draftId}", ct);
        Assert.Equal(HttpStatusCode.OK, reuploadedCover.StatusCode);
        Assert.Equal("image/png", reuploadedCover.Content.Headers.ContentType?.MediaType);
        Assert.Equal(reuploadedPng, await reuploadedCover.Content.ReadAsByteArrayAsync(ct));

        var removePublishedToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officerClient, $"/Event/Cover?id={publishedId}", ct);
        var removePublished = await PostAsync(officerClient, "/Event/RemoveCover", publishedId, removePublishedToken, ct);
        Assert.Equal(HttpStatusCode.OK, removePublished.StatusCode);
        Assert.Contains("Only draft event covers can be removed.", await removePublished.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(publishedPath, (await context.Events.AsNoTracking().SingleAsync(item => item.EventId == publishedId, ct)).ImagePath);
        }

        var publishedCoverToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officerClient, $"/Event/Cover?id={publishedId}", ct);
        var publishedReplacement = await UploadAsync(officerClient, publishedId, "published.png", PngBytes(80, 0x73), publishedCoverToken, ct);
        Assert.Equal(HttpStatusCode.Redirect, publishedReplacement.StatusCode);
        Assert.False(File.Exists(Resolve(factory.SetupRoot, publishedPath)));

        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(reuploadedDraftPath, (await context.Events.AsNoTracking().SingleAsync(item => item.EventId == draftId, ct)).ImagePath);
            Assert.True(File.Exists(Resolve(factory.SetupRoot, reuploadedDraftPath)));
            Assert.Equal(3, await context.ActivityLogs.CountAsync(
                row => row.EntityType == "events" && row.EntityId == draftId && row.Action == AuditActions.EventUpdated, ct));
            var publishedAudit = await context.ActivityLogs.AsNoTracking().SingleAsync(
                row => row.EntityType == "events" && row.EntityId == publishedId && row.Action == AuditActions.EventUpdated, ct);
            Assert.Contains("\"CoverChanged\":true", publishedAudit.NewValues, StringComparison.Ordinal);
            Assert.DoesNotContain("uploads/", publishedAudit.OldValues, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("uploads/", publishedAudit.NewValues, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(HttpStatusCode.OK, (await officerClient.GetAsync($"/EventCover/Image?id={draftId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await officerClient.GetAsync("/EventCover/Image?id=2147483647", ct)).StatusCode);
    }

    [Theory]
    [InlineData(EventStatusCode.Completed)]
    [InlineData(EventStatusCode.Cancelled)]
    public async Task TerminalCoverMutationsLeaveDatabaseAuditAndPrivateFileUnchanged(EventStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"cover-terminal-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var actor = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, email, $"member-{suffix}@attendta.test", password, ct);
        var (typeId, venueId) = await SeedReferencesAsync(fixture.ConnectionString, actor, 30, ct);
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        var original = JpegBytes(64, 0x42);
        var path = await SaveImageAsync(factory, original, ".jpg", ct);
        var id = await SeedEventAsync(fixture.ConnectionString, actor, typeId, venueId, status, path, ct);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EventDraftTestSupport.SignInAsync(client, email, password, ct);
        var token = await EventDraftTestSupport.AntiforgeryTokenAsync(client, $"/Event/Cover?id={id}", ct);
        var upload = await UploadAsync(client, id, "new.jpg", JpegBytes(64, 0x53), token, ct);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var remove = await PostAsync(client, "/Event/RemoveCover", id, token, ct);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var row = await observer.Events.AsNoTracking().SingleAsync(item => item.EventId == id, ct);
        Assert.Equal(status, row.EventStatusId);
        Assert.Equal(path, row.ImagePath);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == id, ct));
        Assert.Equal(original, await File.ReadAllBytesAsync(Resolve(factory.SetupRoot, path), ct));
        Assert.Single(Directory.GetFiles(Path.Combine(factory.SetupRoot, "uploads", "events")));
    }

    [Fact]
    public async Task MembersCanReadPublishedCoverButCannotReadDraftOrStaticStorage()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"event-cover-read-officer-{suffix}@attendta.test";
        var memberEmail = $"event-cover-read-member-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var officerId = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (typeId, venueId) = await SeedReferencesAsync(fixture.ConnectionString, officerId, 40, ct);
        var publishedBytes = JpegBytes(64, 0x27);
        var draftBytes = PngBytes(64, 0x37);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        var publishedPath = await SaveImageAsync(factory, publishedBytes, ".jpg", ct);
        var draftPath = await SaveImageAsync(factory, draftBytes, ".png", ct);
        var publishedId = await SeedEventAsync(fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published, publishedPath, ct);
        var draftId = await SeedEventAsync(fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Draft, draftPath, ct);
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var anonymous = await anonymousClient.GetAsync($"/EventCover/Image?id={publishedId}", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Contains("/Account/Login", anonymous.Headers.Location?.ToString(), StringComparison.Ordinal);

        await EventDraftTestSupport.SignInAsync(memberClient, memberEmail, password, ct);

        var eventManagement = await memberClient.GetAsync("/Event", ct);
        Assert.Equal(HttpStatusCode.Redirect, eventManagement.StatusCode);
        Assert.Contains("/Account/AccessDenied", EventDraftTestSupport.LocationPath(eventManagement), StringComparison.Ordinal);
        var coverManagement = await memberClient.GetAsync($"/Event/Cover?id={publishedId}", ct);
        Assert.Equal(HttpStatusCode.Redirect, coverManagement.StatusCode);
        Assert.Contains("/Account/AccessDenied", EventDraftTestSupport.LocationPath(coverManagement), StringComparison.Ordinal);

        var memberToken = await EventDraftTestSupport.AntiforgeryTokenAsync(memberClient, "/Home/Index", ct);
        var deniedUpload = await UploadAsync(memberClient, draftId, "member.jpg", JpegBytes(48, 0x47), memberToken, ct);
        Assert.Equal(HttpStatusCode.Redirect, deniedUpload.StatusCode);
        Assert.Contains("/Account/AccessDenied", EventDraftTestSupport.LocationPath(deniedUpload), StringComparison.Ordinal);
        var deniedRemoval = await PostAsync(memberClient, "/Event/RemoveCover", draftId, memberToken, ct);
        Assert.Equal(HttpStatusCode.Redirect, deniedRemoval.StatusCode);
        Assert.Contains("/Account/AccessDenied", EventDraftTestSupport.LocationPath(deniedRemoval), StringComparison.Ordinal);

        var publishedCover = await memberClient.GetAsync($"/EventCover/Image?id={publishedId}", ct);
        Assert.Equal(HttpStatusCode.OK, publishedCover.StatusCode);
        Assert.Equal(publishedBytes, await publishedCover.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal("no-store, private", publishedCover.Headers.CacheControl?.ToString());

        var draftCover = await memberClient.GetAsync($"/EventCover/Image?id={draftId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, draftCover.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await memberClient.GetAsync("/EventCover/Image?id=2147483647", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await memberClient.GetAsync($"/{draftPath}", ct)).StatusCode);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(publishedPath, (await observer.Events.AsNoTracking().SingleAsync(item => item.EventId == publishedId, ct)).ImagePath);
        Assert.Equal(draftPath, (await observer.Events.AsNoTracking().SingleAsync(item => item.EventId == draftId, ct)).ImagePath);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(
            row => row.EntityType == "events" && (row.EntityId == publishedId || row.EntityId == draftId), ct));
        Assert.True(File.Exists(Resolve(factory.SetupRoot, publishedPath)));
        Assert.True(File.Exists(Resolve(factory.SetupRoot, draftPath)));
    }

    private static async Task<int> SeedEventAsync(
        string connectionString,
        int actorId,
        int typeId,
        int venueId,
        EventStatusCode status,
        string? imagePath,
        CancellationToken cancellationToken)
    {
        await using var context = EventDraftTestSupport.Context(connectionString);
        var eventItem = new Event
        {
            Title = $"Cover test {Guid.NewGuid():N}",
            TypeId = typeId,
            VenueId = venueId,
            StartsAt = new DateTime(2026, 12, 10, 9, 0, 0, DateTimeKind.Utc),
            RegistrationDeadline = new DateTime(2026, 12, 9, 9, 0, 0, DateTimeKind.Utc),
            ParticipantLimit = 20,
            EventStatusId = status,
            ImagePath = imagePath,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        context.Events.Add(eventItem);
        await context.SaveChangesAsync(cancellationToken);
        return eventItem.EventId;
    }

    private static async Task<(int TypeId, int VenueId)> SeedReferencesAsync(
        string connectionString, int actorId, int capacity, CancellationToken cancellationToken)
    {
        await using var context = EventDraftTestSupport.Context(connectionString);
        var suffix = Guid.NewGuid().ToString("N");
        var eventType = new EventType
        {
            TypeName = $"Event Cover Type {suffix}",
            Description = "Event cover test type",
            IsActive = true,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        var venue = new Venue
        {
            RoomName = $"Event Cover Room {suffix}",
            Capacity = capacity,
            ImagePath = "uploads/venues/event-cover-test.jpg",
            IsActive = true,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        context.EventTypes.Add(eventType);
        context.Venues.Add(venue);
        await context.SaveChangesAsync(cancellationToken);
        return (eventType.TypeId, venue.VenueId);
    }

    private static async Task CreateFailingCoverUpdateTriggerAsync(string connectionString, CancellationToken cancellationToken)
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER dbo.TR_TestFailEventCoverUpdate ON dbo.events AFTER UPDATE AS BEGIN SET NOCOUNT ON; THROW 51052, 'Test-only event cover update failure.', 1; END;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DropFailingCoverUpdateTriggerAsync(string connectionString, CancellationToken cancellationToken)
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "IF OBJECT_ID(N'dbo.TR_TestFailEventCoverUpdate', N'TR') IS NOT NULL DROP TRIGGER dbo.TR_TestFailEventCoverUpdate;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> SaveImageAsync(AttendTaWebApplicationFactory factory, byte[] bytes, string extension, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        await using var image = new MemoryStream(bytes);
        return await files.SaveAsync(PrivateFileArea.EventImages, image, extension, cancellationToken);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, int eventId, string filename, byte[] bytes, string? antiforgeryToken, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        if (antiforgeryToken != null)
        {
            form.Add(new StringContent(antiforgeryToken), "__RequestVerificationToken");
        }

        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
        form.Add(file, "cover", filename);
        return await client.PostAsync($"/Event/UploadCover?id={eventId.ToString(CultureInfo.InvariantCulture)}", form, cancellationToken);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, int eventId, string token, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        return await client.PostAsync($"{path}?id={eventId.ToString(CultureInfo.InvariantCulture)}", form, cancellationToken);
    }

    private static string AntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, "No antiforgery token was rendered on the event cover form.");
        return match.Groups[1].Value;
    }

    private static byte[] JpegBytes(int length, byte fill)
    {
        var bytes = Enumerable.Repeat(fill, length).ToArray();
        bytes[0] = 0xff;
        bytes[1] = 0xd8;
        bytes[2] = 0xff;
        return bytes;
    }

    private static byte[] PngBytes(int length, byte fill)
    {
        var bytes = Enumerable.Repeat(fill, length).ToArray();
        byte[] signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    private static string Resolve(string root, string relativePath) => Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
