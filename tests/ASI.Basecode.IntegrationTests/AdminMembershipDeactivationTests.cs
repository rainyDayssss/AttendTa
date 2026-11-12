using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Membership deactivation against SQL Server: the real 5.3 preview behind the dialog, the
/// endpoint cascade that cancels upcoming registrations and audits once, the refusals, and the
/// demo switch that must not mask the real row. Membership status never changes login access.
/// </summary>
public sealed class AdminMembershipDeactivationTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture fixture;
    private string password = null!;
    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public AdminMembershipDeactivationTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public ValueTask InitializeAsync()
    {
        password = MemberEventTestSupport.NewPassword();
        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = CreateClient(factory);
        return ValueTask.CompletedTask;
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
    public async Task DialogPreviewsExactlyTheMembersUpcomingRegistrations()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "preview-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "preview-target", ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, target.Id, MembershipStatusCode.Active, ct);
        var peerUser = await SeedUserAsync(RoleCode.Member, "preview-peer", ct);
        var peerMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, peerUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actor.Id, ct);

        var start = new DateTime(2099, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Preview Confirmed Event", ct);
        var secondEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start.AddDays(1), start.AddDays(-1), 20, null, "Preview Pending Event", ct);
        var pastEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2019, 12, 1, 0, 0, 0, DateTimeKind.Utc), 20, null, "Preview Past Event", ct);

        var confirmed = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, firstEvent.EventId, member.MemberId, RegistrationStatusCode.Confirmed, actor.Id, ct);
        var pending = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, secondEvent.EventId, member.MemberId, RegistrationStatusCode.Pending, actor.Id, ct);
        var past = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, pastEvent.EventId, member.MemberId, RegistrationStatusCode.Confirmed, actor.Id, ct);
        var peer = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, firstEvent.EventId, peerMember.MemberId, RegistrationStatusCode.Waitlisted, actor.Id, ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        var dialog = await client.GetStringAsync($"/Member/{member.MemberId}/access", ct);

        Assert.Contains($"data-registration-id=\"{confirmed.RegistrationId}\"", dialog, StringComparison.Ordinal);
        Assert.Contains($"data-registration-id=\"{pending.RegistrationId}\"", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-registration-id=\"{past.RegistrationId}\"", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-registration-id=\"{peer.RegistrationId}\"", dialog, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(dialog, "data-registration-id=").Count);
        Assert.Contains("badge-active", dialog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeactivationCascadesCancellationAuditsOnceAndLeavesLoginAccessAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "deactivate-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "deactivate-target", ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, target.Id, MembershipStatusCode.Active, ct);
        var peerUser = await SeedUserAsync(RoleCode.Member, "deactivate-peer", ct);
        var peerMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, peerUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actor.Id, ct);

        var start = new DateTime(2099, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var confirmedEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Deactivation Confirmed Event", ct);
        var pendingEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start.AddDays(1), start.AddDays(-1), 20, null, "Deactivation Pending Event", ct);

        var confirmed = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, confirmedEvent.EventId, member.MemberId, RegistrationStatusCode.Confirmed, actor.Id, ct);
        var pending = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, pendingEvent.EventId, member.MemberId, RegistrationStatusCode.Pending, actor.Id, ct);
        var waitlisted = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, confirmedEvent.EventId, peerMember.MemberId, RegistrationStatusCode.Waitlisted, actor.Id, ct);
        await SetQrTokenAsync(confirmed.RegistrationId, new string('a', 64), ct);
        await SetQrTokenAsync(pending.RegistrationId, new string('b', 64), ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        const string reason = "  Membership deactivated for cascade test.  ";
        using var response = await DeactivateThroughEndpointAsync(client, member.MemberId, reason, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Member", MemberEventTestSupport.LocationPath(response), StringComparison.Ordinal);

        await using var observer = Context();
        var memberAfter = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == member.MemberId, ct);
        Assert.Equal(MembershipStatusCode.Inactive, memberAfter.MembershipStatusId);
        Assert.Equal(actor.Id, memberAfter.UpdatedBy);
        Assert.NotNull(memberAfter.UpdatedAt);

        var cancelled = await observer.Registrations.AsNoTracking()
            .Where(item => item.RegistrationId == confirmed.RegistrationId || item.RegistrationId == pending.RegistrationId)
            .ToListAsync(ct);
        Assert.Equal(2, cancelled.Count);
        Assert.All(cancelled, row =>
        {
            Assert.Equal(RegistrationStatusCode.Cancelled, row.RegistrationStatusId);
            Assert.Null(row.QrToken);
            Assert.Equal(actor.Id, row.CancelledBy);
            Assert.Equal("Membership deactivated for cascade test.", row.CancellationReason);
            Assert.NotNull(row.CancelledAt);
        });

        var stillWaitlisted = await observer.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == waitlisted.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Waitlisted, stillWaitlisted.RegistrationStatusId);

        // Membership eligibility is separate from login access.
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
        Assert.True(user.IsActive);

        var audit = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(item => item.Action == AuditActions.MemberMembershipDeactivated && item.EntityId == member.MemberId, ct);
        Assert.Equal("members", audit.EntityType);
        Assert.Equal(actor.Id, audit.ActorUserId);
        Assert.Equal(ActorTypeCode.User, audit.ActorTypeId);
        Assert.Equal("Membership deactivated for cascade test.", audit.Reason);
        Assert.Contains("\"Status\":\"Active\"", audit.OldValues ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"Status\":\"Inactive\"", audit.NewValues ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(
            item => item.Action == AuditActions.MemberMembershipDeactivated && item.EntityId == member.MemberId, ct));

        var cancellationLogs = await observer.ActivityLogs.AsNoTracking()
            .Where(item => item.Action == AuditActions.RegistrationCancelled
                && (item.EntityId == confirmed.RegistrationId || item.EntityId == pending.RegistrationId))
            .ToListAsync(ct);
        Assert.Equal(2, cancellationLogs.Count);
        Assert.All(cancellationLogs, log => Assert.Contains(
            "\"Cause\":\"MembershipDeactivated\"", log.NewValues ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeactivatedMemberStillSignsInAndReachesTheMemberSurface()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "signin-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "signin-target", ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, target.Id, MembershipStatusCode.Active, ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        using (var deactivated = await DeactivateThroughEndpointAsync(client, member.MemberId, "Membership suspended.", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);
        }

        using var targetClient = CreateClient(factory);
        await MemberEventTestSupport.SignInAsync(targetClient, target.UserId, password, ct);
        using var feed = await targetClient.GetAsync("/MemberEvents", ct);
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
    }

    [Fact]
    public async Task BlankOverlongAlreadyInactiveUnknownAndNonManagementAreRefusedWithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "refusal-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "refusal-target", ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, target.Id, MembershipStatusCode.Active, ct);
        var inactiveUser = await SeedUserAsync(RoleCode.Member, "refusal-inactive", ct);
        var inactiveMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, inactiveUser.Id, MembershipStatusCode.Inactive, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actor.Id, ct);

        var start = new DateTime(2099, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Refusal Event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, actor.Id, ct);
        await SetQrTokenAsync(registration.RegistrationId, new string('c', 64), ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);

        using (var blank = await DeactivateThroughEndpointAsync(client, member.MemberId, "   ", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, blank.StatusCode);
        }

        var blankList = await client.GetStringAsync("/Member", ct);
        Assert.Contains("A reason is required.", blankList, StringComparison.Ordinal);

        using (var overlong = await DeactivateThroughEndpointAsync(client, member.MemberId, new string('x', 1001), ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, overlong.StatusCode);
        }

        var overlongList = await client.GetStringAsync("/Member", ct);
        Assert.Contains("The reason must be 1000 characters or fewer.", overlongList, StringComparison.Ordinal);

        await using (var context = Context())
        {
            var service = CreateService(context, new TestActor(actor.Id, RoleCodes.Admin));

            var alreadyInactive = await Assert.ThrowsAsync<DomainException>(
                () => service.DeactivateAsync(inactiveMember.MemberId, "Already inactive.", ct));
            Assert.Equal(OutcomeCode.Invalid, alreadyInactive.Outcome);
            Assert.Equal("This membership is already inactive.", alreadyInactive.Message);

            var unknown = await Assert.ThrowsAsync<DomainException>(
                () => service.DeactivateAsync(int.MaxValue, "Unknown member.", ct));
            Assert.Equal(OutcomeCode.NotFound, unknown.Outcome);
            Assert.Equal("Member not found.", unknown.Message);
        }

        await using (var context = Context())
        {
            var memberService = CreateService(context, new TestActor(target.Id, RoleCodes.Member));
            var forbidden = await Assert.ThrowsAsync<DomainException>(
                () => memberService.DeactivateAsync(member.MemberId, "Member attempt.", ct));
            Assert.Equal(OutcomeCode.Forbidden, forbidden.Outcome);
            Assert.Equal("Access denied.", forbidden.Message);
        }

        await using (var context = Context())
        {
            var anonymousService = CreateService(context, new AnonymousActor());
            var forbidden = await Assert.ThrowsAsync<DomainException>(
                () => anonymousService.DeactivateAsync(member.MemberId, "Anonymous attempt.", ct));
            Assert.Equal(OutcomeCode.Forbidden, forbidden.Outcome);
            Assert.Equal("Access denied.", forbidden.Message);
        }

        await using var observer = Context();
        var memberAfter = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == member.MemberId, ct);
        Assert.Equal(MembershipStatusCode.Active, memberAfter.MembershipStatusId);
        Assert.Null(memberAfter.UpdatedBy);
        Assert.Null(memberAfter.UpdatedAt);

        var registrationAfter = await observer.Registrations.AsNoTracking()
            .SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, registrationAfter.RegistrationStatusId);
        Assert.Equal(new string('c', 64), registrationAfter.QrToken);

        Assert.False(await observer.ActivityLogs.AnyAsync(
            item => item.Action == AuditActions.MemberMembershipDeactivated
                && (item.EntityId == member.MemberId || item.EntityId == inactiveMember.MemberId), ct));
    }

    [Fact]
    public async Task DeactivationThroughTheEndpointUsesTheRealCommandAndShowsTheInactiveBadge()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "deactivate-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "deactivate-target", ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, target.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actor.Id, ct);

        var start = new DateTime(2099, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Deactivation Event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, actor.Id, ct);
        await SetQrTokenAsync(registration.RegistrationId, new string('d', 64), ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = CreateClient(factory);

        using (var scope = factory.Services.CreateScope())
        {
            Assert.IsType<MembershipAccessService>(scope.ServiceProvider.GetRequiredService<IMembershipAccessCommands>());
        }

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        using (var response = await DeactivateThroughEndpointAsync(client, member.MemberId, "Deactivation through the endpoint.", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using (var observer = Context())
        {
            var memberAfter = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == member.MemberId, ct);
            Assert.Equal(MembershipStatusCode.Inactive, memberAfter.MembershipStatusId);
            Assert.Equal(actor.Id, memberAfter.UpdatedBy);
            Assert.Equal(1, await observer.ActivityLogs.CountAsync(
                item => item.Action == AuditActions.MemberMembershipDeactivated && item.EntityId == member.MemberId, ct));

            var registrationAfter = await observer.Registrations.AsNoTracking()
                .SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
            Assert.Equal(RegistrationStatusCode.Cancelled, registrationAfter.RegistrationStatusId);
            Assert.Null(registrationAfter.QrToken);
        }

        var page = await client.GetStringAsync($"/Member?search={Uri.EscapeDataString(member.StudentNumber)}", ct);
        var row = MemberRow(page, member.MemberId);
        Assert.Contains("badge-inactive", row, StringComparison.Ordinal);
        Assert.Contains(">Inactive<", row, StringComparison.Ordinal);
    }

    private MembershipAccessService CreateService(AsiBasecodeDBContext context, ICurrentActor actor)
    {
        var unitOfWork = new UnitOfWork(context);
        var audit = new ActivityLogWriter(context, actor, TimeProvider.System);
        return new MembershipAccessService(
            new MemberRepository(unitOfWork),
            new UserRepository(unitOfWork),
            new RegistrationCancellationService(
                new RegistrationRepository(unitOfWork),
                new EventRepository(unitOfWork),
                unitOfWork,
                actor,
                audit,
                TimeProvider.System),
            unitOfWork,
            TimeProvider.System,
            actor,
            audit);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> host) => host.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private AsiBasecodeDBContext Context() => MemberEventTestSupport.Context(fixture.ConnectionString);

    private async Task<HttpResponseMessage> DeactivateThroughEndpointAsync(HttpClient targetClient, int memberId, string reason, CancellationToken cancellationToken)
    {
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(
            targetClient, $"/Member/{memberId}/access", cancellationToken);
        return await targetClient.PostAsync(
            $"/Member/{memberId}/access/deactivate",
            MemberEventTestSupport.Form(new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["__RequestVerificationToken"] = token
            }),
            cancellationToken);
    }

    private async Task SetQrTokenAsync(int registrationId, string token, CancellationToken cancellationToken)
    {
        await using var context = Context();
        var row = await context.Registrations.SingleAsync(item => item.RegistrationId == registrationId, cancellationToken);
        row.QrToken = token;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> SeedUserAsync(RoleCode role, string prefix, CancellationToken cancellationToken) =>
        await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"{prefix}-{Guid.NewGuid():N}@attendta.test", password, role, cancellationToken);

    private static string MemberRow(string html, int memberId)
    {
        var match = Regex.Match(html, $"<tr data-member-id=\"{memberId}\">(.*?)</tr>", RegexOptions.Singleline);
        Assert.True(match.Success, $"Member row {memberId} was not rendered.");
        return match.Value;
    }

    private sealed class TestActor(int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => role;
    }

    private sealed class AnonymousActor : ICurrentActor
    {
        public bool IsAuthenticated => false;
        public int UserId => throw new InvalidOperationException("Anonymous requests have no user id.");
        public string Role => null!;
    }
}
