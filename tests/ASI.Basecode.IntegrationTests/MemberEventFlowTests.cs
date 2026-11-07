using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class MemberEventBrowsingFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task MemberBrowsesOnlyPublishedEventsInStableOrderWithManilaTimesAndPaging()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var memberEmail = $"browse-member-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);

        var start = new DateTime(2099, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var published = new List<Event>();
        for (var index = 1; index <= 26; index++)
        {
            // Two rows share a start so the event id tiebreak decides their order.
            var startsAt = start.AddHours(index <= 25 ? index : 25);
            published.Add(await MemberEventTestSupport.SeedEventAsync(
                fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
                startsAt, startsAt.AddHours(-1), 10, null, $"Published {index:D2}", ct));
        }

        var closed = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            10, null, "Closed event", ct);
        var draft = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Draft,
            new DateTime(2099, 12, 11, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Draft event", ct);
        var cancelled = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Cancelled,
            new DateTime(2099, 12, 12, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 11, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Cancelled event", ct);
        var completed = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Completed,
            new DateTime(2099, 12, 13, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 12, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Completed event", ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var anonymous = await client.GetAsync("/Events", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymous), StringComparison.Ordinal);

        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var firstPage = await client.GetStringAsync("/Events", ct);
        Assert.Contains("Page 1 of 2", firstPage, StringComparison.Ordinal);
        Assert.Contains("2099-12-01 09:00 Asia/Manila", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft event", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelled event", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Completed event", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-event-id=\"{draft.EventId}\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-event-id=\"{cancelled.EventId}\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-event-id=\"{completed.EventId}\"", firstPage, StringComparison.Ordinal);
        Assert.Contains(">Events</a>", firstPage, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", firstPage, StringComparison.Ordinal);

        var firstPageIds = EventIds(firstPage);
        Assert.Equal(25, firstPageIds.Length);
        Assert.Equal(
            published.OrderBy(item => item.StartsAt).ThenBy(item => item.EventId).Take(25).Select(item => item.EventId),
            firstPageIds);

        var secondPage = await client.GetStringAsync("/Events?page=2", ct);
        Assert.Contains("Page 2 of 2", secondPage, StringComparison.Ordinal);
        Assert.Equal(2, EventIds(secondPage).Length);
        Assert.Contains("Closed event", secondPage, StringComparison.Ordinal);
        Assert.Contains("Signup closed", secondPage, StringComparison.Ordinal);

        foreach (var invalidPage in new[] { "0", "-1", "not-a-number" })
        {
            var page = await client.GetStringAsync($"/Events?page={invalidPage}", ct);
            Assert.Equal(firstPageIds, EventIds(page));
        }

        var pastEnd = await client.GetStringAsync("/Events?page=999999999", ct);
        Assert.Equal(2, EventIds(pastEnd).Length);
        Assert.Contains($"data-event-id=\"{closed.EventId}\"", pastEnd, StringComparison.Ordinal);
    }

    private static int[] EventIds(string html) =>
        Regex.Matches(html, "data-event-id=\"(\\d+)\"")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
}

public sealed class MemberEventCoverFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task MemberSeesOnlyAuthorizedCoversAndDraftsAreHidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var memberEmail = $"cover-member-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        var publishedCover = await MemberEventTestSupport.SaveCoverAsync(factory, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46], ct);
        var draftCover = await MemberEventTestSupport.SaveCoverAsync(factory, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46], ct);
        var published = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, publishedCover, "Covered event", ct);
        var draft = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Draft,
            new DateTime(2099, 12, 11, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc),
            10, draftCover, "Hidden draft", ct);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var feed = await client.GetStringAsync("/Events", ct);
        Assert.Contains($"src=\"/EventCover/Image?id={published.EventId}\"", feed, StringComparison.Ordinal);
        Assert.DoesNotContain("uploads/events/", feed, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden draft", feed, StringComparison.Ordinal);

        var cover = await client.GetAsync($"/EventCover/Image?id={published.EventId}", ct);
        Assert.Equal(HttpStatusCode.OK, cover.StatusCode);
        Assert.Equal("image/jpeg", cover.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 }, await cover.Content.ReadAsByteArrayAsync(ct));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/EventCover/Image?id={draft.EventId}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/MemberEvents/Details?id={draft.EventId}", ct)).StatusCode);
    }
}

public sealed class MemberEventEmptyFeedTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task PublishedFeedShowsTheEmptyStateWhenNoEventIsPublished()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var memberEmail = $"empty-member-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);
        await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Draft,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Draft only event", ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var page = await client.GetStringAsync("/Events", ct);
        Assert.Contains("No published events are available yet.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("data-event-id=", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft only event", page, StringComparison.Ordinal);
    }
}

public sealed class MemberEventRegistrationFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task MemberRegistersOnceWithSnapshotAndDuplicateIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var memberEmail = $"register-member-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct, course: "BSIT", yearLevel: 3, section: "C");
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Registration event", ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var detail = await client.GetStringAsync($"/MemberEvents/Details?id={eventItem.EventId}", ct);
        Assert.Contains("Submit registration", detail, StringComparison.Ordinal);
        Assert.Contains("2099-12-10 09:00 Asia/Manila", detail, StringComparison.Ordinal);

        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/MemberEvents/Details?id={eventItem.EventId}", ct);
        var submitted = await client.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);
        Assert.Equal($"/MemberEvents/Details?id={eventItem.EventId}", MemberEventTestSupport.LocationPath(submitted));

        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var registration = await context.Registrations.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
            Assert.Equal(member.MemberId, registration.MemberId);
            Assert.Equal(RegistrationStatusCode.Pending, registration.RegistrationStatusId);
            Assert.Equal("BSIT", registration.CourseSnapshot);
            Assert.Equal(3, registration.YearLevelSnapshot);
            Assert.Equal("C", registration.SectionSnapshot);
            Assert.Null(registration.QrToken);
            Assert.Null(registration.ConfirmedBy);
            Assert.Null(registration.ConfirmedAt);
            Assert.Equal(memberUser.Id, registration.CreatedBy);
        }

        var registeredDetail = await client.GetStringAsync($"/MemberEvents/Details?id={eventItem.EventId}", ct);
        Assert.Contains("Registration submitted. Your request is pending review.", registeredDetail, StringComparison.Ordinal);
        Assert.Contains("You already have a registration for this event.", registeredDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("Submit registration", registeredDetail, StringComparison.Ordinal);

        var duplicateToken = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/MemberEvents/Details?id={eventItem.EventId}", ct);
        var duplicate = await client.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = duplicateToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, duplicate.StatusCode);
        var duplicatePage = await client.GetStringAsync(MemberEventTestSupport.LocationPath(duplicate), ct);
        Assert.Contains("You already have a registration for this event.", duplicatePage, StringComparison.Ordinal);

        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(1, await context.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
        }
    }

    [Fact]
    public async Task FullEventStillCreatesPendingAndWarnsThatNoSeatIsGuaranteed()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var memberEmail = $"full-member-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            1, null, "Full event", ct);
        var otherUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"full-other-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var otherMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, otherUser.Id, MembershipStatusCode.Active, ct);
        await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, otherMember.MemberId, RegistrationStatusCode.Confirmed, memberUser.Id, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var detail = await client.GetStringAsync($"/MemberEvents/Details?id={eventItem.EventId}", ct);
        Assert.Contains("This event is full. You can still submit a registration, but a seat is not guaranteed.", detail, StringComparison.Ordinal);
        Assert.Contains("Submit registration", detail, StringComparison.Ordinal);

        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/MemberEvents/Details?id={eventItem.EventId}", ct);
        var submitted = await client.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);

        var page = await client.GetStringAsync(MemberEventTestSupport.LocationPath(submitted), ct);
        Assert.Contains("This event is full, so a seat is not guaranteed.", page, StringComparison.Ordinal);

        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var registration = await context.Registrations.AsNoTracking().SingleAsync(
                row => row.EventId == eventItem.EventId && row.MemberId != otherMember.MemberId, ct);
            Assert.Equal(RegistrationStatusCode.Pending, registration.RegistrationStatusId);
        }
    }

    [Fact]
    public async Task ClosedEventIneligibleMemberMissingEventAndForgedPostLeaveNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var memberEmail = $"closed-member-{suffix}@attendta.test";
        var ineligibleEmail = $"ineligible-member-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var ineligiblePassword = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var ineligibleUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, ineligibleEmail, ineligiblePassword, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, ineligibleUser.Id, MembershipStatusCode.Inactive, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);
        var closed = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            10, null, "Closed registration event", ct);
        var open = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 11, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Open registration event", ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var ineligibleClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(memberClient, memberEmail, password, ct);
        await MemberEventTestSupport.SignInAsync(ineligibleClient, ineligibleEmail, ineligiblePassword, ct);

        var closedToken = await MemberEventTestSupport.AntiforgeryTokenAsync(memberClient, $"/MemberEvents/Details?id={closed.EventId}", ct);
        var closedResponse = await memberClient.PostAsync($"/MemberEvents/Register?id={closed.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = closedToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, closedResponse.StatusCode);
        Assert.Equal("/Events", MemberEventTestSupport.LocationPath(closedResponse));
        var closedPage = await memberClient.GetStringAsync("/Events", ct);
        Assert.Contains("Signup for this event is closed.", closedPage, StringComparison.Ordinal);

        var missingToken = await MemberEventTestSupport.AntiforgeryTokenAsync(memberClient, "/Events", ct);
        var missing = await memberClient.PostAsync("/MemberEvents/Register?id=999999",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = missingToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, missing.StatusCode);
        Assert.Equal("/Events", MemberEventTestSupport.LocationPath(missing));

        var forged = await memberClient.PostAsync($"/MemberEvents/Register?id={open.EventId}", new FormUrlEncodedContent([]), ct);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);

        var ineligibleToken = await MemberEventTestSupport.AntiforgeryTokenAsync(ineligibleClient, $"/MemberEvents/Details?id={open.EventId}", ct);
        var ineligible = await ineligibleClient.PostAsync($"/MemberEvents/Register?id={open.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = ineligibleToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, ineligible.StatusCode);
        Assert.Equal("/Events", MemberEventTestSupport.LocationPath(ineligible));
        var ineligiblePage = await ineligibleClient.GetStringAsync("/Events", ct);
        Assert.Contains("An active membership is required to register.", ineligiblePage, StringComparison.Ordinal);

        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(0, await context.Registrations.CountAsync(
                row => row.EventId == closed.EventId || row.EventId == open.EventId, ct));
        }
    }

    [Fact]
    public async Task StaleDetailFormIsRefusedWhenTheEventIsCancelledBeforeSubmission()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var memberEmail = $"stale-event-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Stale event", ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var detail = await client.GetStringAsync($"/MemberEvents/Details?id={eventItem.EventId}", ct);
        Assert.Contains("Submit registration", detail, StringComparison.Ordinal);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/MemberEvents/Details?id={eventItem.EventId}", ct);

        await MemberEventTestSupport.ExecuteSqlAsync(fixture.ConnectionString,
            $"UPDATE dbo.events SET event_status_id = {(byte)EventStatusCode.Cancelled} WHERE event_id = {eventItem.EventId}", ct);

        var response = await client.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Events", MemberEventTestSupport.LocationPath(response));
        var page = await client.GetStringAsync("/Events", ct);
        Assert.Contains("Signup for this event is closed.", page, StringComparison.Ordinal);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(0, await observer.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
    }

    [Fact]
    public async Task StaleDetailFormIsRefusedWhenMembershipIsDeactivatedBeforeSubmission()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var memberEmail = $"stale-member-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Stale membership event", ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var detail = await client.GetStringAsync($"/MemberEvents/Details?id={eventItem.EventId}", ct);
        Assert.Contains("Submit registration", detail, StringComparison.Ordinal);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/MemberEvents/Details?id={eventItem.EventId}", ct);

        await MemberEventTestSupport.ExecuteSqlAsync(fixture.ConnectionString,
            $"UPDATE dbo.members SET membership_status_id = {(byte)MembershipStatusCode.Inactive} WHERE user_id = {memberUser.Id}", ct);

        var response = await client.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Events", MemberEventTestSupport.LocationPath(response));
        var page = await client.GetStringAsync("/Events", ct);
        Assert.Contains("An active membership is required to register.", page, StringComparison.Ordinal);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(0, await observer.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
    }

    [Theory]
    [InlineData(RoleCode.Officer)]
    [InlineData(RoleCode.Admin)]
    public async Task OfficerAndAdministratorWithMemberProfilesCanRegister(RoleCode role)
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"role-{role}-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, role, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, $"Role event {role}", ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);

        var feed = await client.GetStringAsync("/Events", ct);
        Assert.Contains("Browse events", feed, StringComparison.Ordinal);

        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/MemberEvents/Details?id={eventItem.EventId}", ct);
        var submitted = await client.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(1, await context.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
    }
}

public sealed class MemberEventDeadlineFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task StaleFormIsRefusedAtOrAfterDeadlineWithoutChangingEarlierRegistration(int offsetTicks)
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var password = MemberEventTestSupport.NewPassword();
        var earlyEmail = $"early-{suffix}@attendta.test";
        var staleEmail = $"stale-deadline-{suffix}@attendta.test";
        var earlyUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, earlyEmail, password, RoleCode.Member, ct);
        var earlyMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, earlyUser.Id, MembershipStatusCode.Active, ct);
        var staleUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, staleEmail, password, RoleCode.Member, ct);
        var staleMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, staleUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, earlyUser.Id, ct);
        // datetime2(3) can represent this deadline exactly; only the injected clock varies by ticks.
        var deadline = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, earlyUser.Id, typeId, venueId,
            EventStatusCode.Published, deadline.AddDays(1), deadline, 10, null, $"Deadline event {suffix}", ct);
        var clock = new MutableClock { Now = deadline.AddTicks(-1) };

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        await using var clockFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var earlyClient = clockFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var staleClient = clockFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(earlyClient, earlyEmail, password, ct);
        await MemberEventTestSupport.SignInAsync(staleClient, staleEmail, password, ct);

        var detailUrl = $"/MemberEvents/Details?id={eventItem.EventId}";
        var detail = await staleClient.GetStringAsync(detailUrl, ct);
        Assert.Contains("Submit registration", detail, StringComparison.Ordinal);
        Assert.Contains("2026-11-06 20:00 Asia/Manila", detail, StringComparison.Ordinal);
        var staleToken = await MemberEventTestSupport.AntiforgeryTokenAsync(staleClient, detailUrl, ct);
        var earlyToken = await MemberEventTestSupport.AntiforgeryTokenAsync(earlyClient, detailUrl, ct);
        var earlyResponse = await earlyClient.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = earlyToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, earlyResponse.StatusCode);
        Assert.Equal(detailUrl, MemberEventTestSupport.LocationPath(earlyResponse));

        int earlierId;
        await using (var observer = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(deadline, (await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct)).RegistrationDeadline);
            var earlier = await observer.Registrations.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
            Assert.Equal(earlyMember.MemberId, earlier.MemberId);
            Assert.Equal(RegistrationStatusCode.Pending, earlier.RegistrationStatusId);
            earlierId = earlier.RegistrationId;
        }

        clock.Now = deadline.AddTicks(offsetTicks);
        var response = await staleClient.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = staleToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Events", MemberEventTestSupport.LocationPath(response));
        var feed = await staleClient.GetStringAsync("/Events", ct);
        Assert.Contains("Signup for this event is closed.", feed, StringComparison.Ordinal);
        var card = Regex.Match(feed, $"<article[^>]*data-event-id=\"{eventItem.EventId}\"[^>]*>.*?</article>", RegexOptions.Singleline);
        Assert.True(card.Success, "The published event must remain in the feed.");
        Assert.Contains("Signup closed", card.Value, StringComparison.Ordinal);
        Assert.Contains("Signup deadline", card.Value, StringComparison.Ordinal);
        Assert.Contains("2026-11-06 20:00 Asia/Manila", card.Value, StringComparison.Ordinal);
        var closedDetail = await staleClient.GetStringAsync(detailUrl, ct);
        Assert.Contains("Signup closed on 2026-11-06 20:00 Asia/Manila", closedDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("Submit registration", closedDetail, StringComparison.Ordinal);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var registrations = await context.Registrations.AsNoTracking().Where(row => row.EventId == eventItem.EventId).ToListAsync(ct);
        var preserved = Assert.Single(registrations);
        Assert.Equal(earlierId, preserved.RegistrationId);
        Assert.Equal(earlyMember.MemberId, preserved.MemberId);
        Assert.NotEqual(staleMember.MemberId, preserved.MemberId);
        Assert.Equal(RegistrationStatusCode.Pending, preserved.RegistrationStatusId);
        Assert.Equal(EventStatusCode.Published, (await context.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct)).EventStatusId);
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTime Now { get; set; }
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
    }
}

public sealed class MemberEventRegistrationPersistenceTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task RegistrationLocksTheUserThenTheEventAndPersistsThePendingSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"lock-member-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct, course: "BSIT", yearLevel: 2, section: "B");
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Lock event", ct);

        var recorder = new EventDraftPersistenceTests.LockOrderRecorder();
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, recorder);
        var service = MemberEventTestSupport.Service(context, user.Id, RoleCodes.Member, TimeProvider.System);

        var result = await service.RegisterAsync(eventItem.EventId, ct);

        Assert.Equal(2, recorder.Locks.Count);
        Assert.Contains("users", recorder.Locks[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("events", recorder.Locks[1], StringComparison.OrdinalIgnoreCase);
        Assert.False(result.SeatNotGuaranteed);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var registration = await observer.Registrations.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(member.MemberId, registration.MemberId);
        Assert.Equal(RegistrationStatusCode.Pending, registration.RegistrationStatusId);
        Assert.Equal("BSIT", registration.CourseSnapshot);
        Assert.Equal(2, registration.YearLevelSnapshot);
        Assert.Equal("B", registration.SectionSnapshot);
        Assert.Equal(user.Id, registration.CreatedBy);
    }

    [Fact]
    public async Task ExactDeadlineIsClosedAndOneMillisecondBeforeIsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"deadline-member-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var atDeadline = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            now.AddDays(1), now, 10, null, "At deadline", ct);
        var justOpen = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            now.AddDays(1), now.AddMilliseconds(1), 10, null, "Just open", ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = MemberEventTestSupport.Service(context, user.Id, RoleCodes.Member, new FixedClock(now));

        var error = await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync(atDeadline.EventId, ct));
        Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
        await service.RegisterAsync(justOpen.EventId, ct);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(0, await observer.Registrations.CountAsync(row => row.EventId == atDeadline.EventId, ct));
        Assert.Equal(1, await observer.Registrations.CountAsync(row => row.EventId == justOpen.EventId, ct));
    }

    [Fact]
    public async Task CancelledRegistrationStillRefusesANewSubmission()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"cancelled-member-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Cancelled registration event", ct);
        await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Cancelled, user.Id, ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = MemberEventTestSupport.Service(context, user.Id, RoleCodes.Member, TimeProvider.System);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync(eventItem.EventId, ct));

        Assert.Equal(OutcomeCode.Duplicate, error.Outcome);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(1, await observer.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
    }

    [Fact]
    public async Task ConcurrentSubmissionsFromParallelConnectionsCreateExactlyOneRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"race-member-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Race event", ct);

        await using var firstContext = MemberEventTestSupport.Context(fixture.ConnectionString);
        await using var secondContext = MemberEventTestSupport.Context(fixture.ConnectionString);
        var firstService = MemberEventTestSupport.Service(firstContext, user.Id, RoleCodes.Member, TimeProvider.System);
        var secondService = MemberEventTestSupport.Service(secondContext, user.Id, RoleCodes.Member, TimeProvider.System);

        var results = await Task.WhenAll(
            CaptureAsync(firstService.RegisterAsync(eventItem.EventId, ct)),
            CaptureAsync(secondService.RegisterAsync(eventItem.EventId, ct)));

        Assert.Single(results, result => result == null);
        var duplicate = Assert.Single(results, result => result != null);
        Assert.Equal(OutcomeCode.Duplicate, duplicate!.Outcome);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(1, await observer.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
    }

    [Fact]
    public async Task MembershipDeactivationBeforeTheUserLockIsRefusedWithoutAPartialRegistration()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"membership-race-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Membership race event", ct);

        var gate = new MemberEventTestSupport.LockGateInterceptor("users");
        await using var registrationContext = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var service = MemberEventTestSupport.Service(registrationContext, user.Id, RoleCodes.Member, TimeProvider.System);

        await using var changer = await MemberEventTestSupport.BeginChangerAsync(fixture.ConnectionString, ct);
        await changer.LockAsync("SELECT user_id FROM dbo.users WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE user_id = @id", ct, ("@id", user.Id));
        await changer.ExecuteAsync("UPDATE dbo.members SET membership_status_id = @status WHERE user_id = @id", ct,
            ("@status", (byte)MembershipStatusCode.Inactive), ("@id", user.Id));

        var registration = service.RegisterAsync(eventItem.EventId, ct);
        try
        {
            // The registration is paused immediately before its users lock; commit the deactivation first.
            await gate.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);
            await changer.CommitAsync(ct);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        var error = await Assert.ThrowsAsync<DomainException>(() => registration);
        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.Empty(registrationContext.ChangeTracker.Entries<Registration>());

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(0, await observer.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
        Assert.Equal(MembershipStatusCode.Inactive,
            (await observer.Members.AsNoTracking().SingleAsync(row => row.UserId == user.Id, ct)).MembershipStatusId);
    }

    [Fact]
    public async Task EventCancellationBeforeTheEventLockIsRefusedWithoutAPartialRegistration()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"event-race-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Event race event", ct);

        var gate = new MemberEventTestSupport.LockGateInterceptor("events");
        await using var registrationContext = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var service = MemberEventTestSupport.Service(registrationContext, user.Id, RoleCodes.Member, TimeProvider.System);

        await using var changer = await MemberEventTestSupport.BeginChangerAsync(fixture.ConnectionString, ct);
        await changer.LockAsync("SELECT event_id FROM dbo.events WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE event_id = @id", ct, ("@id", eventItem.EventId));
        await changer.ExecuteAsync("UPDATE dbo.events SET event_status_id = @status WHERE event_id = @id", ct,
            ("@status", (byte)EventStatusCode.Cancelled), ("@id", eventItem.EventId));

        var registration = service.RegisterAsync(eventItem.EventId, ct);
        try
        {
            // The registration is paused immediately before its events lock; commit the cancellation first.
            await gate.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);
            await changer.CommitAsync(ct);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        var error = await Assert.ThrowsAsync<DomainException>(() => registration);
        Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
        Assert.Empty(registrationContext.ChangeTracker.Entries<Registration>());

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(0, await observer.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
        Assert.Equal(EventStatusCode.Cancelled,
            (await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct)).EventStatusId);
    }

    [Fact]
    public async Task DeadlineChangeBeforeTheEventLockIsRefusedWithoutAPartialRegistration()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"deadline-race-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Deadline race event", ct);
        var pastDeadline = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var gate = new MemberEventTestSupport.LockGateInterceptor("events");
        await using var registrationContext = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var service = MemberEventTestSupport.Service(registrationContext, user.Id, RoleCodes.Member, TimeProvider.System);

        await using var changer = await MemberEventTestSupport.BeginChangerAsync(fixture.ConnectionString, ct);
        await changer.LockAsync("SELECT event_id FROM dbo.events WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE event_id = @id", ct, ("@id", eventItem.EventId));
        await changer.ExecuteAsync("UPDATE dbo.events SET registration_deadline = @deadline WHERE event_id = @id", ct,
            ("@deadline", pastDeadline), ("@id", eventItem.EventId));

        var registration = service.RegisterAsync(eventItem.EventId, ct);
        try
        {
            // The registration is paused immediately before its events lock; commit the deadline change first.
            await gate.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);
            await changer.CommitAsync(ct);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        var error = await Assert.ThrowsAsync<DomainException>(() => registration);
        Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
        Assert.Empty(registrationContext.ChangeTracker.Entries<Registration>());

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(0, await observer.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
        Assert.Equal(pastDeadline,
            (await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct)).RegistrationDeadline);
    }

    [Fact]
    public async Task UniqueKeyFallbackTranslatesADuplicateThatBypassesTheExistencePreCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"unique-key-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Unique key event", ct);
        await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Pending, user.Id, ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = MemberEventTestSupport.Service(context, user.Id, RoleCodes.Member, TimeProvider.System,
            registrations => new StaleExistenceRegistrationRepository(registrations));

        var error = await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync(eventItem.EventId, ct));

        Assert.Equal(OutcomeCode.Duplicate, error.Outcome);
        Assert.Equal("You already have a registration for this event.", error.Message);
        Assert.Empty(context.ChangeTracker.Entries<Registration>());

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = Assert.Single(await observer.Registrations.AsNoTracking()
            .Where(registration => registration.EventId == eventItem.EventId)
            .ToListAsync(ct));
        Assert.Equal(member.MemberId, row.MemberId);
        Assert.Equal(RegistrationStatusCode.Pending, row.RegistrationStatusId);
    }

    private static async Task<DomainException?> CaptureAsync(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (DomainException exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Deliberately reports no existing registration so the insert reaches the database unique key
    /// instead of being short-circuited by the service's pre-check.
    /// </summary>
    private sealed class StaleExistenceRegistrationRepository(IRegistrationRepository inner) : IRegistrationRepository
    {
        public Task<RegistrationReviewIdentity> DiscoverReviewAsync(int id, CancellationToken ct = default) => inner.DiscoverReviewAsync(id, ct);
        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) => inner.DiscoverPromotionCandidateAsync(eventId, ct);
        public Task<bool> HasEligibleWaitlistAsync(int id, CancellationToken ct = default) => inner.HasEligibleWaitlistAsync(id, ct);
        public Task<bool> HasValidAttendanceAsync(int id, CancellationToken ct = default) => inner.HasValidAttendanceAsync(id, ct);
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken ct = default) => inner.HasValidAttendanceForEventAsync(eventId, ct);
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken ct = default) => inner.HasIssuedCertificateForEventAsync(eventId, ct);
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken ct = default) => inner.CountReviewQueueAsync(status, eventId, ct);
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken ct = default) => inner.GetReviewQueueAsync(status, eventId, offset, limit, ct);
        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken ct = default) => inner.GetMyPassRowsAsync(userId, ct);
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken ct = default) => inner.GetOwnedPassAsync(registrationId, userId, ct);
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken ct = default) => inner.FindConfirmedRegistrationIdByTokenAsync(eventId, token, ct);
        public Task<Registration> GetForCancellationAsync(int id, CancellationToken ct = default) => inner.GetForCancellationAsync(id, ct);
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int id, CancellationToken ct = default) => inner.GetOpenForEventAsync(id, ct);
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int id, CancellationToken ct = default) => inner.DiscoverOpenEventIdsForUserAsync(id, ct);
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int id, DateTime now, int? eventId = null, CancellationToken ct = default) => inner.GetUpcomingForUserAsync(id, now, eventId, ct);
        public Task<bool> IsConfirmedAsync(int registrationId, CancellationToken cancellationToken = default) =>
            inner.IsConfirmedAsync(registrationId, cancellationToken);

        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default) =>
            inner.GetMemberRegistrationAsync(eventId, userId, cancellationToken);

        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken cancellationToken = default) =>
            inner.CountMyRegistrationsAsync(userId, cancellationToken);

        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken cancellationToken = default) =>
            inner.GetMyRegistrationsAsync(userId, skip, take, cancellationToken);

        public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) =>
            inner.CountConfirmedAsync(eventId, cancellationToken);

        public Task AddAsync(Registration registration, CancellationToken cancellationToken = default) =>
            inner.AddAsync(registration, cancellationToken);
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}

internal static class MemberEventTestSupport
{
    public static readonly IMapper Mapper = new MapperConfiguration(config =>
    {
        config.CreateMap<PublishedEventListItem, MemberEventListItemViewModel>();
        config.CreateMap<PublishedEventDetail, MemberEventDetailViewModel>();
    }).CreateMapper();

    public static AsiBasecodeDBContext Context(string connectionString, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(connectionString)
            .AddInterceptors(interceptors).Options);

    /// <summary>
    /// Generates a fresh password for one test. The value is never committed and satisfies the
    /// hasher's non-empty requirement plus the usual upper/lower/digit/symbol policy.
    /// </summary>
    public static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";

    public static async Task ExecuteSqlAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static Task<SqlChanger> BeginChangerAsync(string connectionString, CancellationToken cancellationToken) =>
        SqlChanger.BeginAsync(connectionString, cancellationToken);

    public static MemberEventService Service(
        AsiBasecodeDBContext context,
        int actorUserId,
        string role,
        TimeProvider clock,
        Func<IRegistrationRepository, IRegistrationRepository>? wrapRegistrations = null)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorUserId, role);
        IRegistrationRepository registrations = new RegistrationRepository(unitOfWork);
        if (wrapRegistrations != null)
        {
            registrations = wrapRegistrations(registrations);
        }

        return new MemberEventService(
            new EventRepository(unitOfWork),
            registrations,
            new MemberRepository(unitOfWork),
            new UserRepository(unitOfWork),
            unitOfWork,
            clock,
            actor,
            new RegistrationCancellationService(
                registrations,
                new EventRepository(unitOfWork),
                unitOfWork,
                actor,
                new ActivityLogWriter(context, actor, clock),
                clock),
            Mapper);
    }

    public static async Task<User> SeedUserAsync(string connectionString, string email, string password, RoleCode role, CancellationToken cancellationToken)
    {
        var hasher = new Argon2PasswordHasher();
        await using var context = Context(connectionString);
        var user = new User
        {
            UserId = email,
            Name = $"Member event {role}",
            Password = hasher.Hash(password),
            RoleId = role,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public static async Task<Member> SeedMemberAsync(
        string connectionString,
        int userId,
        MembershipStatusCode status,
        CancellationToken cancellationToken,
        string course = "BSIT",
        int yearLevel = 1,
        string section = "A")
    {
        await using var context = Context(connectionString);
        var member = new Member
        {
            UserId = userId,
            StudentNumber = $"ME-{Guid.NewGuid():N}",
            Course = course,
            YearLevel = yearLevel,
            Section = section,
            MembershipStatusId = status,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(cancellationToken);
        return member;
    }

    public static async Task<(int TypeId, int VenueId)> SeedReferencesAsync(string connectionString, int actorId, CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var eventType = new EventType
        {
            TypeName = $"Member event type {Guid.NewGuid():N}",
            Description = "Member event test type",
            IsActive = true,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        var venue = new Venue
        {
            RoomName = $"Member event room {Guid.NewGuid():N}",
            Capacity = 50,
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

    public static async Task<Event> SeedEventAsync(
        string connectionString,
        int actorId,
        int typeId,
        int venueId,
        EventStatusCode status,
        DateTime startsAt,
        DateTime deadline,
        int participantLimit,
        string? imagePath,
        string title,
        CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var eventItem = new Event
        {
            Title = title,
            Description = $"About {title}",
            TypeId = typeId,
            VenueId = venueId,
            StartsAt = startsAt,
            RegistrationDeadline = deadline,
            ParticipantLimit = participantLimit,
            EventStatusId = status,
            ImagePath = imagePath,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        context.Events.Add(eventItem);
        await context.SaveChangesAsync(cancellationToken);
        return eventItem;
    }

    public static async Task<Registration> SeedRegistrationAsync(
        string connectionString,
        int eventId,
        int memberId,
        RegistrationStatusCode status,
        int actorId,
        CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var registration = new Registration
        {
            EventId = eventId,
            MemberId = memberId,
            RegistrationStatusId = status,
            CourseSnapshot = "BSIT",
            YearLevelSnapshot = 1,
            SectionSnapshot = "A",
            SubmittedAt = DateTime.UtcNow,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        context.Registrations.Add(registration);
        await context.SaveChangesAsync(cancellationToken);
        return registration;
    }

    public static async Task<string> SaveCoverAsync(AttendTaWebApplicationFactory factory, byte[] bytes, CancellationToken cancellationToken)
    {
        var relativePath = $"uploads/events/{Guid.NewGuid():N}.jpg";
        var fullPath = Path.Combine(factory.SetupRoot, "uploads", "events", Path.GetFileName(relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
        return relativePath;
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

    private sealed class Actor(int userId, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => userId;
        public string Role => role;
    }

    /// <summary>
    /// Holds a competing SQL transaction open so a test can commit a membership or event change
    /// before the registration transaction acquires its next lock.
    /// </summary>
    internal sealed class SqlChanger : IAsyncDisposable
    {
        private readonly SqlConnection connection;
        private readonly SqlTransaction transaction;

        private SqlChanger(SqlConnection connection, SqlTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public static async Task<SqlChanger> BeginAsync(string connectionString, CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            return new SqlChanger(connection, transaction);
        }

        public async Task LockAsync(string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
        {
            await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 120 };
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            await command.ExecuteScalarAsync(cancellationToken);
        }

        public async Task ExecuteAsync(string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
        {
            await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 120 };
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Pauses a registration command immediately before it executes its <c>UPDLOCK</c> statement
    /// for the named table, so the test can commit a competing change first.
    /// </summary>
    internal sealed class LockGateInterceptor(string tableName) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Attempted => attempted.Task;
        public TaskCompletionSource Release => release;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains(tableName, StringComparison.OrdinalIgnoreCase))
            {
                attempted.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return result;
        }
    }
}
