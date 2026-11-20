using System.Text.RegularExpressions;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// The dossier's fact sheet, roster caption and count are rendered contracts, not only styling.
/// </summary>
public sealed class EventDossierLayoutTests(SqlServerDatabaseFixture fixture)
    : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Password = "DossierPassword#2026";

    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient client = null!;
    private int eventId;
    private int adminId;
    private int memberId;
    private string memberEmail = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var admin = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"dossier-admin-{suffix}@attendta.test", Password, RoleCode.Admin, ct);
        adminId = admin.Id;
        var memberUser = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"dossier-member-{suffix}@attendta.test", Password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(
            fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        memberId = member.MemberId;
        memberEmail = memberUser.Email;
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, admin.Id, ct);
        var starts = new DateTime(2099, 11, 10, 1, 0, 0, DateTimeKind.Utc);
        var item = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, admin.Id, typeId, venueId, EventStatusCode.Published,
            starts, starts.AddDays(-1), 100, null, "Dossier layout assembly", ct);
        await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, item.EventId, member.MemberId, RegistrationStatusCode.Confirmed, admin.Id, ct);
        eventId = item.EventId;

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await MemberEventTestSupport.SignInAsync(client, admin.Email, Password, ct);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task PopulatedRosterShowsCompactCountAndSecondaryCheckIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var html = await client.GetStringAsync($"/Event/Details?id={eventId}&status=confirmed", ct);

        Assert.DoesNotContain("Asia/Manila", html, StringComparison.Ordinal);
        Assert.Contains("Showing 1–1 of 1 matching confirmed registration", html, StringComparison.Ordinal);

        var checkInButton = Regex.Match(html, "<button[^>]*data-dialog-open=\"check-in-[0-9]+\"[^>]*>").Value;
        Assert.Contains("button-secondary", checkInButton, StringComparison.Ordinal);
        Assert.DoesNotContain("button-primary", checkInButton, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyRosterShowsZeroCountWithoutPagination()
    {
        var ct = TestContext.Current.CancellationToken;
        var html = await client.GetStringAsync($"/Event/Details?id={eventId}&status=waitlisted", ct);

        Assert.DoesNotContain("<caption>", html, StringComparison.Ordinal);
        Assert.Contains("0 matching waitlisted registrations", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", html, StringComparison.Ordinal);
        Assert.Matches("Cancelled / declined\\s*<span class=\"filter-tab-count\">0</span>", html);
    }

    [Fact]
    public async Task CancelledCountAndDeclineReasonAppearInBothManagementViews()
    {
        var ct = TestContext.Current.CancellationToken;
        int registrationId;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var registration = await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct);
            registrationId = registration.RegistrationId;
            registration.RegistrationStatusId = RegistrationStatusCode.Pending;
            await context.SaveChangesAsync(ct);
        }

        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/RegistrationReview/Index?eventId={eventId}", ct);
        var response = await client.PostAsync("/RegistrationReview/Decline", MemberEventTestSupport.Form(new()
        {
            ["id"] = registrationId.ToString(), ["status"] = "pending", ["eventId"] = eventId.ToString(),
            ["reason"] = "Decline <reason> & details", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var dossier = await client.GetStringAsync($"/Event/Details?id={eventId}&status=cancelled", ct);
        Assert.Matches("Cancelled / declined\\s*<span class=\"filter-tab-count\">1</span>", dossier);
        Assert.Contains("Decline &lt;reason&gt; &amp; details", dossier, StringComparison.Ordinal);
        var review = await client.GetStringAsync($"/RegistrationReview/Index?status=cancelled&eventId={eventId}", ct);
        Assert.Contains("Decline &lt;reason&gt; &amp; details", review, StringComparison.Ordinal);
        var filtered = await client.GetStringAsync($"/Event/Details?id={eventId}&status=cancelled&search=no-such-person", ct);
        Assert.Matches("Cancelled / declined\\s*<span class=\"filter-tab-count\">0</span>", filtered);
        Assert.DoesNotContain("Decline &lt;reason&gt;", filtered, StringComparison.Ordinal);
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(memberClient, memberEmail, Password, ct);
        var memberPage = await memberClient.GetStringAsync("/MyEvents/Registrations", ct);
        Assert.Contains("Decline &lt;reason&gt; &amp; details", memberPage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledBadgeCountsAllMatchingPagesAndDoesNotInventDeclineReasons()
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 6; i++)
        {
            var email = $"roster-cancelled-{Guid.NewGuid():N}@attendta.test";
            var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, Password, RoleCode.Member, ct);
            var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
            await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventId, member.MemberId, RegistrationStatusCode.Cancelled, user.Id, ct);
        }

        var html = await client.GetStringAsync($"/Event/Details?id={eventId}&status=cancelled&pageSize=5&search=Dossier", ct);
        Assert.Matches("Cancelled / declined\\s*<span class=\"filter-tab-count\">6</span>", html);
        Assert.Contains("Showing 1–5 of 6 matching cancelled registrations", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Decline reason", html, StringComparison.Ordinal);
    }
    [Fact]
    public async Task CancellationAvailabilityTracksAttendanceAndEventOnBothScreens()
    {
        var ct = TestContext.Current.CancellationToken;
        int registrationId;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
            registrationId = (await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct)).RegistrationId;
        var reviewUrl = $"/RegistrationReview/Index?status=confirmed&eventId={eventId}";
        var dossierUrl = $"/Event/Details?id={eventId}&status=confirmed";
        var initial = await client.GetStringAsync(reviewUrl, ct);
        Assert.Contains("data-dialog-open=\"registration-cancel\"", initial, StringComparison.Ordinal);
        Assert.Contains("data-dialog-open=\"registration-cancel\"", await client.GetStringAsync(dossierUrl, ct), StringComparison.Ordinal);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, reviewUrl, ct);
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            context.AttendanceRecords.Add(new Attendance { RegistrationId = registrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = adminId, RecordedAt = DateTime.UtcNow });
            var item = await context.Events.SingleAsync(e => e.EventId == eventId, ct);
            item.EventStatusId = EventStatusCode.Completed;
            await context.SaveChangesAsync(ct);
        }
        foreach (var url in new[] { reviewUrl, dossierUrl })
        {
            var html = await client.GetStringAsync(url, ct);
            var row = Regex.Match(html, $"<tr[^>]*data-registration-id=\"{registrationId}\"[^>]*>.*?</tr>", RegexOptions.Singleline).Value;
            Assert.Contains(">Cancellation unavailable</span>", row, StringComparison.Ordinal);
            var dialog = Regex.Match(row, $"<dialog[^>]*id=\"registration-details-{registrationId}\"[^>]*>.*?</dialog>", RegexOptions.Singleline).Value;
            Assert.NotEmpty(dialog);
            Assert.Contains("Event completed", dialog, StringComparison.Ordinal);
            Assert.Contains("valid attendance recorded", dialog, StringComparison.Ordinal);
            Assert.DoesNotContain("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
            Assert.Contains("registration-details-", row, StringComparison.Ordinal);
        }
        var response = await client.PostAsync("/RegistrationReview/Cancel", MemberEventTestSupport.Form(new()
        {
            ["id"] = registrationId.ToString(), ["status"] = "confirmed", ["eventId"] = eventId.ToString(),
            ["reason"] = "Retain my entered reason", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var after = await client.GetStringAsync(response.Headers.Location!.ToString(), ct);
        Assert.Contains("Retain my entered reason", after, StringComparison.Ordinal);
        Assert.Contains("Event status changed", after, StringComparison.Ordinal);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(RegistrationStatusCode.Confirmed, (await observer.Registrations.SingleAsync(r => r.RegistrationId == registrationId, ct)).RegistrationStatusId);
    }

    [Fact]
    public async Task InvalidatedAttendanceStillAllowsCancellationOnBothScreens()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var registration = await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct);
            context.AttendanceRecords.Add(new Attendance { RegistrationId = registration.RegistrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = adminId, RecordedAt = DateTime.UtcNow, InvalidatedAt = DateTime.UtcNow, InvalidatedBy = adminId, InvalidationReason = "Incorrect scan" });
            await context.SaveChangesAsync(ct);
        }
        foreach (var url in new[] { $"/RegistrationReview/Index?status=confirmed&eventId={eventId}", $"/Event/Details?id={eventId}&status=confirmed" })
        {
            var html = await client.GetStringAsync(url, ct);
            Assert.Contains("data-dialog-open=\"registration-cancel\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("valid attendance recorded", html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Waitlisted, EventStatusCode.Published)]
    [InlineData(RegistrationStatusCode.Pending, EventStatusCode.Published)]
    [InlineData(RegistrationStatusCode.Cancelled, EventStatusCode.Published)]
    [InlineData(RegistrationStatusCode.Waitlisted, EventStatusCode.Draft)]
    [InlineData(RegistrationStatusCode.Confirmed, EventStatusCode.Draft)]
    [InlineData(RegistrationStatusCode.Waitlisted, EventStatusCode.Cancelled)]
    [InlineData(RegistrationStatusCode.Confirmed, EventStatusCode.Cancelled)]
    public async Task BothSurfacesRenderStatusAndEventSpecificActions(RegistrationStatusCode status, EventStatusCode eventStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        int registrationId;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var registration = await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct);
            registrationId = registration.RegistrationId;
            registration.RegistrationStatusId = status;
            (await context.Events.SingleAsync(e => e.EventId == eventId, ct)).EventStatusId = eventStatus;
            await context.SaveChangesAsync(ct);
        }
        var key = status.ToString().ToLowerInvariant();
        foreach (var url in new[] { $"/RegistrationReview/Index?status={key}&eventId={eventId}", $"/Event/Details?id={eventId}&status={key}" })
        {
            var html = await client.GetStringAsync(url, ct);
            var row = Regex.Match(html, $"<tr[^>]*data-registration-id=\"{registrationId}\"[^>]*>.*?</tr>", RegexOptions.Singleline).Value;
            Assert.NotEmpty(row);
            Assert.Contains($"registration-details-{registrationId}", row, StringComparison.Ordinal);
            if (status == RegistrationStatusCode.Pending)
            {
                Assert.Contains("data-dialog-open=\"approve\"", row, StringComparison.Ordinal);
                Assert.Contains("data-dialog-open=\"decline\"", row, StringComparison.Ordinal);
                Assert.DoesNotContain("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
                Assert.DoesNotContain("Cancellation unavailable", row, StringComparison.Ordinal);
            }
            else if (status == RegistrationStatusCode.Cancelled)
            {
                Assert.DoesNotContain("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
                Assert.DoesNotContain("Cancellation unavailable", row, StringComparison.Ordinal);
            }
            else if (eventStatus == EventStatusCode.Published)
                Assert.Contains("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
            else
            {
                Assert.Contains("Cancellation unavailable", row, StringComparison.Ordinal);
                Assert.Contains($"Event {eventStatus.ToString().ToLowerInvariant()}", row, StringComparison.Ordinal);
                Assert.DoesNotContain("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task BothSurfacesRespectExactStartCutoff(long ticks, bool available)
    {
        var ct = TestContext.Current.CancellationToken;
        int registrationId;
        string email;
        DateTime starts;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            email = (await context.Users.SingleAsync(u => u.Id == adminId, ct)).Email;
            registrationId = (await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct)).RegistrationId;
            starts = DateTime.SpecifyKind((await context.Events.SingleAsync(e => e.EventId == eventId, ct)).StartsAt, DateTimeKind.Utc);
        }
        await using var clockFactory = new AttendTaWebApplicationFactory(fixture.ConnectionString)
        {
            ConfigureServicesForTesting = services => services.AddSingleton<TimeProvider>(new FixedClock(starts.AddTicks(ticks)))
        };
        using var clockClient = clockFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(clockClient, email, Password, ct);
        foreach (var url in new[] { $"/RegistrationReview/Index?status=confirmed&eventId={eventId}", $"/Event/Details?id={eventId}&status=confirmed" })
        {
            var html = await clockClient.GetStringAsync(url, ct);
            var row = Regex.Match(html, $"<tr[^>]*data-registration-id=\"{registrationId}\"[^>]*>.*?</tr>", RegexOptions.Singleline).Value;
            Assert.NotEmpty(row);
            Assert.Equal(available, row.Contains("data-dialog-open=\"registration-cancel\"", StringComparison.Ordinal));
            Assert.Equal(!available, row.Contains("event has started", StringComparison.Ordinal));
            Assert.Equal(!available, row.Contains("Cancellation unavailable", StringComparison.Ordinal));
        }
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    [Fact]
    public async Task SearchedPagedQueueUsesEachRowsEventAndAttendance()
    {
        var ct = TestContext.Current.CancellationToken;
        var search = "Matrix" + Guid.NewGuid().ToString("N");
        int typeId, venueId, firstId;
        DateTime starts;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var item = await context.Events.SingleAsync(e => e.EventId == eventId, ct);
            typeId = item.TypeId; venueId = item.VenueId; starts = item.StartsAt;
            var first = await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct);
            firstId = first.RegistrationId;
            var userId = await context.Members.Where(m => m.MemberId == memberId).Select(m => m.UserId).SingleAsync(ct);
            (await context.Users.SingleAsync(u => u.Id == userId, ct)).Name = search;
            first.SubmittedAt = new DateTime(2020, 1, 1);
            await context.SaveChangesAsync(ct);
        }
        var other = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, adminId, typeId, venueId,
            EventStatusCode.Completed, starts, starts.AddDays(-1), 100, null, "Other matrix event", ct);
        var ids = new List<int> { firstId };
        for (var i = 1; i < 6; i++)
        {
            var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"matrix-{Guid.NewGuid():N}@attendta.test", Password, RoleCode.Member, ct);
            var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
            var registration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, i % 2 == 0 ? eventId : other.EventId, member.MemberId, RegistrationStatusCode.Confirmed, adminId, ct);
            ids.Add(registration.RegistrationId);
            await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
            (await context.Users.SingleAsync(u => u.Id == user.Id, ct)).Name = search;
            (await context.Registrations.SingleAsync(r => r.RegistrationId == registration.RegistrationId, ct)).SubmittedAt = new DateTime(2020, 1, 1).AddDays(i);
            if (i == 2) context.AttendanceRecords.Add(new Attendance { RegistrationId = registration.RegistrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = adminId, RecordedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(ct);
        }
        var firstPage = await client.GetStringAsync($"/RegistrationReview/Index?status=confirmed&search={search}&pageSize=5&page=1", ct);
        var secondPage = await client.GetStringAsync($"/RegistrationReview/Index?status=confirmed&search={search}&pageSize=5&page=2", ct);
        Assert.Contains("Showing 1–5 of 6", firstPage, StringComparison.Ordinal);
        Assert.Contains("Showing 6–6 of 6", secondPage, StringComparison.Ordinal);
        Assert.Equal(ids.Take(5), Regex.Matches(firstPage, "data-registration-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)));
        Assert.Equal(ids.Skip(5), Regex.Matches(secondPage, "data-registration-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)));
        for (var i = 0; i < ids.Count; i++)
        {
            var html = i < 5 ? firstPage : secondPage;
            var row = Regex.Match(html, $"<tr[^>]*data-registration-id=\"{ids[i]}\"[^>]*>.*?</tr>", RegexOptions.Singleline).Value;
            Assert.NotEmpty(row);
            if (i % 2 != 0) Assert.Contains("Event completed", row, StringComparison.Ordinal);
            else if (i == 2) Assert.Contains("valid attendance recorded", row, StringComparison.Ordinal);
            else Assert.Contains("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
            Assert.Equal(i % 2 != 0 || i == 2, row.Contains("Cancellation unavailable", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task DossierCancellationPostSucceedsAndWritesBothAudits()
    {
        var ct = TestContext.Current.CancellationToken;
        int registrationId;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
            registrationId = (await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct)).RegistrationId;
        var url = $"/Event/Details?id={eventId}&status=confirmed&search=Member&pageSize=5&page=1";
        var html = await client.GetStringAsync(url, ct);
        var row = Regex.Match(html, $"<tr[^>]*data-registration-id=\"{registrationId}\"[^>]*>.*?</tr>", RegexOptions.Singleline).Value;
        Assert.Contains("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
        var dialog = Regex.Match(html, "<dialog id=\"registration-cancel\".*?</dialog>", RegexOptions.Singleline).Value;
        var action = System.Net.WebUtility.HtmlDecode(Regex.Match(dialog, "<form[^>]*action=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(action);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, url, ct);
        using var response = await client.PostAsync(action, MemberEventTestSupport.Form(new()
        {
            ["id"] = registrationId.ToString(), ["eventId"] = eventId.ToString(), ["status"] = "confirmed", ["page"] = "1",
            ["dossier"] = "true", ["reason"] = "  Dossier cancellation  ", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/Event/Details", location, StringComparison.Ordinal);
        Assert.Contains("search=Member", location, StringComparison.Ordinal);
        Assert.Contains("pageSize=5", location, StringComparison.Ordinal);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var saved = await observer.Registrations.SingleAsync(r => r.RegistrationId == registrationId, ct);
        Assert.Equal(RegistrationStatusCode.Cancelled, saved.RegistrationStatusId);
        Assert.Equal("Dossier cancellation", saved.CancellationReason);
        Assert.Equal(adminId, saved.CancelledBy);
        var actions = await observer.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == registrationId).Select(l => l.Action).ToListAsync(ct);
        Assert.Equal(2, actions.Count);
        Assert.Contains("registration.cancelled", actions);
        Assert.Contains("registration.admin_cancelled", actions);
    }

    [Theory]
    [InlineData(EventStatusCode.Published, EventStatusCode.Completed)]
    [InlineData(EventStatusCode.Completed, EventStatusCode.Published)]
    public async Task DossierUsesLaterEventSnapshotWhenStateChangesAfterRosterRead(EventStatusCode before, EventStatusCode after)
    {
        var ct = TestContext.Current.CancellationToken;
        string email;
        int registrationId;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            email = (await context.Users.SingleAsync(u => u.Id == adminId, ct)).Email;
            registrationId = (await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct)).RegistrationId;
            (await context.Events.SingleAsync(e => e.EventId == eventId, ct)).EventStatusId = before;
            await context.SaveChangesAsync(ct);
        }

        var gate = new RosterReadGate();
        await using var gatedFactory = new AttendTaWebApplicationFactory(fixture.ConnectionString)
        {
            ConfigureServicesForTesting = services =>
            {
                services.AddScoped<RegistrationReviewService>();
                services.AddScoped<IRegistrationReviewService>(sp => new GatedReviewService(sp.GetRequiredService<RegistrationReviewService>(), gate));
            }
        };
        using var gatedClient = gatedFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(gatedClient, email, Password, ct);
        var request = gatedClient.GetStringAsync($"/Event/Details?id={eventId}&status=confirmed", ct);
        try
        {
            var roster = await gate.Read.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            var snapshot = Assert.Single(roster.Items);
            Assert.Equal(registrationId, snapshot.RegistrationId);
            Assert.Equal(before, snapshot.EventStatus);
            Assert.Equal(before == EventStatusCode.Published, snapshot.CancellationUnavailableReason == null);

            // The real roster query has completed, while the controller cannot yet read the event.
            await using var writer = MemberEventTestSupport.Context(fixture.ConnectionString);
            (await writer.Events.SingleAsync(e => e.EventId == eventId, ct)).EventStatusId = after;
            await writer.SaveChangesAsync(ct);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        var html = await request.WaitAsync(TimeSpan.FromSeconds(15), ct);
        var header = Regex.Match(html, "<header class=\"page-heading\">.*?</header>", RegexOptions.Singleline).Value;
        Assert.Contains($">{after}</span>", header, StringComparison.Ordinal);
        var row = Regex.Match(html, $"<tr[^>]*data-registration-id=\"{registrationId}\"[^>]*>.*?</tr>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(row);
        if (after == EventStatusCode.Completed)
        {
            Assert.Contains("Cancellation unavailable", row, StringComparison.Ordinal);
            Assert.Contains("Event completed", row, StringComparison.Ordinal);
            Assert.DoesNotContain("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
            Assert.Contains("This event is completed. Its roster is read-only.", html, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
            Assert.DoesNotContain("Cancellation unavailable", row, StringComparison.Ordinal);
            Assert.DoesNotContain("Its roster is read-only.", html, StringComparison.Ordinal);
        }
    }

    private sealed class RosterReadGate
    {
        public TaskCompletionSource<PagedResult<RegistrationReviewRow>> Read { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedReviewService(IRegistrationReviewService inner, RosterReadGate gate) : IRegistrationReviewService
    {
        public async Task<PagedResult<RegistrationReviewRow>> GetPageAsync(string status, int? eventId, int? page, CancellationToken ct = default)
        {
            var result = await inner.GetPageAsync(status, eventId, page, ct);
            gate.Read.TrySetResult(result);
            await gate.Release.Task.WaitAsync(ct);
            return result;
        }

        public Task<IReadOnlyList<EventSelectorOption>> GetEventOptionsAsync(CancellationToken ct = default) => inner.GetEventOptionsAsync(ct);
        public Task<int> GetCountAsync(string status, int? eventId, CancellationToken ct = default) => inner.GetCountAsync(status, eventId, ct);
        public Task ApproveAsync(int id, CancellationToken ct = default) => inner.ApproveAsync(id, ct);
        public Task<IReadOnlyList<RegistrationApprovalOutcome>> ApproveManyAsync(IReadOnlyCollection<int> ids, int? eventId, CancellationToken ct = default) => inner.ApproveManyAsync(ids, eventId, ct);
        public Task DeclineAsync(int id, string reason, CancellationToken ct = default) => inner.DeclineAsync(id, reason, ct);
        public Task CancelAsync(int id, string reason, CancellationToken ct = default) => inner.CancelAsync(id, reason, ct);
        public Task<RegistrationSummary> GetPromotionCandidateAsync(int eventId, CancellationToken ct = default) => inner.GetPromotionCandidateAsync(eventId, ct);
        public Task PromoteNextAsync(int eventId, int expectedRegistrationId, CancellationToken ct = default) => inner.PromoteNextAsync(eventId, expectedRegistrationId, ct);
    }

    [Fact]
    public async Task OffPageFailedCancellationRetainsRegistrationAndReason()
    {
        var ct = TestContext.Current.CancellationToken;
        int registrationId;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var registration = await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct);
            registrationId = registration.RegistrationId;
            registration.RegistrationStatusId = RegistrationStatusCode.Cancelled;
            await context.SaveChangesAsync(ct);
        }
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/RegistrationReview/Index?status=confirmed&eventId={eventId}", ct);
        var response = await client.PostAsync("/RegistrationReview/Cancel?search=missing&pageSize=5", MemberEventTestSupport.Form(new()
        {
            ["id"] = registrationId.ToString(), ["status"] = "confirmed", ["eventId"] = eventId.ToString(),
            ["reason"] = "Keep this reason", ["__RequestVerificationToken"] = token
        }), ct);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("search=missing", location, StringComparison.Ordinal);
        Assert.Contains("pageSize=5", location, StringComparison.Ordinal);
        var html = await client.GetStringAsync(location, ct);
        Assert.Contains($"Registration #{registrationId} is no longer on this page", html, StringComparison.Ordinal);
        Assert.Contains("Keep this reason", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueuePromotionFormPreservesSearchAndPageSize()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var registration = await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct);
            registration.RegistrationStatusId = RegistrationStatusCode.Waitlisted;
            await context.SaveChangesAsync(ct);
        }
        var html = await client.GetStringAsync($"/RegistrationReview/Index?status=waitlisted&eventId={eventId}&search=Dossier&pageSize=5", ct);
        var dialog = Regex.Match(html, "<dialog id=\"promote\".*?</dialog>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(dialog);
        Assert.Contains("search=Dossier", dialog, StringComparison.Ordinal);
        Assert.Contains("pageSize=5", dialog, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PromotionPostPreservesListContextAndWritesOneAudit(bool dossier)
    {
        var ct = TestContext.Current.CancellationToken;
        int registrationId;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var registration = await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct);
            registrationId = registration.RegistrationId;
            registration.RegistrationStatusId = RegistrationStatusCode.Waitlisted;
            registration.ReviewedAt = new DateTime(2020, 1, 1);
            await context.SaveChangesAsync(ct);
        }
        for (var i = 0; i < 5; i++)
        {
            var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"promotion-page-{Guid.NewGuid():N}@attendta.test", Password, RoleCode.Member, ct);
            var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
            await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventId, member.MemberId, RegistrationStatusCode.Waitlisted, adminId, ct);
        }
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            foreach (var row in await context.Registrations.Where(r => r.EventId == eventId && r.RegistrationId != registrationId).ToListAsync(ct))
                row.ReviewedAt = new DateTime(2021, 1, 1);
            await context.SaveChangesAsync(ct);
        }
        var url = dossier ? $"/Event/Details?id={eventId}&status=waitlisted&search=Member&pageSize=5&page=2"
            : $"/RegistrationReview/Index?eventId={eventId}&status=waitlisted&search=Member&pageSize=5&page=2";
        var html = await client.GetStringAsync(url, ct);
        Assert.Contains("Page 2 of 2", html, StringComparison.Ordinal);
        var dialog = Regex.Match(html, "<dialog id=\"promote\".*?</dialog>", RegexOptions.Singleline).Value;
        Assert.Contains($"name=\"expectedRegistrationId\" value=\"{registrationId}\"", dialog, StringComparison.Ordinal);
        var action = System.Net.WebUtility.HtmlDecode(Regex.Match(dialog, "<form[^>]*action=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(action);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, url, ct);
        using var response = await client.PostAsync(action, MemberEventTestSupport.Form(new()
        {
            ["eventId"] = eventId.ToString(), ["expectedRegistrationId"] = registrationId.ToString(),
            ["dossier"] = dossier ? "true" : "false", ["page"] = "2", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("search=Member", location, StringComparison.Ordinal);
        Assert.Contains("pageSize=5", location, StringComparison.Ordinal);
        Assert.Contains("page=2", location, StringComparison.Ordinal);
        Assert.Contains("status=waitlisted", location, StringComparison.Ordinal);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(RegistrationStatusCode.Confirmed, (await observer.Registrations.SingleAsync(r => r.RegistrationId == registrationId, ct)).RegistrationStatusId);
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(l => l.EntityType == "registrations" && l.EntityId == registrationId && l.Action == "registration.promoted", ct));
    }

    [Theory]
    [InlineData(false, "event")]
    [InlineData(true, "event")]
    [InlineData(false, "attendance")]
    [InlineData(true, "attendance")]
    [InlineData(false, "registration")]
    [InlineData(true, "registration")]
    public async Task StaleCancellationPostRetainsReasonAndDoesNotAudit(bool dossier, string change)
    {
        var ct = TestContext.Current.CancellationToken;
        int registrationId;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
            registrationId = (await context.Registrations.SingleAsync(r => r.EventId == eventId && r.MemberId == memberId, ct)).RegistrationId;
        var url = dossier ? $"/Event/Details?id={eventId}&status=confirmed&search=Dossier&pageSize=5&page=1"
            : $"/RegistrationReview/Index?eventId={eventId}&status=confirmed&search=Dossier&pageSize=5&page=1";
        Assert.Contains("data-dialog-open=\"registration-cancel\"", await client.GetStringAsync(url, ct), StringComparison.Ordinal);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, url, ct);
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            if (change == "event") (await context.Events.SingleAsync(e => e.EventId == eventId, ct)).EventStatusId = EventStatusCode.Completed;
            if (change == "registration") (await context.Registrations.SingleAsync(r => r.RegistrationId == registrationId, ct)).RegistrationStatusId = RegistrationStatusCode.Cancelled;
            if (change == "attendance") context.AttendanceRecords.Add(new Attendance { RegistrationId = registrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = adminId, RecordedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(ct);
        }
        using var response = await client.PostAsync("/RegistrationReview/Cancel?search=Dossier&pageSize=5", MemberEventTestSupport.Form(new()
        {
            ["id"] = registrationId.ToString(), ["eventId"] = eventId.ToString(), ["status"] = "confirmed", ["page"] = "1",
            ["dossier"] = dossier ? "true" : "false", ["reason"] = "Keep <this> reason", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("search=Dossier", location, StringComparison.Ordinal);
        Assert.Contains("pageSize=5", location, StringComparison.Ordinal);
        Assert.Contains("page=1", location, StringComparison.Ordinal);
        var html = await client.GetStringAsync(location, ct);
        Assert.Contains("Keep &lt;this&gt; reason", html, StringComparison.Ordinal);
        Assert.Contains(change == "event" ? "Event status changed" : change == "registration" ? "Registration status changed" : "attendance", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{registrationId}", html, StringComparison.Ordinal);
        Assert.Contains("data-list-preserve", html, StringComparison.Ordinal);
        if (change != "registration")
        {
            var row = Regex.Match(html, $"<tr[^>]*data-registration-id=\"{registrationId}\"[^>]*>.*?</tr>", RegexOptions.Singleline).Value;
            Assert.DoesNotContain("data-dialog-open=\"registration-cancel\"", row, StringComparison.Ordinal);
            var dialog = Regex.Match(html, "<dialog id=\"registration-cancel\".*?</dialog>", RegexOptions.Singleline).Value;
            Assert.Contains("data-reopen=\"true\"", dialog, StringComparison.Ordinal);
            Assert.Contains("data-list-preserve=\"true\"", dialog, StringComparison.Ordinal);
            Assert.Contains("Keep &lt;this&gt; reason", dialog, StringComparison.Ordinal);
        }
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var actual = await observer.Registrations.SingleAsync(r => r.RegistrationId == registrationId, ct);
        Assert.Equal(change == "registration" ? RegistrationStatusCode.Cancelled : RegistrationStatusCode.Confirmed, actual.RegistrationStatusId);
        Assert.Null(actual.CancelledAt);
        Assert.Empty(await observer.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == registrationId).ToListAsync(ct));
    }
}
