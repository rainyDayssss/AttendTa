using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventDraftFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task ManagementCreatesAndEditsDraftsWhileMembersAndAnonymousAreDenied()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"event-officer-{suffix}@attendta.test";
        var memberEmail = $"event-member-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var officerId = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, officerId, capacity: 40, ct);
        await EventDraftTestSupport.SeedInactiveReferencesAsync(fixture.ConnectionString, officerId, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var anonymous = await officerClient.GetAsync("/Event", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", LocationPath(anonymous), StringComparison.Ordinal);

        await SignInAsync(memberClient, memberEmail, password, ct);
        var denied = await memberClient.GetAsync("/Event", ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", LocationPath(denied), StringComparison.Ordinal);

        await SignInAsync(officerClient, officerEmail, password, ct);
        var empty = await officerClient.GetStringAsync("/Event", ct);
        Assert.Contains("No records found.", empty, StringComparison.Ordinal);
        Assert.Contains("Create draft event", empty, StringComparison.Ordinal);
        Assert.Contains(">Events</a>", empty, StringComparison.Ordinal);

        var createPage = await officerClient.GetStringAsync("/Event/Create", ct);
        Assert.Contains("Active Type", createPage, StringComparison.Ordinal);
        Assert.Contains("Active Room", createPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Inactive Type", createPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Inactive Room", createPage, StringComparison.Ordinal);

        var createToken = await AntiforgeryTokenAsync(officerClient, "/Event/Create", ct);
        var createResponse = await officerClient.PostAsync("/Event/Create", Form(new Dictionary<string, string>
        {
            ["Title"] = "  Cyber Security Seminar  ",
            ["Description"] = "  Learn the basics  ",
            ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "40",
            ["__RequestVerificationToken"] = createToken
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal("/Event", LocationPath(createResponse));

        int eventId;
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var created = await context.Events.AsNoTracking().SingleAsync(item => item.Title == "Cyber Security Seminar", ct);
            Assert.Equal(EventStatusCode.Draft, created.EventStatusId);
            Assert.Equal(officerId, created.CreatedBy);
            Assert.Equal(new DateTime(2026, 12, 10, 1, 0, 0), created.StartsAt);
            Assert.Equal(new DateTime(2026, 12, 9, 9, 30, 0), created.RegistrationDeadline);
            Assert.Equal("Learn the basics", created.Description);
            Assert.Equal(40, created.ParticipantLimit);
            eventId = created.EventId;
            Assert.Equal(0, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
        }

        var list = await officerClient.GetStringAsync("/Event", ct);
        Assert.Contains($"data-event-id=\"{eventId}\"", list, StringComparison.Ordinal);
        Assert.Contains("2026-12-10 09:00 Asia/Manila", list, StringComparison.Ordinal);
        Assert.Contains("2026-12-09 17:30 Asia/Manila", list, StringComparison.Ordinal);
        Assert.Contains("Active Type", list, StringComparison.Ordinal);
        Assert.Contains("Active Room", list, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Event/Edit?id={eventId}\"", list, StringComparison.Ordinal);

        var editPage = await officerClient.GetStringAsync($"/Event/Edit?id={eventId}", ct);
        Assert.Contains("2026-12-10T09:00", editPage, StringComparison.Ordinal);
        Assert.Contains("2026-12-09T17:30", editPage, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{typeId}\" selected=\"selected\">Active Type</option>", editPage, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{venueId}\" selected=\"selected\">Active Room (capacity 40)</option>", editPage, StringComparison.Ordinal);

        var editToken = await AntiforgeryTokenAsync(officerClient, $"/Event/Edit?id={eventId}", ct);
        var editResponse = await officerClient.PostAsync("/Event/Edit", Form(new Dictionary<string, string>
        {
            ["EventId"] = eventId.ToString(CultureInfo.InvariantCulture),
            ["Title"] = "Cyber Security Seminar (Revised)",
            ["Description"] = "Learn the basics",
            ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T10:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "35",
            ["__RequestVerificationToken"] = editToken
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var edited = await context.Events.AsNoTracking().SingleAsync(item => item.EventId == eventId, ct);
            Assert.Equal("Cyber Security Seminar (Revised)", edited.Title);
            Assert.Equal(new DateTime(2026, 12, 10, 2, 0, 0), edited.StartsAt);
            Assert.Equal(35, edited.ParticipantLimit);
            Assert.Equal(officerId, edited.UpdatedBy);
            Assert.NotNull(edited.UpdatedAt);

            var log = await context.ActivityLogs.AsNoTracking().SingleAsync(
                item => item.EntityType == "events" && item.EntityId == eventId && item.Action == AuditActions.EventUpdated, ct);
            Assert.Equal(officerId, log.ActorUserId);
            Assert.Contains("\"Title\":\"Cyber Security Seminar\"", log.OldValues, StringComparison.Ordinal);
            Assert.Contains("\"Title\":\"Cyber Security Seminar (Revised)\"", log.NewValues, StringComparison.Ordinal);
            Assert.Contains("\"ParticipantLimit\":35", log.NewValues, StringComparison.Ordinal);
            Assert.DoesNotContain("ImagePath", log.NewValues, StringComparison.Ordinal);
        }
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);

    private static async Task SignInAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        var loginPage = await client.GetStringAsync("/Account/Login", cancellationToken);
        var token = Regex.Match(loginPage, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        var response = await client.PostAsync("/Account/Login", Form(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> AntiforgeryTokenAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var page = await client.GetStringAsync(url, cancellationToken);
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private static string LocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }
}

public sealed class EventDraftValidationFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task InactiveReferencesAreRejectedWithoutWritingAndDraftOnlyEditingIsEnforced()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"event-inactive-officer-{suffix}@attendta.test";
        var memberEmail = $"event-inactive-member-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var officerId = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, officerId, capacity: 20, ct);
        var (inactiveTypeId, inactiveVenueId) = await EventDraftTestSupport.SeedInactiveReferencesAsync(fixture.ConnectionString, officerId, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EventDraftTestSupport.SignInAsync(officerClient, officerEmail, password, ct);

        var inactiveTypeResponse = await PostCreateAsync(officerClient, new Dictionary<string, string>
        {
            ["Title"] = "Rejected draft",
            ["TypeId"] = inactiveTypeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "10"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, inactiveTypeResponse.StatusCode);
        Assert.Contains("Select an active event type.", await inactiveTypeResponse.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var inactiveVenueResponse = await PostCreateAsync(officerClient, new Dictionary<string, string>
        {
            ["Title"] = "Rejected draft",
            ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = inactiveVenueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "10"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, inactiveVenueResponse.StatusCode);
        Assert.Contains("Select an active venue.", await inactiveVenueResponse.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var overCapacityResponse = await PostCreateAsync(officerClient, new Dictionary<string, string>
        {
            ["Title"] = "Rejected draft",
            ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "21"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, overCapacityResponse.StatusCode);
        Assert.Contains("Participant limit cannot exceed the venue capacity.", await overCapacityResponse.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var deadlineResponse = await PostCreateAsync(officerClient, new Dictionary<string, string>
        {
            ["Title"] = "Rejected draft",
            ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-10T09:01",
            ["ParticipantLimit"] = "10"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, deadlineResponse.StatusCode);
        Assert.Contains("The signup deadline cannot be after the event start.", await deadlineResponse.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(0, await context.Events.CountAsync(item => item.Title == "Rejected draft", ct));
        }

        var createResponse = await PostCreateAsync(officerClient, new Dictionary<string, string>
        {
            ["Title"] = "Published later",
            ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "10"
        }, ct);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int eventId;
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var created = await context.Events.AsNoTracking().SingleAsync(item => item.Title == "Published later", ct);
            eventId = created.EventId;
        }

        var editInactiveTypeResponse = await PostEditAsync(officerClient, new Dictionary<string, string>
        {
            ["EventId"] = eventId.ToString(CultureInfo.InvariantCulture),
            ["Title"] = "Should not apply",
            ["TypeId"] = inactiveTypeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "10"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, editInactiveTypeResponse.StatusCode);
        Assert.Contains("Select an active event type.", await editInactiveTypeResponse.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var editInactiveVenueResponse = await PostEditAsync(officerClient, new Dictionary<string, string>
        {
            ["EventId"] = eventId.ToString(CultureInfo.InvariantCulture),
            ["Title"] = "Should not apply",
            ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = inactiveVenueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "10"
        }, ct);
        Assert.Equal(HttpStatusCode.OK, editInactiveVenueResponse.StatusCode);
        Assert.Contains("Select an active venue.", await editInactiveVenueResponse.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var unchangedDraft = await context.Events.AsNoTracking().SingleAsync(item => item.EventId == eventId, ct);
            Assert.Equal("Published later", unchangedDraft.Title);
            Assert.Equal(typeId, unchangedDraft.TypeId);
            Assert.Equal(venueId, unchangedDraft.VenueId);
            Assert.Equal(EventStatusCode.Draft, unchangedDraft.EventStatusId);
            Assert.Equal(0, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
        }

        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var published = await context.Events.SingleAsync(item => item.EventId == eventId, ct);
            published.EventStatusId = EventStatusCode.Published;
            await context.SaveChangesAsync(ct);
        }

        var publishedEditPage = await officerClient.GetAsync($"/Event/Edit?id={eventId}", ct);
        Assert.Equal(HttpStatusCode.OK, publishedEditPage.StatusCode);

        var publishedResponse = await PostEditAsync(officerClient, new Dictionary<string, string>
        {
            ["EventId"] = eventId.ToString(CultureInfo.InvariantCulture),
            ["Title"] = "Published edit",
            ["Description"] = "Updated description",
            ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00",
            ["RegistrationDeadline"] = "2026-12-09T17:30",
            ["ParticipantLimit"] = "10"
        }, ct);
        Assert.Equal(HttpStatusCode.Redirect, publishedResponse.StatusCode);
        Assert.Contains(EventDraftTestSupport.LocationPath(publishedResponse), new[] { "/Event", "/Event/Index" });

        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var unchanged = await context.Events.AsNoTracking().SingleAsync(item => item.EventId == eventId, ct);
            Assert.Equal("Published edit", unchanged.Title);
            Assert.Equal("Updated description", unchanged.Description);
            Assert.Equal(EventStatusCode.Published, unchanged.EventStatusId);
            Assert.Equal(1, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
        }

        foreach (var (field, value) in new[]
        {
            ("TypeId", inactiveTypeId.ToString(CultureInfo.InvariantCulture)),
            ("VenueId", inactiveVenueId.ToString(CultureInfo.InvariantCulture)),
            ("StartsAt", "2026-12-10T09:01"),
            ("RegistrationDeadline", "2026-12-09T17:29"),
            ("ParticipantLimit", "11")
        })
        {
            var values = new Dictionary<string, string>
            {
                ["EventId"] = eventId.ToString(CultureInfo.InvariantCulture),
                ["EventStatusId"] = ((int)EventStatusCode.Draft).ToString(CultureInfo.InvariantCulture),
                ["Title"] = "Tampered", ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
                ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
                ["StartsAt"] = "2026-12-10T09:00", ["RegistrationDeadline"] = "2026-12-09T17:30",
                ["ParticipantLimit"] = "10"
            };
            values[field] = value;
            await PostEditAsync(officerClient, values, ct);
        }
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal("Published edit", (await context.Events.AsNoTracking().SingleAsync(item => item.EventId == eventId, ct)).Title);
            Assert.Equal(1, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
        }
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(HttpClient client, Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        var token = await EventDraftTestSupport.AntiforgeryTokenAsync(client, "/Event/Create", cancellationToken);
        values["__RequestVerificationToken"] = token;
        return await client.PostAsync("/Event/Create", EventDraftTestSupport.Form(values), cancellationToken);
    }

    private static async Task<HttpResponseMessage> PostEditAsync(HttpClient client, Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        // The list page renders the shared logout form, so its token also authorizes the edit POST.
        var token = await EventDraftTestSupport.AntiforgeryTokenAsync(client, "/Event", cancellationToken);
        values["__RequestVerificationToken"] = token;
        return await client.PostAsync("/Event/Edit", EventDraftTestSupport.Form(values), cancellationToken);
    }
}

public sealed class EventDraftPagingFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task TwentySixEventsPageInStartThenIdOrderAndClampOutOfRangePages()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"event-page-officer-{suffix}@attendta.test";
        var memberEmail = $"event-page-member-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var officerId = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, officerId, capacity: 20, ct);
        var (pageOne, pageTwo) = await EventDraftTestSupport.SeedEventsAsync(fixture.ConnectionString, officerId, typeId, venueId, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EventDraftTestSupport.SignInAsync(officerClient, officerEmail, password, ct);

        var firstPage = await officerClient.GetStringAsync("/Event", ct);
        Assert.Contains("Page 1 of 2", firstPage, StringComparison.Ordinal);
        Assert.Equal(pageOne, EventIds(firstPage));
        Assert.Contains("aria-label=\"Next page\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Previous page\"", firstPage, StringComparison.Ordinal);

        var lastPage = await officerClient.GetStringAsync("/Event?page=2", ct);
        Assert.Contains("Page 2 of 2", lastPage, StringComparison.Ordinal);
        Assert.Equal(pageTwo, EventIds(lastPage));
        Assert.Contains("aria-label=\"Previous page\"", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", lastPage, StringComparison.Ordinal);

        foreach (var invalidPage in new[] { "0", "-1", "not-a-number" })
        {
            var page = await officerClient.GetStringAsync($"/Event?page={invalidPage}", ct);
            Assert.Equal(pageOne, EventIds(page));
        }

        var pastEnd = await officerClient.GetStringAsync("/Event?page=999999999", ct);
        Assert.Equal(pageTwo, EventIds(pastEnd));
    }

    private static int[] EventIds(string html) =>
        Regex.Matches(html, "data-event-id=\"(\\d+)\"")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
}

public sealed class EventDraftPersistenceTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task CreateLocksTheEventTypeThenTheVenueAndPersistsTheDraftInSql()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, actorId, capacity: 30, ct);
        var recorder = new LockOrderRecorder();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString, recorder);
        var service = EventDraftTestSupport.Service(context, actorId);

        var id = await service.CreateDraftAsync(new EventViewModel
        {
            Title = "SQL draft",
            Description = "Persisted",
            TypeId = typeId,
            VenueId = venueId,
            StartsAt = new DateTime(2026, 12, 10, 1, 0, 0, DateTimeKind.Utc),
            RegistrationDeadline = new DateTime(2026, 12, 9, 9, 30, 0, DateTimeKind.Utc),
            ParticipantLimit = 30
        }, ct);

        Assert.Equal(2, recorder.Locks.Count);
        Assert.Contains("event_types", recorder.Locks[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("venues", recorder.Locks[1], StringComparison.OrdinalIgnoreCase);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var created = await observer.Events.AsNoTracking().SingleAsync(item => item.EventId == id, ct);
        Assert.Equal(EventStatusCode.Draft, created.EventStatusId);
        Assert.Equal(actorId, created.CreatedBy);
        Assert.Equal(new DateTime(2026, 12, 10, 1, 0, 0), created.StartsAt);
        Assert.Equal(new DateTime(2026, 12, 9, 9, 30, 0), created.RegistrationDeadline);
        Assert.Null(created.UpdatedBy);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(log => log.EntityType == "events", ct));
    }

    internal sealed class LockOrderRecorder : DbCommandInterceptor
    {
        public List<string> Locks { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                Locks.Add(command.CommandText);
            }

            return ValueTask.FromResult(result);
        }
    }
}

public sealed class EventDraftEditPersistenceTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task EditLocksTypeThenVenueThenEventAndWritesOneEventUpdatedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, actorId, capacity: 30, ct);
        int eventId;
        await using (var seed = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            eventId = await EventDraftTestSupport.Service(seed, actorId).CreateDraftAsync(new EventViewModel
            {
                Title = "Before",
                TypeId = typeId,
                VenueId = venueId,
                StartsAt = new DateTime(2026, 12, 10, 1, 0, 0, DateTimeKind.Utc),
                RegistrationDeadline = new DateTime(2026, 12, 9, 9, 30, 0, DateTimeKind.Utc),
                ParticipantLimit = 10
            }, ct);
        }

        var recorder = new EventDraftPersistenceTests.LockOrderRecorder();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString, recorder);
        var service = EventDraftTestSupport.Service(context, actorId);

        await service.UpdateDraftAsync(new EventViewModel
        {
            EventId = eventId,
            Title = "After",
            EventStatusId = EventStatusCode.Draft,
            TypeId = typeId,
            VenueId = venueId,
            StartsAt = new DateTime(2026, 12, 10, 2, 0, 0, DateTimeKind.Utc),
            RegistrationDeadline = new DateTime(2026, 12, 9, 9, 30, 0, DateTimeKind.Utc),
            ParticipantLimit = 12
        }, ct);

        Assert.Equal(3, recorder.Locks.Count);
        Assert.Contains("event_types", recorder.Locks[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("venues", recorder.Locks[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("events", recorder.Locks[2], StringComparison.OrdinalIgnoreCase);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var edited = await observer.Events.AsNoTracking().SingleAsync(item => item.EventId == eventId, ct);
        Assert.Equal("After", edited.Title);
        Assert.Equal(12, edited.ParticipantLimit);
        Assert.Equal(actorId, edited.UpdatedBy);

        var log = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            item => item.EntityType == "events" && item.EntityId == eventId, ct);
        Assert.Equal(AuditActions.EventUpdated, log.Action);
        Assert.Equal(actorId, log.ActorUserId);
        Assert.Contains("\"Title\":\"Before\"", log.OldValues, StringComparison.Ordinal);
        Assert.Contains("\"Title\":\"After\"", log.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"TypeName\":\"Active Type\"", log.OldValues, StringComparison.Ordinal);
        Assert.Contains("\"VenueName\":\"Active Room\"", log.NewValues, StringComparison.Ordinal);
    }
}

internal static class EventDraftTestSupport
{
    public static readonly IMapper Mapper = new MapperConfiguration(config =>
    {
        config.CreateMap<EventViewModel, Event>();
        config.CreateMap<Event, EventViewModel>();
        config.CreateMap<EventListItem, EventViewModel>();
    }).CreateMapper();

    public static AsiBasecodeDBContext Context(string connectionString, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(connectionString)
            .AddInterceptors(interceptors).Options);

    public static EventLifecycleService Service(AsiBasecodeDBContext context, int actorId, TimeProvider? clock = null,
        Func<IActivityLogWriter, IActivityLogWriter>? wrapAudit = null)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorId);
        var timeProvider = clock ?? TimeProvider.System;
        var events = new EventRepository(unitOfWork);
        var registrations = new RegistrationRepository(unitOfWork);
        IActivityLogWriter audit = new ActivityLogWriter(context, actor, timeProvider);
        if (wrapAudit != null)
        {
            audit = wrapAudit(audit);
        }

        return new EventLifecycleService(
            events,
            new EventTypeRepository(unitOfWork),
            new VenueRepository(unitOfWork),
            registrations,
            new RegistrationCancellationService(registrations, events, unitOfWork, actor, audit, timeProvider),
            unitOfWork,
            Mapper,
            timeProvider,
            audit,
            actor,
            new UnusedPrivateFileStore(),
            NullLogger<EventLifecycleService>.Instance);
    }

    public static async Task<int> SeedActorAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var actor = new User
        {
            Name = "Event Draft Tester",
            UserId = $"{Guid.NewGuid():N}@example.invalid",
            Password = "test",
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        context.Users.Add(actor);
        await context.SaveChangesAsync(cancellationToken);
        return actor.Id;
    }

    public static async Task<int> SeedUsersAsync(string connectionString, string officerEmail, string memberEmail, string password, CancellationToken cancellationToken)
    {
        var hasher = new Argon2PasswordHasher();
        await using var context = Context(connectionString);
        var officer = new User
        {
            UserId = officerEmail,
            Name = "Event Draft Officer",
            Password = hasher.Hash(password),
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        var member = new User
        {
            UserId = memberEmail,
            Name = "Event Draft Member",
            Password = hasher.Hash(password),
            RoleId = RoleCode.Member,
            IsActive = true
        };
        context.Users.AddRange(officer, member);
        await context.SaveChangesAsync(cancellationToken);
        return officer.Id;
    }

    public static async Task<(int TypeId, int VenueId)> SeedActiveReferencesAsync(string connectionString, int actorId, int capacity, CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var eventType = new EventType
        {
            TypeName = "Active Type",
            Description = "Active",
            IsActive = true,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        var venue = new Venue
        {
            RoomName = "Active Room",
            Capacity = capacity,
            ImagePath = "uploads/venues/test.jpg",
            IsActive = true,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        context.EventTypes.Add(eventType);
        context.Venues.Add(venue);
        await context.SaveChangesAsync(cancellationToken);
        return (eventType.TypeId, venue.VenueId);
    }

    public static async Task<(int TypeId, int VenueId)> SeedInactiveReferencesAsync(string connectionString, int actorId, CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var eventType = new EventType
        {
            TypeName = "Inactive Type",
            Description = "Inactive",
            IsActive = false,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        var venue = new Venue
        {
            RoomName = "Inactive Room",
            Capacity = 20,
            ImagePath = "uploads/venues/test.jpg",
            IsActive = false,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        context.EventTypes.Add(eventType);
        context.Venues.Add(venue);
        await context.SaveChangesAsync(cancellationToken);
        return (eventType.TypeId, venue.VenueId);
    }

    public static async Task<(int[] PageOne, int[] PageTwo)> SeedEventsAsync(
        string connectionString, int actorId, int typeId, int venueId, CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var start = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var events = new List<Event>();
        for (var index = 1; index <= 26; index++)
        {
            // Two rows share a start so the event_id tie-break decides their order.
            var startsAt = start.AddHours(index <= 25 ? index : 25);
            events.Add(new Event
            {
                Title = $"Paged Event {index:D2}",
                TypeId = typeId,
                VenueId = venueId,
                StartsAt = startsAt,
                RegistrationDeadline = startsAt.AddHours(-1),
                ParticipantLimit = 10,
                EventStatusId = EventStatusCode.Draft,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            });
        }

        context.Events.AddRange(events);
        await context.SaveChangesAsync(cancellationToken);

        var ordered = events.OrderBy(item => item.StartsAt).ThenBy(item => item.EventId).Select(item => item.EventId).ToArray();
        return (ordered[..25], ordered[25..]);
    }

    public static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);

    public static async Task SignInAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        var loginPage = await client.GetStringAsync("/Account/Login", cancellationToken);
        var token = Regex.Match(loginPage, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        var response = await client.PostAsync("/Account/Login", Form(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public static async Task<string> AntiforgeryTokenAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var page = await client.GetStringAsync(url, cancellationToken);
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    public static string LocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }

    private sealed class Actor(int id) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => "officer";
    }

    private sealed class UnusedPrivateFileStore : IPrivateFileStore
    {
        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();
        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Delete(string relativePath) => throw new NotSupportedException();
    }
}
