using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventCombinedDraftFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task CreateWithCoverIsAtomicAndInvalidImageRetainsForm()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"event-combined-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var actor = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, email, $"member-{suffix}@attendta.test", password, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, actor, 40, ct);
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        factory.ConfigureServicesForTesting = services => services.AddSingleton<TimeProvider>(EventDraftTestSupport.DraftClock);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EventDraftTestSupport.SignInAsync(client, email, password, ct);
        var token = await EventDraftTestSupport.AntiforgeryTokenAsync(client, "/Event/Create", ct);
        MultipartFormDataContent FormWithCover(byte[] bytes)
        {
            var content = new MultipartFormDataContent();
            foreach (var (key, value) in new Dictionary<string, string>
            {
                ["Title"] = "Combined cover event", ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
                ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture), ["StartsAt"] = "2026-12-10T09:00",
                ["RegistrationDeadline"] = "2026-12-09T17:30", ["ParticipantLimit"] = "20", ["__RequestVerificationToken"] = token
            }) content.Add(new StringContent(value), key);
            content.Add(new ByteArrayContent(bytes), "cover", "cover.jpg");
            return content;
        }
        using (var invalid = FormWithCover([1, 2, 3]))
        {
            var response = await client.PostAsync("/Event/Create", invalid, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("Combined cover event", html, StringComparison.Ordinal);
            Assert.Contains("Choose the image again", html, StringComparison.Ordinal);
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            Assert.False(await context.Events.AnyAsync(e => e.Title == "Combined cover event", ct));
        }
        using (var valid = FormWithCover([0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0]))
        {
            var response = await client.PostAsync("/Event/Create", valid, ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var created = await context.Events.AsNoTracking().SingleAsync(e => e.Title == "Combined cover event", ct);
            Assert.StartsWith("uploads/events/", created.ImagePath, StringComparison.Ordinal);
            Assert.Equal(EventStatusCode.Draft, created.EventStatusId);
            Assert.Equal(1, await context.ActivityLogs.CountAsync(a => a.EntityId == created.EventId && a.EntityType == "events", ct));
        }
    }

}

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
        factory.ConfigureServicesForTesting = services => services.AddSingleton<TimeProvider>(EventDraftTestSupport.DraftClock);
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
        Assert.Contains("min=\"2026-11-20T00:00\"", createPage, StringComparison.Ordinal);
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
            Assert.Equal(EventDraftTestSupport.DraftClock.GetUtcNow().UtcDateTime, created.CreatedAt);
            Assert.Equal(new DateTime(2026, 12, 10, 1, 0, 0), created.StartsAt);
            Assert.Equal(new DateTime(2026, 12, 9, 9, 30, 0), created.RegistrationDeadline);
            Assert.Equal("Learn the basics", created.Description);
            Assert.Equal(40, created.ParticipantLimit);
            eventId = created.EventId;
            Assert.Equal(1, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
        }

        var list = await officerClient.GetStringAsync("/Event", ct);
        Assert.Contains($"data-event-id=\"{eventId}\"", list, StringComparison.Ordinal);
        Assert.Contains("2026-12-10 09:00 AM", list, StringComparison.Ordinal);
        Assert.Contains("2026-12-09 05:30 PM", list, StringComparison.Ordinal);
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
        factory.ConfigureServicesForTesting = services => services.AddSingleton<TimeProvider>(EventDraftTestSupport.DraftClock);
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
            Assert.Equal(1, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
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
            Assert.Equal(2, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
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
            Assert.Equal(2, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
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

public sealed class EventCreateValidationTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    // 23:00 UTC is already 07:00 the following day in Manila.
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateValidationUsesManilaDateAndDisplaysFieldErrorsOnlyBesideFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"validation-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var actor = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, email, $"member-{suffix}@attendta.test", password, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, actor, 40, ct);
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        factory.ConfigureServicesForTesting = services => services.AddSingleton<TimeProvider>(new FixedClock());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EventDraftTestSupport.SignInAsync(client, email, password, ct);
        client.DefaultRequestHeaders.Add("X-Record-Dialog", "true");

        var create = await client.GetStringAsync("/Event/Create", ct);
        foreach (var field in new[] { "StartsAt", "RegistrationDeadline" })
        {
            var input = Input(create, field);
            Assert.Contains("min=\"2026-11-20T00:00\"", input, StringComparison.Ordinal);
        }

        async Task<HttpResponseMessage> Post(string title, string start, string deadline, string venue)
        {
            var token = await EventDraftTestSupport.AntiforgeryTokenAsync(client, "/Event/Create", ct);
            return await client.PostAsync("/Event/Create", EventDraftTestSupport.Form(new Dictionary<string, string>
            {
                ["Title"] = title, ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
                ["VenueId"] = venue, ["StartsAt"] = start, ["RegistrationDeadline"] = deadline,
                ["ParticipantLimit"] = "10", ["__RequestVerificationToken"] = token
            }), ct);
        }

        var validVenue = venueId.ToString(CultureInfo.InvariantCulture);
        var missingVenue = await Post($"Missing venue {suffix}", "2026-11-21T09:00", "2026-11-20T09:00", "");
        Assert.Equal(HttpStatusCode.OK, missingVenue.StatusCode);
        var invalidHtml = await missingVenue.Content.ReadAsStringAsync(ct);
        AssertFieldError(invalidHtml, "VenueId", "Select a venue.");

        foreach (var (title, start, deadline, field) in new[]
        {
            ($"Past start {suffix}", "2026-11-19T23:00", "2026-11-19T22:00", "StartsAt"),
            ($"Past deadline {suffix}", "2026-11-21T09:00", "2026-11-19T23:00", "RegistrationDeadline"),
            ($"Deadline after start {suffix}", "2026-11-20T05:00", "2026-11-20T05:01", "RegistrationDeadline")
        })
        {
            var response = await Post(title, start, deadline, validVenue);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync(ct);
            var error = Regex.Match(html, $@"<span\b[^>]*id=""{field}-validation""[^>]*>(.*?)</span>", RegexOptions.Singleline);
            Assert.True(error.Success, $"Missing validation span for {field}.");
            var message = WebUtility.HtmlDecode(Regex.Replace(error.Groups[1].Value, "<[^>]+>", "")).Trim();
            Assert.False(string.IsNullOrEmpty(message));
            AssertFieldError(html, field, message);
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            Assert.False(await context.Events.AnyAsync(e => e.Title == title, ct));
        }

        var acceptedTitle = $"Earlier today {suffix}";
        var accepted = await Post(acceptedTitle, "2026-11-20T06:00", "2026-11-20T05:00", validVenue);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var result = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(ct));
        Assert.True(result.RootElement.GetProperty("saved").GetBoolean());
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var draft = await observer.Events.AsNoTracking().SingleAsync(e => e.Title == acceptedTitle, ct);
        Assert.Equal(EventStatusCode.Draft, draft.EventStatusId);
        Assert.False(await observer.Events.AnyAsync(e => e.Title == $"Missing venue {suffix}", ct));

        var edit = await client.GetStringAsync($"/Event/Edit?id={draft.EventId}", ct);
        foreach (var field in new[] { "StartsAt", "RegistrationDeadline" })
            Assert.DoesNotContain(" min=", Input(edit, field), StringComparison.Ordinal);
    }

    private static string Input(string html, string field)
    {
        var match = Regex.Match(html, $@"<input\b(?=[^>]*\bname=""{field}"")[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Missing input for {field}.");
        return match.Value;
    }

    private static void AssertFieldError(string html, string field, string message)
    {
        var decoded = WebUtility.HtmlDecode(html);
        var span = Regex.Match(decoded, $@"<span\b[^>]*id=""{field}-validation""[^>]*>(.*?)</span>", RegexOptions.Singleline);
        Assert.True(span.Success, $"Missing validation span for {field}.");
        Assert.Contains(message, span.Groups[1].Value, StringComparison.Ordinal);
        var validationSpans = Regex.Matches(decoded, @"<span\b[^>]*data-valmsg-for=""[^""]+""[^>]*>.*?</span>", RegexOptions.Singleline);
        Assert.Equal(1, validationSpans.Cast<Match>().Count(item => item.Value.Contains(message, StringComparison.Ordinal)));
        var summary = Regex.Match(decoded, @"<div\b[^>]*data-record-errors[^>]*>.*?</div>", RegexOptions.Singleline);
        if (summary.Success)
            Assert.DoesNotContain(message, summary.Value, StringComparison.Ordinal);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
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
        Assert.Contains("Page 1 of 6", firstPage, StringComparison.Ordinal);
        Assert.Equal(pageOne, EventIds(firstPage));
        Assert.Contains("aria-label=\"Next page\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Previous page\"", firstPage, StringComparison.Ordinal);

        var lastPage = await officerClient.GetStringAsync("/Event?page=6", ct);
        Assert.Contains("Page 6 of 6", lastPage, StringComparison.Ordinal);
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
        var service = EventDraftTestSupport.Service(context, actorId, EventDraftTestSupport.DraftClock);

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
        Assert.Equal(EventDraftTestSupport.DraftClock.GetUtcNow().UtcDateTime, created.CreatedAt);
        Assert.Equal(new DateTime(2026, 12, 10, 1, 0, 0), created.StartsAt);
        Assert.Equal(new DateTime(2026, 12, 9, 9, 30, 0), created.RegistrationDeadline);
        Assert.Null(created.UpdatedBy);
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(log => log.EntityType == "events", ct));
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
            eventId = await EventDraftTestSupport.Service(seed, actorId, EventDraftTestSupport.DraftClock).CreateDraftAsync(new EventViewModel
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
        var service = EventDraftTestSupport.Service(context, actorId, EventDraftTestSupport.DraftClock);

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
            item => item.EntityType == "events" && item.EntityId == eventId && item.Action == AuditActions.EventUpdated, ct);
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
    // December fixtures must remain in the future regardless of the machine's date.
    public static readonly TimeProvider DraftClock = new FixedDraftClock();

    private sealed class FixedDraftClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 19, 23, 0, 0, TimeSpan.Zero);
    }

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
        Func<IActivityLogWriter, IActivityLogWriter>? wrapAudit = null,
        IEnumerable<IEventCompletionHandler>? completionHandlers = null,
        Func<IUnitOfWork, IEnumerable<IEventCompletionHandler>>? completionHandlerFactory = null,
        IPrivateFileStore? files = null)
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
            completionHandlerFactory?.Invoke(unitOfWork) ?? completionHandlers ?? Array.Empty<IEventCompletionHandler>(),
            unitOfWork,
            Mapper,
            timeProvider,
            audit,
            actor,
            files ?? new UnusedPrivateFileStore(),
            NullLogger<EventLifecycleService>.Instance);
    }

    public static async Task<int> SeedActorAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var actor = new User
        {
            Name = "Event Draft Tester",
            Email = $"{Guid.NewGuid():N}@example.invalid",
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
            Email = officerEmail,
            Name = "Event Draft Officer",
            Password = hasher.Hash(password),
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        var member = new User
        {
            Email = memberEmail,
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
        return (ordered[..5], ordered[25..]);
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
