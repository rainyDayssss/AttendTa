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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Account deactivation against SQL Server: the MVC endpoint cascade, the last-administrator app
/// lock under parallel deactivations, the next-request session refusal, and the refusals.
/// </summary>
public sealed class AdminAccountDeactivationTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture fixture;
    private string password = null!;
    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public AdminAccountDeactivationTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public ValueTask InitializeAsync()
    {
        password = MemberEventTestSupport.NewPassword();
        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = CreateClient();
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
    public async Task OfficerDeactivationCancelsUpcomingRegistrationsAndAuditsOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "deactivate-actor", ct);
        var officer = await SeedUserAsync(RoleCode.Officer, "deactivate-officer", ct);
        var officerMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, officer.Id, MembershipStatusCode.Active, ct);
        var waitUser = await SeedUserAsync(RoleCode.Member, "deactivate-wait", ct);
        var waitMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, waitUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actor.Id, ct);

        var start = new DateTime(2099, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var confirmedEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Deactivation Confirmed Event", ct);
        var pendingEvent = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start.AddDays(1), start.AddDays(-1), 20, null, "Deactivation Pending Event", ct);

        var confirmed = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, confirmedEvent.EventId, officerMember.MemberId, RegistrationStatusCode.Confirmed, actor.Id, ct);
        var pending = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, pendingEvent.EventId, officerMember.MemberId, RegistrationStatusCode.Pending, actor.Id, ct);
        var waitlisted = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, confirmedEvent.EventId, waitMember.MemberId, RegistrationStatusCode.Waitlisted, actor.Id, ct);
        await SetQrTokenAsync(confirmed.RegistrationId, new string('a', 64), ct);
        await SetQrTokenAsync(pending.RegistrationId, new string('b', 64), ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        const string reason = "  Account deactivated for cascade test.  ";
        using var response = await DeactivateThroughEndpointAsync(client, officer.Id, reason, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/accounts", MemberEventTestSupport.LocationPath(response));

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == officer.Id, ct);
        Assert.False(user.IsActive);
        Assert.Equal(actor.Id, user.UpdatedById);
        Assert.NotNull(user.UpdatedTime);

        var cancelled = await observer.Registrations.AsNoTracking()
            .Where(item => item.RegistrationId == confirmed.RegistrationId || item.RegistrationId == pending.RegistrationId)
            .ToListAsync(ct);
        Assert.Equal(2, cancelled.Count);
        Assert.All(cancelled, row =>
        {
            Assert.Equal(RegistrationStatusCode.Cancelled, row.RegistrationStatusId);
            Assert.Null(row.QrToken);
            Assert.Equal(actor.Id, row.CancelledBy);
            Assert.Equal("Account deactivated for cascade test.", row.CancellationReason);
            Assert.NotNull(row.CancelledAt);
        });

        var stillWaitlisted = await observer.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == waitlisted.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Waitlisted, stillWaitlisted.RegistrationStatusId);

        var audit = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(item => item.Action == AuditActions.UserDeactivated && item.EntityId == officer.Id, ct);
        Assert.Equal("users", audit.EntityType);
        Assert.Equal(actor.Id, audit.ActorUserId);
        Assert.Equal(ActorTypeCode.User, audit.ActorTypeId);
        Assert.Equal("Account deactivated for cascade test.", audit.Reason);
        Assert.Contains("\"IsActive\":true", audit.OldValues ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"IsActive\":false", audit.NewValues ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(
            item => item.Action == AuditActions.UserDeactivated && item.EntityId == officer.Id, ct));

        var cancellationLogs = await observer.ActivityLogs.AsNoTracking()
            .Where(item => item.Action == AuditActions.RegistrationCancelled
                && (item.EntityId == confirmed.RegistrationId || item.EntityId == pending.RegistrationId))
            .ToListAsync(ct);
        Assert.Equal(2, cancellationLogs.Count);
        Assert.All(cancellationLogs, log => Assert.Contains(
            "\"Cause\":\"AccountDeactivated\"", log.NewValues ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParallelDeactivationsCommitExactlyOneAndKeepOneActiveAdministrator()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var first = await SeedUserAsync(RoleCode.Admin, "parallel-deactivate-a", ct);
        var second = await SeedUserAsync(RoleCode.Admin, "parallel-deactivate-b", ct);

        async Task<(OutcomeCode? Outcome, string? Message)> DeactivateAsync(int actorId, int targetId)
        {
            await using var context = Context();
            var service = CreateService(context, new TestActor(actorId, RoleCodes.Admin));
            try
            {
                await service.DeactivateAsync(targetId, "Concurrent deactivation.", ct);
                return (null, null);
            }
            catch (DomainException exception)
            {
                return (exception.Outcome, exception.Message);
            }
        }

        var outcomes = await Task.WhenAll(
            DeactivateAsync(first.Id, second.Id),
            DeactivateAsync(second.Id, first.Id));

        Assert.Single(outcomes, outcome => outcome.Outcome is null);
        var loser = Assert.Single(outcomes, outcome => outcome.Outcome == OutcomeCode.Stale);
        Assert.Contains("last active Administrator", loser.Message ?? string.Empty, StringComparison.Ordinal);

        await using var observer = Context();
        Assert.Equal(1, await observer.Users.CountAsync(item => item.IsActive && item.RoleId == RoleCode.Admin, ct));
        Assert.Equal(1, await observer.Users.CountAsync(
            item => item.IsActive && (item.Id == first.Id || item.Id == second.Id), ct));
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(
            item => item.Action == AuditActions.UserDeactivated && (item.EntityId == first.Id || item.EntityId == second.Id), ct));
    }

    [Fact]
    public async Task DeactivatedSessionIsRejectedOnTheNextRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "session-actor", ct);
        var officer = await SeedUserAsync(RoleCode.Officer, "session-officer", ct);

        using var officerClient = CreateClient();
        await MemberEventTestSupport.SignInAsync(officerClient, officer.UserId, password, ct);
        Assert.Equal(HttpStatusCode.OK, (await officerClient.GetAsync("/Member", ct)).StatusCode);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        using var response = await DeactivateThroughEndpointAsync(client, officer.Id, "Session refusal test.", ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var afterDeactivation = await officerClient.GetAsync("/Member", ct);
        Assert.Equal(HttpStatusCode.Redirect, afterDeactivation.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(afterDeactivation), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BlankAndOverlongReasonsAreRefusedWithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "validation-deactivate-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "validation-deactivate-target", ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);

        using (var blank = await DeactivateThroughEndpointAsync(client, target.Id, "   ", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, blank.StatusCode);
        }

        var blankList = await client.GetStringAsync("/admin/accounts", ct);
        Assert.Contains("A reason is required.", blankList, StringComparison.Ordinal);

        using (var overlong = await DeactivateThroughEndpointAsync(client, target.Id, new string('x', 1001), ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, overlong.StatusCode);
        }

        var overlongList = await client.GetStringAsync("/admin/accounts", ct);
        Assert.Contains("The reason must be 1000 characters or fewer.", overlongList, StringComparison.Ordinal);

        await AssertNoDeactivationAsync(target.Id, ct);
    }

    [Fact]
    public async Task AlreadyInactiveAccountAndLastActiveAdministratorAreRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "refusal-admin", ct);
        var inactive = await SeedUserAsync(RoleCode.Member, "refusal-inactive", ct);
        await SetActiveAsync(inactive.Id, false, ct);

        await using (var context = Context())
        {
            var service = CreateService(context, new TestActor(actor.Id, RoleCodes.Admin));
            var alreadyInactive = await Assert.ThrowsAsync<DomainException>(
                () => service.DeactivateAsync(inactive.Id, "Already inactive.", ct));
            Assert.Equal(OutcomeCode.Invalid, alreadyInactive.Outcome);
            Assert.Equal("This account is already inactive.", alreadyInactive.Message);

            var lastAdministrator = await Assert.ThrowsAsync<DomainException>(
                () => service.DeactivateAsync(actor.Id, "Step down.", ct));
            Assert.Equal(OutcomeCode.Stale, lastAdministrator.Outcome);
            Assert.Equal("The last active Administrator cannot be deactivated.", lastAdministrator.Message);
        }

        await using var observer = Context();
        Assert.True((await observer.Users.AsNoTracking().SingleAsync(item => item.Id == actor.Id, ct)).IsActive);
        Assert.False((await observer.Users.AsNoTracking().SingleAsync(item => item.Id == inactive.Id, ct)).IsActive);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(
            item => item.Action == AuditActions.UserDeactivated && (item.EntityId == actor.Id || item.EntityId == inactive.Id), ct));
    }

    [Fact]
    public async Task NonAdministratorCallerIsRefusedWithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        var officer = await SeedUserAsync(RoleCode.Officer, "forbidden-deactivate-caller", ct);
        var target = await SeedUserAsync(RoleCode.Member, "forbidden-deactivate-target", ct);

        await using (var context = Context())
        {
            var service = CreateService(context, new TestActor(officer.Id, RoleCodes.Officer));
            var refusal = await Assert.ThrowsAsync<DomainException>(
                () => service.DeactivateAsync(target.Id, "Officer attempt.", ct));
            Assert.Equal(OutcomeCode.Forbidden, refusal.Outcome);
            Assert.Equal("Access denied.", refusal.Message);
        }

        await AssertNoDeactivationAsync(target.Id, ct);
    }

    private AccountAccessService CreateService(AsiBasecodeDBContext context, ICurrentActor actor)
    {
        var unitOfWork = new UnitOfWork(context);
        var audit = new ActivityLogWriter(context, actor, TimeProvider.System);
        return new AccountAccessService(
            new AccountAdministrationRepository(unitOfWork),
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

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private AsiBasecodeDBContext Context() => MemberEventTestSupport.Context(fixture.ConnectionString);

    private async Task<HttpResponseMessage> DeactivateThroughEndpointAsync(HttpClient targetClient, int accountId, string reason, CancellationToken cancellationToken)
    {
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(
            targetClient, $"/admin/accounts/{accountId}/access", cancellationToken);
        return await targetClient.PostAsync(
            $"/admin/accounts/{accountId}/access/deactivate",
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

    private async Task SetActiveAsync(int userId, bool isActive, CancellationToken cancellationToken)
    {
        await using var context = Context();
        var user = await context.Users.SingleAsync(item => item.Id == userId, cancellationToken);
        user.IsActive = isActive;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task AssertNoDeactivationAsync(int accountId, CancellationToken cancellationToken)
    {
        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == accountId, cancellationToken);
        Assert.True(user.IsActive);
        Assert.Null(user.UpdatedById);
        Assert.Null(user.UpdatedTime);
        Assert.False(await observer.ActivityLogs.AnyAsync(
            item => item.Action == AuditActions.UserDeactivated && item.EntityId == accountId, cancellationToken));
    }

    private async Task<User> SeedUserAsync(RoleCode role, string prefix, CancellationToken cancellationToken) =>
        await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"{prefix}-{Guid.NewGuid():N}@attendta.test", password, role, cancellationToken);

    /// <summary>Keeps the active-Administrator count deterministic for every test in this class.</summary>
    private async Task ResetAdministratorsAsync(CancellationToken cancellationToken)
    {
        await using var context = Context();
        await context.Users
            .Where(user => user.RoleId == RoleCode.Admin)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.RoleId, RoleCode.Member), cancellationToken);
    }

    private sealed class TestActor(int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => role;
    }
}
