using System.Net;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class CheckInWindowFlagTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Theory]
    [InlineData("Scan")]
    [InlineData("CheckIn")]
    public async Task ClosedCheckInShowsOnlyTheResultCardAfterRedirect(string action)
    {
        var ct = TestContext.Current.CancellationToken;
        var password = MemberEventTestSupport.NewPassword();
        var officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString,
            $"notice-{Guid.NewGuid():N}@example.invalid", password, RoleCode.Officer, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var item = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, typeId, venueId,
            EventStatusCode.Published, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(6), 20, null, "Notice test", ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await MemberEventTestSupport.SignInAsync(client, officer.Email, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/CheckIn?eventId={item.EventId}", ct);
        using var response = await client.PostAsync($"/CheckIn/{action}", MemberEventTestSupport.Form(new()
        {
            ["eventId"] = item.EventId.ToString(), ["registrationId"] = "999", ["token"] = "unknown",
            ["q"] = "unknown", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await client.GetStringAsync(response.Headers.Location!.ToString(), ct);
        Assert.Contains("scanner-outcome-closed", html, StringComparison.Ordinal);
        Assert.Contains("Check-in closed", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#check-in-result\"", html, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\" data-redirect-outcome", html, StringComparison.Ordinal);
        Assert.DoesNotContain("notice-error", html, StringComparison.Ordinal);
        Assert.DoesNotContain("notice-success", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisappearedOrDifferentEventShowsOutcomeAsNotice(bool disappear)
    {
        var ct = TestContext.Current.CancellationToken;
        var password = MemberEventTestSupport.NewPassword();
        var officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString,
            $"fallback-{Guid.NewGuid():N}@example.invalid", password, RoleCode.Officer, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var item = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, typeId, venueId,
            EventStatusCode.Published, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(6), 20, null, "Fallback event", ct);
        var other = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, typeId, venueId,
            EventStatusCode.Published, DateTime.UtcNow.AddDays(8), DateTime.UtcNow.AddDays(6), 20, null, "Other event", ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await MemberEventTestSupport.SignInAsync(client, officer.Email, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/CheckIn?eventId={item.EventId}", ct);
        using var response = await client.PostAsync("/CheckIn/Scan", MemberEventTestSupport.Form(new()
        {
            ["eventId"] = item.EventId.ToString(), ["token"] = "unknown", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        if (disappear)
        {
            await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
            var row = await writer.Events.SingleAsync(e => e.EventId == item.EventId, ct);
            row.EventStatusId = EventStatusCode.Completed;
            await writer.SaveChangesAsync(ct);
        }
        var url = disappear ? response.Headers.Location!.ToString() : $"/CheckIn?eventId={other.EventId}";
        var html = await client.GetStringAsync(url, ct);
        Assert.Contains("notice-error", html, StringComparison.Ordinal);
        Assert.Contains("Check-in is closed for this event", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-redirect-outcome", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManualSuccessAndInvalidationKeepTheirOwnFeedback()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = MemberEventTestSupport.NewPassword();
        var officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString,
            $"manual-{Guid.NewGuid():N}@example.invalid", password, RoleCode.Officer, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var item = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, typeId, venueId,
            EventStatusCode.Published, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(-1), 20, null, "Manual event", ct);
        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Events.SingleAsync(e => e.EventId == item.EventId, ct);
            row.IsCheckInOpen = true;
            row.CheckInOpenedBy = officer.Id;
            row.CheckInOpenedAt = DateTime.UtcNow;
            await writer.SaveChangesAsync(ct);
        }
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString,
            $"participant-{Guid.NewGuid():N}@example.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, item.EventId,
            member.MemberId, RegistrationStatusCode.Confirmed, officer.Id, ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await MemberEventTestSupport.SignInAsync(client, officer.Email, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/CheckIn?eventId={item.EventId}", ct);
        using var response = await client.PostAsync("/CheckIn/CheckIn", MemberEventTestSupport.Form(new()
        {
            ["eventId"] = item.EventId.ToString(), ["registrationId"] = registration.RegistrationId.ToString(),
            ["q"] = member.StudentNumber, ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await client.GetStringAsync(response.Headers.Location!.ToString(), ct);
        Assert.Contains("scanner-outcome-success", html, StringComparison.Ordinal);
        Assert.Contains(member.StudentNumber, html, StringComparison.Ordinal);
        Assert.DoesNotContain("notice-success", html, StringComparison.Ordinal);
        using var invalidation = await client.PostAsync("/CheckIn/Invalidate", MemberEventTestSupport.Form(new()
        {
            ["eventId"] = item.EventId.ToString(), ["registrationId"] = registration.RegistrationId.ToString(),
            ["reason"] = "Test correction", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, invalidation.StatusCode);
        html = await client.GetStringAsync(invalidation.Headers.Location!.ToString(), ct);
        Assert.Contains("notice-success", html, StringComparison.Ordinal);
        Assert.Contains("Attendance invalidated.", html, StringComparison.Ordinal);
        using var repeat = await client.PostAsync("/CheckIn/Invalidate", MemberEventTestSupport.Form(new()
        {
            ["eventId"] = item.EventId.ToString(), ["registrationId"] = registration.RegistrationId.ToString(),
            ["reason"] = "Test correction", ["__RequestVerificationToken"] = token
        }), ct);
        html = await client.GetStringAsync(repeat.Headers.Location!.ToString(), ct);
        Assert.Contains("notice-error", html, StringComparison.Ordinal);
        Assert.Contains("This attendance has already been invalidated.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckInPageRendersHiddenCameraFormAndManualFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = MemberEventTestSupport.NewPassword();
        var officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString,
            $"scanner-{Guid.NewGuid():N}@example.invalid", password, RoleCode.Officer, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var published = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, typeId, venueId,
            EventStatusCode.Published, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(6), 20, null, "Scanner markup event", ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await MemberEventTestSupport.SignInAsync(client, officer.Email, password, ct);

        using var response = await client.GetAsync($"/CheckIn?eventId={published.EventId}", ct);
        var html = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-scanner-mode=\"camera\"", html);
        Assert.Contains("data-scanner-mode=\"manual\"", html);
        Assert.DoesNotContain("data-scanner-mode=\"code\"", html);
        Assert.DoesNotContain("id=\"scan-token\"", html);
        Assert.Matches("(?s)<form\\b(?=[^>]*data-scan-form)(?=[^>]*\\bhidden\\b)(?=[^>]*action=\"/CheckIn/Scan\")(?=[^>]*method=\"post\")[^>]*>.*?name=\"eventId\" value=\"" + published.EventId + "\".*?name=\"__RequestVerificationToken\".*?</form>", html);
        Assert.Contains("id=\"panel-manual\"", html);
        Assert.Contains("name=\"q\"", html);
        Assert.DoesNotContain("id=\"panel-manual\" class=\"scanner-panel\" role=\"tabpanel\" aria-labelledby=\"mode-manual\" hidden", html);
    }

    [Fact]
    public async Task ScannerAndManualLookupReadTheRealCheckInFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var published = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(6), 20, null, "Check-in flag event", ct);

        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var unitOfWork = new UnitOfWork(context);
        var events = new EventRepository(unitOfWork);
        var registrations = new RegistrationRepository(unitOfWork);
        var attendance = new AttendanceRepository(unitOfWork);
        var actor = new Actor(officerId);
        var clock = TimeProvider.System;
        var engine = new AttendanceRecordingService(
            registrations,
            new UserRepository(unitOfWork),
            new MemberRepository(unitOfWork),
            events,
            attendance,
            unitOfWork,
            actor,
            new ActivityLogWriter(context, actor, clock),
            clock);
        var scanner = new CheckInService(events, registrations, attendance, engine);
        var lookup = new CheckInLookupService(events, registrations, engine);

        Assert.False((await scanner.GetScreenAsync(published.EventId, ct)).IsCheckInOpen);
        Assert.Equal(CheckInOutcomeViewModel.Closed, (await scanner.ScanAsync(published.EventId, "unknown", ct)).Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Closed, (await lookup.CheckInAsync(published.EventId, 999, "unknown", ct)).Outcome);

        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Events.SingleAsync(item => item.EventId == published.EventId, ct);
            row.IsCheckInOpen = true;
            await writer.SaveChangesAsync(ct);
        }

        Assert.True((await scanner.GetScreenAsync(published.EventId, ct)).IsCheckInOpen);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, (await scanner.ScanAsync(published.EventId, "unknown", ct)).Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, (await lookup.CheckInAsync(published.EventId, 999, "unknown", ct)).Outcome);
    }

    [Fact]
    public async Task RetiredSeamSwitchKeyIsInertWhenEnabled()
    {
        var ct = TestContext.Current.CancellationToken;

        // The seam is gone, so the old switch is just an unknown configuration key: the host must
        // start and serve normally with it on, no token key and a non-demo database. This test is the
        // one place the retired section name may appear, because it is the check that the seam is gone.
        const string retiredSection = "DemoData";

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(retiredSection + ":Enabled", "true"));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/Account/Login", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void RealCheckInServicesResolveFromTheContainer()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        Assert.IsType<CheckInService>(scope.ServiceProvider.GetRequiredService<ICheckInService>());
        Assert.IsType<CheckInLookupService>(scope.ServiceProvider.GetRequiredService<ICheckInLookupService>());
        Assert.IsType<AttendanceInvalidationService>(scope.ServiceProvider.GetRequiredService<IAttendanceInvalidationService>());
    }

    private sealed class Actor(int id) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => RoleCodes.Officer;
    }
}

public sealed class CheckInEmptyScreenNoticeTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task RemovedLastPublishedEventStillShowsOutcome()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = MemberEventTestSupport.NewPassword();
        var officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString,
            $"empty-{Guid.NewGuid():N}@example.invalid", password, RoleCode.Officer, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var item = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, typeId, venueId,
            EventStatusCode.Published, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(6), 20, null, "Last event", ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await MemberEventTestSupport.SignInAsync(client, officer.Email, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/CheckIn?eventId={item.EventId}", ct);
        using var response = await client.PostAsync("/CheckIn/Scan", MemberEventTestSupport.Form(new()
        {
            ["eventId"] = item.EventId.ToString(), ["token"] = "unknown", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Events.SingleAsync(e => e.EventId == item.EventId, ct);
            row.EventStatusId = EventStatusCode.Completed;
            await writer.SaveChangesAsync(ct);
        }
        var html = await client.GetStringAsync(response.Headers.Location!.ToString(), ct);
        Assert.Contains("No event to check in", html, StringComparison.Ordinal);
        Assert.Contains("notice-error", html, StringComparison.Ordinal);
        Assert.Contains("Check-in is closed for this event", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-redirect-outcome", html, StringComparison.Ordinal);
    }
}
