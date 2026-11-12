using System.Net;
using System.Security.Cryptography;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DeactivationPreviewTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture fixture;
    private AttendTaWebApplicationFactory factory;
    private HttpClient client;
    private string password;
    private User admin;
    private User officer;
    private User memberUser;
    private User emptyUser;
    private Member member;
    private Member emptyMember;
    private Event firstEvent;
    private Event secondEvent;
    private Event attendedEvent;
    private Registration firstRegistration;
    private Registration secondRegistration;
    private Registration attendedRegistration;

    public DeactivationPreviewTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        password = MemberEventTestSupport.NewPassword();

        admin = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"preview-admin-{Guid.NewGuid():N}@attendta.test", password, RoleCode.Admin, ct);
        officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"preview-officer-{Guid.NewGuid():N}@attendta.test", password, RoleCode.Officer, ct);
        memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"preview-member-{Guid.NewGuid():N}@attendta.test", password, RoleCode.Member, ct);
        emptyUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"preview-empty-{Guid.NewGuid():N}@attendta.test", password, RoleCode.Member, ct);

        member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        emptyMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, emptyUser.Id, MembershipStatusCode.Active, ct);

        // The preview must render the server-read identity as text, so this account carries markup
        // characters in its display name.
        emptyUser.Name = "Preview <Empty> & Co";
        await using (var identityContext = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var stored = await identityContext.Users.SingleAsync(u => u.Id == emptyUser.Id, ct);
            stored.Name = emptyUser.Name;
            await identityContext.SaveChangesAsync(ct);
        }

        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, admin.Id, ct);

        var firstStart = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        firstEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, admin.Id, typeId, venueId,
            EventStatusCode.Published, firstStart, firstStart.AddDays(-1), 20, null, "Preview First Event", ct);
        secondEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, admin.Id, typeId, venueId,
            EventStatusCode.Published, firstStart.AddDays(1), firstStart.AddDays(-1), 20, null, "Preview Second Event", ct);
        attendedEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, admin.Id, typeId, venueId,
            EventStatusCode.Published, firstStart.AddDays(2), firstStart.AddDays(-1), 20, null, "Preview Attended Event", ct);

        firstRegistration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, firstEvent.EventId, member.MemberId, RegistrationStatusCode.Confirmed, admin.Id, ct);
        secondRegistration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, secondEvent.EventId, member.MemberId, RegistrationStatusCode.Pending, admin.Id, ct);
        attendedRegistration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, attendedEvent.EventId, member.MemberId, RegistrationStatusCode.Confirmed, admin.Id, ct);

        // A past event and a cancelled registration must never appear in the preview.
        var pastStart = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var pastEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, admin.Id, typeId, venueId,
            EventStatusCode.Published, pastStart, pastStart.AddDays(-1), 20, null, "Preview Past Event", ct);
        var cancelledEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, admin.Id, typeId, venueId,
            EventStatusCode.Published, firstStart.AddDays(3), firstStart.AddDays(-1), 20, null, "Preview Cancelled Event", ct);
        await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, pastEvent.EventId, emptyMember.MemberId, RegistrationStatusCode.Confirmed, admin.Id, ct);
        await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, cancelledEvent.EventId, emptyMember.MemberId, RegistrationStatusCode.Cancelled, admin.Id, ct);

        await SeedQrTokenAndAttendanceAsync(ct);

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
    }

    [Fact]
    public async Task AdministratorPreviewListsOnlyUnattendedUpcomingRegistrationsAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var before = await SnapshotAsync(ct);

        await MemberEventTestSupport.SignInAsync(client, admin.UserId, password, ct);
        var response = await client.GetAsync($"/DeactivationPreview/{memberUser.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("<dialog class=\"app-dialog\" aria-labelledby=\"deactivation-preview-title\">", html, StringComparison.Ordinal);
        Assert.Contains("data-registration-id=\"" + firstRegistration.RegistrationId + "\"", html, StringComparison.Ordinal);
        Assert.Contains("data-registration-id=\"" + secondRegistration.RegistrationId + "\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-registration-id=\"" + attendedRegistration.RegistrationId + "\"", html, StringComparison.Ordinal);
        Assert.Contains("Preview First Event", html, StringComparison.Ordinal);
        Assert.Contains("Preview Second Event", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview Attended Event", html, StringComparison.Ordinal);
        Assert.Contains(memberUser.Name, html, StringComparison.Ordinal);
        Assert.Contains(memberUser.UserId, html, StringComparison.Ordinal);
        Assert.Contains("Asia/Manila", html, StringComparison.Ordinal);
        Assert.Contains("method=\"dialog\"", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("Preview First Event", StringComparison.Ordinal) < html.IndexOf("Preview Second Event", StringComparison.Ordinal),
            "The preview must be ordered by start time.");
        Assert.DoesNotContain("No upcoming registrations will be cancelled.", html, StringComparison.Ordinal);

        var after = await SnapshotAsync(ct);
        Assert.Equal(before.Registrations, after.Registrations);
        Assert.Equal(before.AttendanceCount, after.AttendanceCount);
        Assert.Equal(before.ActivityLogCount, after.ActivityLogCount);
    }

    [Fact]
    public async Task EmptyPreviewShowsTheEmptyStateAndNoRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await MemberEventTestSupport.SignInAsync(client, admin.UserId, password, ct);

        var response = await client.GetAsync($"/DeactivationPreview/{emptyUser.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("No upcoming registrations will be cancelled.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-registration-id=", html, StringComparison.Ordinal);
        // Identity is shown even with no impact, and the server-read name is encoded as text.
        Assert.Contains("Preview &lt;Empty&gt; &amp; Co", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview <Empty>", html, StringComparison.Ordinal);
        Assert.Contains(emptyUser.UserId, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownAccountReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await MemberEventTestSupport.SignInAsync(client, admin.UserId, password, ct);

        var response = await client.GetAsync($"/DeactivationPreview/{int.MaxValue}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InactiveAccountPreviewKeepsIdentityAndUpcomingRegistrations()
    {
        var ct = TestContext.Current.CancellationToken;
        var inactiveUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString,
            $"preview-inactive-{Guid.NewGuid():N}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var inactiveMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, inactiveUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, admin.Id, ct);
        var start = new DateTime(2099, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var upcoming = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, admin.Id, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Preview Inactive Upcoming", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString,
            upcoming.EventId, inactiveMember.MemberId, RegistrationStatusCode.Confirmed, admin.Id, ct);

        // The account is disabled directly, leaving its upcoming registration in place.
        await using (var db = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var stored = await db.Users.SingleAsync(row => row.Id == inactiveUser.Id, ct);
            stored.Name = "Inactive Preview Target";
            stored.IsActive = false;
            await db.SaveChangesAsync(ct);
        }

        await MemberEventTestSupport.SignInAsync(client, admin.UserId, password, ct);
        var response = await client.GetAsync($"/DeactivationPreview/{inactiveUser.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("Inactive Preview Target", html, StringComparison.Ordinal);
        Assert.Contains(inactiveUser.UserId, html, StringComparison.Ordinal);
        Assert.Contains("data-registration-id=\"" + registration.RegistrationId + "\"", html, StringComparison.Ordinal);
        Assert.Contains("Preview Inactive Upcoming", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No upcoming registrations will be cancelled.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewRequiresManagementAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var url = $"/DeactivationPreview/{memberUser.Id}";

        var anonymous = await client.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", LocationPath(anonymous), StringComparison.Ordinal);

        using var memberClient = CreateClient();
        await MemberEventTestSupport.SignInAsync(memberClient, memberUser.UserId, password, ct);
        var denied = await memberClient.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", LocationPath(denied), StringComparison.Ordinal);

        using var officerClient = CreateClient();
        await MemberEventTestSupport.SignInAsync(officerClient, officer.UserId, password, ct);
        var officerPreview = await officerClient.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.OK, officerPreview.StatusCode);
        Assert.Contains("data-registration-id=", await officerPreview.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        await MemberEventTestSupport.SignInAsync(client, admin.UserId, password, ct);
        var adminPreview = await client.GetAsync(url, ct);
        Assert.Equal(HttpStatusCode.OK, adminPreview.StatusCode);
        Assert.Contains("data-registration-id=", await adminPreview.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountsListExposesThePreviewTrigger()
    {
        var ct = TestContext.Current.CancellationToken;
        await MemberEventTestSupport.SignInAsync(client, admin.UserId, password, ct);

        var html = await client.GetStringAsync($"/admin/accounts?search={Uri.EscapeDataString(memberUser.UserId)}", ct);

        Assert.Contains("Preview deactivation", html, StringComparison.Ordinal);
        Assert.Contains($"data-deactivation-preview=\"/DeactivationPreview/{memberUser.Id}\"", html, StringComparison.Ordinal);
        Assert.Contains($"/admin/accounts/{memberUser.Id}/role", html, StringComparison.Ordinal);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private async Task SeedQrTokenAndAttendanceAsync(CancellationToken ct)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var first = await context.Registrations.SingleAsync(r => r.RegistrationId == firstRegistration.RegistrationId, ct);
        first.QrToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        context.Set<Attendance>().Add(new Attendance
        {
            RegistrationId = attendedRegistration.RegistrationId,
            CheckInMethodId = CheckInMethodCode.QrCode,
            RecordedBy = admin.Id,
            RecordedAt = attendedEvent.StartsAt
        });
        await context.SaveChangesAsync(ct);
    }

    private async Task<PreviewSnapshot> SnapshotAsync(CancellationToken ct)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var registrations = await context.Registrations.AsNoTracking()
            .OrderBy(r => r.RegistrationId)
            .Select(r => new RegistrationSnapshot(r.RegistrationId, r.RegistrationStatusId, r.QrToken,
                r.CancelledBy, r.CancelledAt, r.CancellationReason, r.UpdatedAt))
            .ToArrayAsync(ct);
        return new PreviewSnapshot(
            registrations,
            await context.Set<Attendance>().AsNoTracking().CountAsync(ct),
            await context.ActivityLogs.AsNoTracking().CountAsync(ct));
    }

    private static string LocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }

    private sealed record PreviewSnapshot(RegistrationSnapshot[] Registrations, int AttendanceCount, int ActivityLogCount);

    private sealed record RegistrationSnapshot(int RegistrationId, RegistrationStatusCode Status, string QrToken,
        int? CancelledBy, DateTime? CancelledAt, string CancellationReason, DateTime? UpdatedAt);
}
