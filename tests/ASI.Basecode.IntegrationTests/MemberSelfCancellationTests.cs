using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// SQL-backed coverage for the member-owned cancellation command: the HTTP surface on the published
/// event detail page, the server guards behind it, and the audit and re-registration consequences.
/// </summary>
public sealed class MemberSelfCancellationTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task MemberCancelsConfirmedRegistrationAndReRegistrationIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"self-cancel-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Self cancellation event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);
        await SetQrTokenAsync(registration.RegistrationId, new string('a', 64), ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);

        var detailUrl = $"/MemberEvents/Details?id={eventItem.EventId}";
        var detail = await client.GetStringAsync(detailUrl, ct);
        Assert.Contains("Your registration is <span class=\"badge badge-open\">Confirmed</span>.", detail, StringComparison.Ordinal);
        Assert.Contains("Cancel my registration", detail, StringComparison.Ordinal);
        Assert.Contains("Cancellation is final.", detail, StringComparison.Ordinal);
        Assert.Contains("Keep my registration", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Submit registration", detail, StringComparison.Ordinal);

        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, detailUrl, ct);
        var cancelled = await client.PostAsync($"/MemberEvents/Cancel?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        Assert.Equal(detailUrl, MemberEventTestSupport.LocationPath(cancelled));

        var afterCancel = await client.GetStringAsync(detailUrl, ct);
        Assert.Contains("Your registration was cancelled. This cannot be undone.", afterCancel, StringComparison.Ordinal);
        Assert.Contains("Your registration is <span class=\"badge badge-inactive\">Cancelled</span>.", afterCancel, StringComparison.Ordinal);
        Assert.Contains("This registration was cancelled. You cannot register again for this event.", afterCancel, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancel my registration", afterCancel, StringComparison.Ordinal);
        Assert.DoesNotContain("Submit registration", afterCancel, StringComparison.Ordinal);

        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var row = await context.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
            Assert.Equal(RegistrationStatusCode.Cancelled, row.RegistrationStatusId);
            Assert.Null(row.QrToken);
            Assert.Equal(user.Id, row.CancelledBy);
            Assert.NotNull(row.CancelledAt);
            Assert.Equal("Member requested cancellation", row.CancellationReason);
            var logs = await context.ActivityLogs.AsNoTracking()
                .Where(log => log.EntityType == "registrations" && log.EntityId == registration.RegistrationId)
                .ToListAsync(ct);
            var log = Assert.Single(logs);
            Assert.Equal("registration.cancelled", log.Action);
            Assert.Equal(user.Id, log.ActorUserId);
            Assert.DoesNotContain("token", log.OldValues!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", log.NewValues!, StringComparison.OrdinalIgnoreCase);
        }

        // The unique (event_id, member_id) key keeps re-registration refused after a cancellation.
        var reRegisterToken = await MetaTokenAsync(client, detailUrl, ct);
        var reRegister = await client.PostAsync($"/MemberEvents/Register?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = reRegisterToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, reRegister.StatusCode);
        Assert.Equal(detailUrl, MemberEventTestSupport.LocationPath(reRegister));
        var reRegisterPage = await client.GetStringAsync(detailUrl, ct);
        Assert.Contains("You already have a registration for this event.", reRegisterPage, StringComparison.Ordinal);

        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            Assert.Equal(1, await context.Registrations.CountAsync(item => item.EventId == eventItem.EventId, ct));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CancellationIsRefusedAtOrAfterStartWithoutChangingTheRow(int offsetTicks)
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"start-cancel-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var startsAt = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            startsAt, startsAt.AddHours(-1), 10, null, $"Started event {suffix}", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);
        var clock = new MutableClock { Now = startsAt.AddTicks(-1) };

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        await using var clockFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = clockFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);

        var detailUrl = $"/MemberEvents/Details?id={eventItem.EventId}";
        var detail = await client.GetStringAsync(detailUrl, ct);
        Assert.Contains("Cancel my registration", detail, StringComparison.Ordinal);
        var token = await MetaTokenAsync(client, detailUrl, ct);

        clock.Now = startsAt.AddTicks(offsetTicks);
        var response = await client.PostAsync($"/MemberEvents/Cancel?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(detailUrl, MemberEventTestSupport.LocationPath(response));

        var page = await client.GetStringAsync(detailUrl, ct);
        Assert.Contains("This event has already started, so the registration can no longer be cancelled.", page, StringComparison.Ordinal);
        Assert.Contains("badge badge-open\">Confirmed</span>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancel my registration", page, StringComparison.Ordinal);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, row.RegistrationStatusId);
        Assert.Null(row.CancelledBy);
        Assert.Empty(await context.ActivityLogs.AsNoTracking()
            .Where(log => log.EntityType == "registrations" && log.EntityId == registration.RegistrationId)
            .ToListAsync(ct));
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Pending)]
    [InlineData(RegistrationStatusCode.Waitlisted)]
    public async Task DetailShowsTheDerivedCutoffBadgeWithoutChangingTheStoredStatus(RegistrationStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"cutoff-badge-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var startsAt = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            startsAt, startsAt.AddHours(-1), 10, null, $"Cutoff badge event {suffix}", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, status, user.Id, ct);
        var clock = new MutableClock { Now = startsAt };

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        await using var clockFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = clockFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);

        var detail = await client.GetStringAsync($"/MemberEvents/Details?id={eventItem.EventId}", ct);

        Assert.Contains("Your registration is <span class=\"badge badge-closed\">Not confirmed before cutoff</span>.", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancel my registration", detail, StringComparison.Ordinal);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(status, row.RegistrationStatusId);
    }

    [Fact]
    public async Task CancellationIsRefusedWithValidAttendanceWithoutChangingTheRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"attended-cancel-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Attended event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);
        await using (var seed = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            seed.Set<Attendance>().Add(new Attendance
            {
                RegistrationId = registration.RegistrationId,
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = user.Id,
                RecordedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync(ct);
        }

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);

        var detailUrl = $"/MemberEvents/Details?id={eventItem.EventId}";
        var detail = await client.GetStringAsync(detailUrl, ct);
        Assert.DoesNotContain("Cancel my registration", detail, StringComparison.Ordinal);
        var token = await MetaTokenAsync(client, detailUrl, ct);

        var response = await client.PostAsync($"/MemberEvents/Cancel?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(detailUrl, MemberEventTestSupport.LocationPath(response));
        var page = await client.GetStringAsync(detailUrl, ct);
        Assert.Contains("Attendance has already been recorded for this registration.", page, StringComparison.Ordinal);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, row.RegistrationStatusId);
        Assert.Null(row.CancelledBy);
        Assert.Empty(await context.ActivityLogs.AsNoTracking()
            .Where(log => log.EntityType == "registrations" && log.EntityId == registration.RegistrationId)
            .ToListAsync(ct));
    }

    [Fact]
    public async Task CancellingAnotherMembersRegistrationIsNotFoundAndLeavesItUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var ownerEmail = $"owner-cancel-{suffix}@attendta.test";
        var intruderEmail = $"intruder-cancel-{suffix}@attendta.test";
        var intruderPassword = MemberEventTestSupport.NewPassword();
        var owner = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, ownerEmail, MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var ownerMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, owner.Id, MembershipStatusCode.Active, ct);
        var intruder = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, intruderEmail, intruderPassword, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, intruder.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, owner.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, owner.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Owned registration event", ct);
        var ownerRegistration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, ownerMember.MemberId, RegistrationStatusCode.Confirmed, owner.Id, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, intruderEmail, intruderPassword, ct);

        var detailUrl = $"/MemberEvents/Details?id={eventItem.EventId}";
        var detail = await client.GetStringAsync(detailUrl, ct);
        Assert.DoesNotContain("Cancel my registration", detail, StringComparison.Ordinal);
        Assert.Contains("Submit registration", detail, StringComparison.Ordinal);
        var token = await MetaTokenAsync(client, detailUrl, ct);

        var response = await client.PostAsync($"/MemberEvents/Cancel?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Events", MemberEventTestSupport.LocationPath(response));
        var feed = await client.GetStringAsync("/Events", ct);
        Assert.Contains("You have no registration for this event.", feed, StringComparison.Ordinal);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == ownerRegistration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, row.RegistrationStatusId);
        Assert.Null(row.CancelledBy);
        Assert.Empty(await context.ActivityLogs.AsNoTracking()
            .Where(log => log.EntityType == "registrations" && log.EntityId == ownerRegistration.RegistrationId)
            .ToListAsync(ct));
    }

    [Fact]
    public async Task CancellationIsStaleWhenTheEventIsNoLongerPublished()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"stale-cancel-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Stale cancellation event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Waitlisted, user.Id, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);

        var detailUrl = $"/MemberEvents/Details?id={eventItem.EventId}";
        var detail = await client.GetStringAsync(detailUrl, ct);
        Assert.Contains("Cancel my registration", detail, StringComparison.Ordinal);
        var token = await MetaTokenAsync(client, detailUrl, ct);

        await MemberEventTestSupport.ExecuteSqlAsync(fixture.ConnectionString,
            $"UPDATE dbo.events SET event_status_id = {(byte)EventStatusCode.Cancelled} WHERE event_id = {eventItem.EventId}", ct);

        var response = await client.PostAsync($"/MemberEvents/Cancel?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Events", MemberEventTestSupport.LocationPath(response));
        var feed = await client.GetStringAsync("/Events", ct);
        Assert.Contains("This event is no longer published, so the registration can no longer be cancelled.", feed, StringComparison.Ordinal);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Waitlisted, row.RegistrationStatusId);
        Assert.Null(row.CancelledBy);
        Assert.Empty(await context.ActivityLogs.AsNoTracking()
            .Where(log => log.EntityType == "registrations" && log.EntityId == registration.RegistrationId)
            .ToListAsync(ct));
    }

    [Fact]
    public async Task AnonymousAndForgedCancellationAreRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"forged-cancel-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Forged cancellation event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var anonymousResponse = await anonymous.PostAsync($"/MemberEvents/Cancel?id={eventItem.EventId}", new FormUrlEncodedContent([]), ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymousResponse), StringComparison.Ordinal);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);
        var forged = await client.PostAsync($"/MemberEvents/Cancel?id={eventItem.EventId}", new FormUrlEncodedContent([]), ct);
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, row.RegistrationStatusId);
        Assert.Null(row.CancelledBy);
        Assert.Empty(await context.ActivityLogs.AsNoTracking()
            .Where(log => log.EntityType == "registrations" && log.EntityId == registration.RegistrationId)
            .ToListAsync(ct));
    }

    [Fact]
    public async Task DeactivatedMembershipCanStillCancelAnExistingRegistration()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"inactive-cancel-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Inactive, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Inactive member cancellation event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Pending, user.Id, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);

        var detailUrl = $"/MemberEvents/Details?id={eventItem.EventId}";
        var detail = await client.GetStringAsync(detailUrl, ct);
        Assert.Contains("Cancel my registration", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Only active members can register for events.", detail, StringComparison.Ordinal);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, detailUrl, ct);

        var response = await client.PostAsync($"/MemberEvents/Cancel?id={eventItem.EventId}",
            MemberEventTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(detailUrl, MemberEventTestSupport.LocationPath(response));

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Cancelled, row.RegistrationStatusId);
        Assert.Equal(user.Id, row.CancelledBy);
    }

    [Fact]
    public async Task CancelLocksTheUserThenTheEventAndWritesOneAuditEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"lock-cancel-{suffix}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, user.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 10, 1, 0, 0, DateTimeKind.Utc), new DateTime(2099, 12, 9, 1, 0, 0, DateTimeKind.Utc),
            10, null, "Lock cancellation event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);
        await SetQrTokenAsync(registration.RegistrationId, new string('b', 64), ct);
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        var recorder = new EventDraftPersistenceTests.LockOrderRecorder();
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, recorder);
        var service = MemberEventTestSupport.Service(context, user.Id, RoleCodes.Member, new FixedClock(now));

        await service.CancelRegistrationAsync(eventItem.EventId, ct);

        Assert.Equal(2, recorder.Locks.Count);
        Assert.Contains("users", recorder.Locks[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("events", recorder.Locks[1], StringComparison.OrdinalIgnoreCase);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await observer.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Cancelled, row.RegistrationStatusId);
        Assert.Null(row.QrToken);
        Assert.Equal(user.Id, row.CancelledBy);
        Assert.Equal(now, row.CancelledAt);
        Assert.Equal("Member requested cancellation", row.CancellationReason);
        var logs = await observer.ActivityLogs.AsNoTracking()
            .Where(log => log.EntityType == "registrations" && log.EntityId == registration.RegistrationId)
            .ToListAsync(ct);
        var log = Assert.Single(logs);
        Assert.Equal("registration.cancelled", log.Action);
        Assert.Equal(user.Id, log.ActorUserId);
    }

    private async Task SetQrTokenAsync(int registrationId, string token, CancellationToken ct)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Registrations.SingleAsync(item => item.RegistrationId == registrationId, ct);
        row.QrToken = token;
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Reads the layout's antiforgery meta tag so a refusal can be posted even when the page renders
    /// no form (the cancel action is hidden).
    /// </summary>
    private static async Task<string> MetaTokenAsync(HttpClient client, string url, CancellationToken ct)
    {
        var page = await client.GetStringAsync(url, ct);
        var token = Regex.Match(page, "name=\"RequestVerificationToken\" content=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private sealed class MutableClock : TimeProvider
    {
        public DateTime Now { get; set; }
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}
