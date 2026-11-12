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
/// Account reactivation against SQL Server: the MVC endpoint restores login access only, leaves
/// cancelled registrations and issued certificates untouched, audits once, and refuses the same
/// inputs the screen expects. The last case proves the demo switch no longer masks the real row.
/// </summary>
public sealed class AdminAccountReactivationTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture fixture;
    private string password = null!;
    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public AdminAccountReactivationTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

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
    public async Task ReactivationRestoresLoginAccessAndLeavesRegistrationsAndCertificatesUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "reactivate-actor", ct);
        var target = await SeedUserAsync(RoleCode.Officer, "reactivate-target", ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, target.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actor.Id, ct);

        var start = new DateTime(2099, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actor.Id, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Reactivation Event", ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId,
            member.MemberId, RegistrationStatusCode.Confirmed, actor.Id, ct);
        await SetQrTokenAsync(registration.RegistrationId, new string('c', 64), ct);
        var certificateId = await SeedIssuedCertificateAsync(registration.RegistrationId, eventItem, actor.Id, ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        const string deactivationReason = "  Account deactivated before the reactivation test.  ";
        using (var deactivated = await DeactivateThroughEndpointAsync(client, target.Id, deactivationReason, ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);
        }

        const string reactivationReason = "  Access restored after review.  ";
        using var reactivated = await ReactivateThroughEndpointAsync(client, target.Id, reactivationReason, ct);
        Assert.Equal(HttpStatusCode.Redirect, reactivated.StatusCode);
        Assert.Equal("/admin/accounts", MemberEventTestSupport.LocationPath(reactivated));

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
        Assert.True(user.IsActive);
        Assert.Equal(actor.Id, user.UpdatedById);
        Assert.NotNull(user.UpdatedTime);

        var registrationAfter = await observer.Registrations.AsNoTracking()
            .SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Cancelled, registrationAfter.RegistrationStatusId);
        Assert.Null(registrationAfter.QrToken);
        Assert.Equal(actor.Id, registrationAfter.CancelledBy);
        Assert.Equal("Account deactivated before the reactivation test.", registrationAfter.CancellationReason);

        var certificateAfter = await observer.Certificates.AsNoTracking()
            .SingleAsync(item => item.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.Issued, certificateAfter.CertificateStatusId);
        Assert.Equal($"CERT-{registration.RegistrationId:D6}", certificateAfter.CertificateNumber);
        Assert.Equal("Reactivation recipient", certificateAfter.RecipientNameSnapshot);
        Assert.Equal("uploads/certificates/reactivation-test.pdf", certificateAfter.FilePath);
        Assert.Equal(actor.Id, certificateAfter.IssuedBy);
        Assert.NotNull(certificateAfter.IssuedAt);

        var audit = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(item => item.Action == AuditActions.UserReactivated && item.EntityId == target.Id, ct);
        Assert.Equal("users", audit.EntityType);
        Assert.Equal(actor.Id, audit.ActorUserId);
        Assert.Equal(ActorTypeCode.User, audit.ActorTypeId);
        Assert.Equal("Access restored after review.", audit.Reason);
        Assert.Contains("\"IsActive\":false", audit.OldValues ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"IsActive\":true", audit.NewValues ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(
            item => item.Action == AuditActions.UserReactivated && item.EntityId == target.Id, ct));
    }

    [Fact]
    public async Task ReactivatedAccountSignsInAndReachesTheMemberSurface()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "signin-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "signin-target", ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        using (var deactivated = await DeactivateThroughEndpointAsync(client, target.Id, "Temporary suspension.", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);
        }

        using var targetClient = CreateClient(factory);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(targetClient, "/Account/Login", ct);
        using (var refused = await targetClient.PostAsync("/Account/Login", MemberEventTestSupport.Form(new Dictionary<string, string>
        {
            ["Email"] = target.UserId,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), ct))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        }

        using (var reactivated = await ReactivateThroughEndpointAsync(client, target.Id, "Suspension lifted.", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, reactivated.StatusCode);
        }

        await MemberEventTestSupport.SignInAsync(targetClient, target.UserId, password, ct);
        using var feed = await targetClient.GetAsync("/MemberEvents", ct);
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
    }

    [Fact]
    public async Task BlankOverlongAlreadyActiveUnknownAndNonAdministratorAreRefusedWithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "refusal-reactivate-actor", ct);
        var inactive = await SeedUserAsync(RoleCode.Member, "refusal-reactivate-inactive", ct);
        await SetActiveAsync(inactive.Id, false, ct);
        var active = await SeedUserAsync(RoleCode.Member, "refusal-reactivate-active", ct);

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);

        using (var blank = await ReactivateThroughEndpointAsync(client, inactive.Id, "   ", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, blank.StatusCode);
        }

        var blankList = await client.GetStringAsync("/admin/accounts", ct);
        Assert.Contains("A reason is required.", blankList, StringComparison.Ordinal);

        using (var overlong = await ReactivateThroughEndpointAsync(client, inactive.Id, new string('x', 1001), ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, overlong.StatusCode);
        }

        var overlongList = await client.GetStringAsync("/admin/accounts", ct);
        Assert.Contains("The reason must be 1000 characters or fewer.", overlongList, StringComparison.Ordinal);

        await using (var context = Context())
        {
            var service = CreateService(context, new TestActor(actor.Id, RoleCodes.Admin));

            var alreadyActive = await Assert.ThrowsAsync<DomainException>(
                () => service.ReactivateAsync(active.Id, "Already active.", ct));
            Assert.Equal(OutcomeCode.Invalid, alreadyActive.Outcome);
            Assert.Equal("This account is already active.", alreadyActive.Message);

            var unknown = await Assert.ThrowsAsync<DomainException>(
                () => service.ReactivateAsync(int.MaxValue, "Unknown account.", ct));
            Assert.Equal(OutcomeCode.NotFound, unknown.Outcome);
            Assert.Equal("Account not found.", unknown.Message);
        }

        await using (var context = Context())
        {
            var officerService = CreateService(context, new TestActor(active.Id, RoleCodes.Officer));
            var forbidden = await Assert.ThrowsAsync<DomainException>(
                () => officerService.ReactivateAsync(inactive.Id, "Officer attempt.", ct));
            Assert.Equal(OutcomeCode.Forbidden, forbidden.Outcome);
            Assert.Equal("Access denied.", forbidden.Message);
        }

        await using var observer = Context();
        var stillInactive = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == inactive.Id, ct);
        Assert.False(stillInactive.IsActive);
        Assert.Null(stillInactive.UpdatedById);
        Assert.Null(stillInactive.UpdatedTime);
        Assert.False(await observer.ActivityLogs.AnyAsync(
            item => item.Action == AuditActions.UserReactivated
                && (item.EntityId == inactive.Id || item.EntityId == active.Id), ct));
    }

    [Fact]
    public async Task ReactivationThroughTheEndpointUsesTheRealCommandAndShowsTheActiveBadge()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "reactivate-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "reactivate-target", ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = CreateClient(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var commands = scope.ServiceProvider.GetRequiredService<IAccountAccessCommands>();
            Assert.IsType<AccountAccessService>(commands);
        }

        await MemberEventTestSupport.SignInAsync(client, actor.UserId, password, ct);
        using (var deactivated = await DeactivateThroughEndpointAsync(client, target.Id, "Deactivation through the endpoint.", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);
        }

        using (var reactivated = await ReactivateThroughEndpointAsync(client, target.Id, "Reactivation through the endpoint.", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, reactivated.StatusCode);
        }

        await using (var observer = Context())
        {
            var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
            Assert.True(user.IsActive);
            Assert.Equal(actor.Id, user.UpdatedById);
            Assert.Equal(1, await observer.ActivityLogs.CountAsync(
                item => item.Action == AuditActions.UserReactivated && item.EntityId == target.Id, ct));
        }

        var page = await client.GetStringAsync(
            $"/admin/accounts?search={Uri.EscapeDataString(target.UserId)}", ct);
        var row = AccountRow(page, target.Id);
        Assert.Contains("badge-active", row, StringComparison.Ordinal);
        Assert.Contains(">Active<", row, StringComparison.Ordinal);
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> host) => host.CreateClient(new WebApplicationFactoryClientOptions
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

    private async Task<HttpResponseMessage> ReactivateThroughEndpointAsync(HttpClient targetClient, int accountId, string reason, CancellationToken cancellationToken)
    {
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(
            targetClient, $"/admin/accounts/{accountId}/access", cancellationToken);
        return await targetClient.PostAsync(
            $"/admin/accounts/{accountId}/access/reactivate",
            MemberEventTestSupport.Form(new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["__RequestVerificationToken"] = token
            }),
            cancellationToken);
    }

    private async Task<int> SeedIssuedCertificateAsync(int registrationId, Event eventItem, int actorId, CancellationToken cancellationToken)
    {
        await using var context = Context();
        var certificate = new Certificate
        {
            RegistrationId = registrationId,
            CertificateStatusId = CertificateStatusCode.Issued,
            CertificateNumber = $"CERT-{registrationId:D6}",
            RecipientNameSnapshot = "Reactivation recipient",
            EventTitleSnapshot = eventItem.Title,
            EventStartsAtSnapshot = eventItem.StartsAt,
            IssuerNameSnapshot = "AttendTa",
            FilePath = "uploads/certificates/reactivation-test.pdf",
            IssuedBy = actorId,
            IssuedAt = DateTime.UtcNow.AddDays(-1),
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.Certificates.Add(certificate);
        await context.SaveChangesAsync(cancellationToken);
        return certificate.CertificateId;
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

    private async Task<User> SeedUserAsync(RoleCode role, string prefix, CancellationToken cancellationToken) =>
        await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"{prefix}-{Guid.NewGuid():N}@attendta.test", password, role, cancellationToken);

    private static string AccountRow(string html, int accountId)
    {
        var match = Regex.Match(html, $"<tr data-account-id=\"{accountId}\">(.*?)</tr>", RegexOptions.Singleline);
        Assert.True(match.Success, $"Account row {accountId} was not rendered.");
        return match.Value;
    }

    private sealed class TestActor(int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => role;
    }
}
