using System.Net;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Membership reactivation against SQL Server: the MVC endpoint restores eligibility only, leaves
/// the linked account and cancelled registrations untouched, audits once, and lets an officer approve
/// a pending registration again through <see cref="ASI.Basecode.Data.Queries.ParticipationFacts"/>.
/// </summary>
public sealed class AdminMembershipReactivationTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture fixture;
    private string password = null!;
    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public AdminMembershipReactivationTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

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
    public async Task ReactivationRestoresEligibilityOnlyAndAuditsOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "reactivate-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "reactivate-target", ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, target.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actor.Id, ct);

        var start = new DateTime(2099, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var cancelledEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Reactivation Cancelled Event", ct);
        var pendingEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start.AddDays(1), start.AddDays(-1), 20, null, "Reactivation Pending Event", ct);

        var cancelled = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, cancelledEvent.EventId,
            member.MemberId, RegistrationStatusCode.Confirmed, actor.Id, ct);
        await SetQrTokenAsync(cancelled.RegistrationId, new string('e', 64), ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        using (var deactivated = await DeactivateThroughEndpointAsync(client, member.MemberId, "Suspended before reactivation.", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);
        }

        using (var noToken = await client.PostAsync(
            $"/Member/{member.MemberId}/access/reactivate",
            MemberEventTestSupport.Form(new Dictionary<string, string> { ["reason"] = "No token." }),
            ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        }

        using var targetClient = CreateClient(factory);
        await MemberEventTestSupport.SignInAsync(targetClient, target.UserId, password, ct);
        using (var feed = await targetClient.GetAsync("/MemberEvents", ct))
        {
            Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        }

        var pending = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, pendingEvent.EventId,
            member.MemberId, RegistrationStatusCode.Pending, actor.Id, ct);

        const string reason = "  Eligibility restored after review.  ";
        using (var reactivated = await ReactivateThroughEndpointAsync(client, member.MemberId, reason, ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, reactivated.StatusCode);
        }

        using (var feed = await targetClient.GetAsync("/MemberEvents", ct))
        {
            Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
        }

        await using (var observer = Context())
        {
            var memberAfter = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == member.MemberId, ct);
            Assert.Equal(MembershipStatusCode.Active, memberAfter.MembershipStatusId);
            Assert.Equal(actor.Id, memberAfter.UpdatedBy);
            Assert.NotNull(memberAfter.UpdatedAt);

            // Reactivation changes eligibility, never login access.
            var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
            Assert.True(user.IsActive);

            var cancelledAfter = await observer.Registrations.AsNoTracking()
                .SingleAsync(item => item.RegistrationId == cancelled.RegistrationId, ct);
            Assert.Equal(RegistrationStatusCode.Cancelled, cancelledAfter.RegistrationStatusId);
            Assert.Null(cancelledAfter.QrToken);

            var pendingAfter = await observer.Registrations.AsNoTracking()
                .SingleAsync(item => item.RegistrationId == pending.RegistrationId, ct);
            Assert.Equal(RegistrationStatusCode.Pending, pendingAfter.RegistrationStatusId);

            var audit = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(item => item.Action == AuditActions.MemberMembershipReactivated && item.EntityId == member.MemberId, ct);
            Assert.Equal("members", audit.EntityType);
            Assert.Equal(actor.Id, audit.ActorUserId);
            Assert.Equal(ActorTypeCode.User, audit.ActorTypeId);
            Assert.Equal("Eligibility restored after review.", audit.Reason);
            Assert.Contains("\"Status\":\"Inactive\"", audit.OldValues ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("\"Status\":\"Active\"", audit.NewValues ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(1, await observer.ActivityLogs.CountAsync(
                item => item.Action == AuditActions.MemberMembershipReactivated && item.EntityId == member.MemberId, ct));
        }

        using (var approved = await ApproveThroughEndpointAsync(client, pending.RegistrationId, ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, approved.StatusCode);
        }

        await using (var observer = Context())
        {
            var pendingAfter = await observer.Registrations.AsNoTracking()
                .SingleAsync(item => item.RegistrationId == pending.RegistrationId, ct);
            Assert.Equal(RegistrationStatusCode.Confirmed, pendingAfter.RegistrationStatusId);
            Assert.Equal(actor.Id, pendingAfter.ConfirmedBy);
        }

        await using (var context = Context())
        {
            var service = CreateService(context, new TestActor(actor.Id, RoleCodes.Officer));

            var blank = await Assert.ThrowsAsync<DomainException>(
                () => service.ReactivateAsync(member.MemberId, "   ", ct));
            Assert.Equal(OutcomeCode.Invalid, blank.Outcome);
            Assert.Equal("reason", blank.Field);

            var overlong = await Assert.ThrowsAsync<DomainException>(
                () => service.ReactivateAsync(member.MemberId, new string('x', 1001), ct));
            Assert.Equal(OutcomeCode.Invalid, overlong.Outcome);
            Assert.Equal("reason", overlong.Field);

            var alreadyActive = await Assert.ThrowsAsync<DomainException>(
                () => service.ReactivateAsync(member.MemberId, "Already active.", ct));
            Assert.Equal(OutcomeCode.Invalid, alreadyActive.Outcome);
            Assert.Equal("This membership is already active.", alreadyActive.Message);

            var unknown = await Assert.ThrowsAsync<DomainException>(
                () => service.ReactivateAsync(int.MaxValue, "Unknown member.", ct));
            Assert.Equal(OutcomeCode.NotFound, unknown.Outcome);
            Assert.Equal("Member not found.", unknown.Message);
        }

        await using (var context = Context())
        {
            var memberService = CreateService(context, new TestActor(target.Id, RoleCodes.Member));
            var forbidden = await Assert.ThrowsAsync<DomainException>(
                () => memberService.ReactivateAsync(member.MemberId, "Member attempt.", ct));
            Assert.Equal(OutcomeCode.Forbidden, forbidden.Outcome);
            Assert.Equal("Access denied.", forbidden.Message);
        }

        await using (var observer = Context())
        {
            Assert.Equal(1, await observer.ActivityLogs.CountAsync(
                item => item.Action == AuditActions.MemberMembershipReactivated && item.EntityId == member.MemberId, ct));
        }
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

    private async Task<HttpResponseMessage> ReactivateThroughEndpointAsync(HttpClient targetClient, int memberId, string reason, CancellationToken cancellationToken)
    {
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(
            targetClient, $"/Member/{memberId}/access", cancellationToken);
        return await targetClient.PostAsync(
            $"/Member/{memberId}/access/reactivate",
            MemberEventTestSupport.Form(new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["__RequestVerificationToken"] = token
            }),
            cancellationToken);
    }

    private async Task<HttpResponseMessage> ApproveThroughEndpointAsync(HttpClient targetClient, int registrationId, CancellationToken cancellationToken)
    {
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(
            targetClient, "/RegistrationReview/Index?status=pending", cancellationToken);
        return await targetClient.PostAsync(
            "/RegistrationReview/Approve",
            MemberEventTestSupport.Form(new Dictionary<string, string>
            {
                ["id"] = registrationId.ToString(),
                ["status"] = "pending",
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

    private sealed class TestActor(int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => role;
    }
}
