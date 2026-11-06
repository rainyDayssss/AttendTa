using System.Net;
using System.Data.Common;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class RegistrationReviewTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Start = new(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ApprovalAfterDeadlineConfirmsWithSafeAuditAndMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        await Service(context, setup.Officer.Id, Start.AddHours(-1)).ApproveAsync(row.RegistrationId, ct);
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, actual.RegistrationStatusId);
        Assert.Matches("^[0-9a-f]{64}$", actual.QrToken!);
        Assert.Equal(setup.Officer.Id, actual.ReviewedBy);
        Assert.Equal(setup.Officer.Id, actual.ConfirmedBy);
        Assert.Equal(Start.AddHours(-1), actual.ReviewedAt);
        Assert.Equal(actual.ReviewedAt, actual.ConfirmedAt);
        var log = await context.ActivityLogs.SingleAsync(l => l.EntityId == row.RegistrationId && l.EntityType == "registrations", ct);
        Assert.Equal("registration.approved", log.Action);
        Assert.DoesNotContain(actual.QrToken!, log.NewValues!, StringComparison.Ordinal);
        Assert.DoesNotContain("token", log.NewValues!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeparateConfirmedApprovalsProduceDistinctQrTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct, limit: 2);
        var first = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var second = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.Officer.Id, Start.AddHours(-1));
        await service.ApproveAsync(first.RegistrationId, ct);
        await service.ApproveAsync(second.RegistrationId, ct);
        context.ChangeTracker.Clear();
        var rows = await context.Registrations.Where(r => r.EventId == setup.Event.EventId).ToListAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => { Assert.Equal(RegistrationStatusCode.Confirmed, r.RegistrationStatusId); Assert.Matches("^[0-9a-f]{64}$", r.QrToken!); });
        Assert.NotEqual(rows[0].QrToken, rows[1].QrToken);
    }

    [Theory]
    [InlineData("full")]
    [InlineData("eligible-waitlist")]
    [InlineData("ineligible-waitlist")]
    public async Task SeatAllocationRespectsCapacityAndEligibleWaitlist(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct, limit: 1);
        var first = await AddRegistrationAsync(setup, scenario == "full" ? RegistrationStatusCode.Confirmed : RegistrationStatusCode.Waitlisted, ct);
        var second = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        if (scenario == "ineligible-waitlist")
        {
            var member = await context.Members.SingleAsync(m => m.MemberId == first.MemberId, ct);
            member.MembershipStatusId = MembershipStatusCode.Inactive;
            await context.SaveChangesAsync(ct);
            context.ChangeTracker.Clear();
        }
        await Service(context, setup.Officer.Id, Start.AddHours(-1)).ApproveAsync(second.RegistrationId, ct);
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == second.RegistrationId, ct);
        Assert.Equal(scenario == "ineligible-waitlist" ? RegistrationStatusCode.Confirmed : RegistrationStatusCode.Waitlisted, actual.RegistrationStatusId);
        if (scenario != "ineligible-waitlist") { Assert.Null(actual.QrToken); Assert.Null(actual.ConfirmedAt); Assert.NotNull(actual.ReviewedAt); }
        Assert.Equal(scenario == "full" ? RegistrationStatusCode.Confirmed : RegistrationStatusCode.Waitlisted,
            (await context.Registrations.SingleAsync(r => r.RegistrationId == first.RegistrationId, ct)).RegistrationStatusId);
    }

    [Theory]
    [InlineData("cutoff")]
    [InlineData("event")]
    [InlineData("membership")]
    [InlineData("account")]
    [InlineData("status")]
    public async Task StaleApprovalGuardsLeaveNoChanges(string scenario)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, scenario == "status" ? RegistrationStatusCode.Waitlisted : RegistrationStatusCode.Pending, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        if (scenario == "event") (await context.Events.SingleAsync(e => e.EventId == setup.Event.EventId, ct)).EventStatusId = EventStatusCode.Cancelled;
        if (scenario == "membership") (await context.Members.SingleAsync(m => m.MemberId == row.MemberId, ct)).MembershipStatusId = MembershipStatusCode.Inactive;
        if (scenario == "account")
        {
            var ownerId = await context.Members.Where(m => m.MemberId == row.MemberId).Select(m => m.UserId).SingleAsync(ct);
            (await context.Users.SingleAsync(u => u.Id == ownerId, ct)).IsActive = false;
        }
        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DomainException>(() => Service(context, setup.Officer.Id, scenario == "cutoff" ? Start : Start.AddTicks(-1)).ApproveAsync(row.RegistrationId, ct));
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(row.RegistrationStatusId, actual.RegistrationStatusId);
        Assert.Null(actual.ReviewedAt);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclineAndAdminCancellationRequireReasonAndWriteBothAudits(bool adminCancel)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, adminCancel ? RegistrationStatusCode.Confirmed : RegistrationStatusCode.Pending, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        if (adminCancel)
        {
            (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).QrToken = new string('a', 64);
            await context.SaveChangesAsync(ct);
            context.ChangeTracker.Clear();
        }
        var service = Service(context, setup.Officer.Id, Start.AddHours(-1));
        Task Decision(string reason) => adminCancel ? service.CancelAsync(row.RegistrationId, reason, ct) : service.DeclineAsync(row.RegistrationId, reason, ct);
        await Assert.ThrowsAsync<DomainException>(() => Decision("  "));
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
        await Decision("  test reason  ");
        context.ChangeTracker.Clear();
        var result = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Cancelled, result.RegistrationStatusId);
        Assert.Null(result.QrToken);
        Assert.Equal("test reason", result.CancellationReason);
        if (!adminCancel) { Assert.Equal(setup.Officer.Id, result.ReviewedBy); Assert.Equal("test reason", result.ReviewNotes); }
        var logs = await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct);
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.Action == "registration.cancelled");
        Assert.Contains(logs, l => l.Action == (adminCancel ? "registration.admin_cancelled" : "registration.declined"));
        Assert.All(logs, l => Assert.DoesNotContain("token", l.NewValues!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdministrativeCancellationOfWaitlistedRegistrationSucceedsWithoutPromotingOthers()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, ct);
        var other = await AddRegistrationAsync(setup, RegistrationStatusCode.Waitlisted, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        await Service(context, setup.Officer.Id, Start.AddTicks(-1)).CancelAsync(row.RegistrationId, "  administrative reason  ", ct);
        context.ChangeTracker.Clear();
        var result = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Cancelled, result.RegistrationStatusId);
        Assert.Equal("administrative reason", result.CancellationReason);
        Assert.Equal(setup.Officer.Id, result.CancelledBy);
        Assert.Null(result.QrToken);
        Assert.Equal(RegistrationStatusCode.Waitlisted, (await context.Registrations.SingleAsync(r => r.RegistrationId == other.RegistrationId, ct)).RegistrationStatusId);
        var logs = await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct);
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.Action == "registration.cancelled");
        Assert.Contains(logs, l => l.Action == "registration.admin_cancelled");
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == other.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData(false, RegistrationStatusCode.Confirmed)]
    [InlineData(false, RegistrationStatusCode.Waitlisted)]
    [InlineData(false, RegistrationStatusCode.Cancelled)]
    [InlineData(true, RegistrationStatusCode.Pending)]
    [InlineData(true, RegistrationStatusCode.Cancelled)]
    public async Task DeclineAndAdministrativeCancellationRejectInvalidRegistrationStates(bool adminCancel, RegistrationStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, status, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.Officer.Id, Start.AddHours(-1));
        var error = await Assert.ThrowsAsync<DomainException>(() => adminCancel
            ? service.CancelAsync(row.RegistrationId, "reason", ct)
            : service.DeclineAsync(row.RegistrationId, "reason", ct));
        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        context.ChangeTracker.Clear();
        Assert.Equal(status, (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData(false, RegistrationStatusCode.Pending, false)]
    [InlineData(false, RegistrationStatusCode.Pending, true)]
    [InlineData(true, RegistrationStatusCode.Confirmed, false)]
    [InlineData(true, RegistrationStatusCode.Confirmed, true)]
    [InlineData(true, RegistrationStatusCode.Waitlisted, false)]
    [InlineData(true, RegistrationStatusCode.Waitlisted, true)]
    public async Task DeclineAndAdminCancellationAtOrAfterStartRefuseAtomically(bool adminCancel, RegistrationStatusCode status, bool afterStart)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, status, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.Officer.Id, afterStart ? Start.AddTicks(1) : Start);
        var error = await Assert.ThrowsAsync<DomainException>(() => adminCancel
            ? service.CancelAsync(row.RegistrationId, "reason", ct)
            : service.DeclineAsync(row.RegistrationId, "reason", ct));
        Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(status, actual.RegistrationStatusId);
        Assert.Null(actual.ReviewedAt);
        Assert.Null(actual.CancelledAt);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData(false, RegistrationStatusCode.Pending, EventStatusCode.Draft)]
    [InlineData(false, RegistrationStatusCode.Pending, EventStatusCode.Cancelled)]
    [InlineData(false, RegistrationStatusCode.Pending, EventStatusCode.Completed)]
    [InlineData(true, RegistrationStatusCode.Confirmed, EventStatusCode.Draft)]
    [InlineData(true, RegistrationStatusCode.Confirmed, EventStatusCode.Cancelled)]
    [InlineData(true, RegistrationStatusCode.Confirmed, EventStatusCode.Completed)]
    [InlineData(true, RegistrationStatusCode.Waitlisted, EventStatusCode.Draft)]
    [InlineData(true, RegistrationStatusCode.Waitlisted, EventStatusCode.Cancelled)]
    [InlineData(true, RegistrationStatusCode.Waitlisted, EventStatusCode.Completed)]
    public async Task DeclineAndAdminCancellationRejectNonpublishedEvents(bool adminCancel, RegistrationStatusCode status, EventStatusCode eventStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, status, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        (await context.Events.SingleAsync(e => e.EventId == setup.Event.EventId, ct)).EventStatusId = eventStatus;
        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
        var service = Service(context, setup.Officer.Id, Start.AddTicks(-1));
        var error = await Assert.ThrowsAsync<DomainException>(() => adminCancel
            ? service.CancelAsync(row.RegistrationId, "reason", ct)
            : service.DeclineAsync(row.RegistrationId, "reason", ct));
        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(status, actual.RegistrationStatusId);
        Assert.Null(actual.ReviewedAt);
        Assert.Null(actual.CancelledAt);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Confirmed)]
    [InlineData(RegistrationStatusCode.Waitlisted)]
    public async Task ValidAttendancePreventsAdministrativeCancellation(RegistrationStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, status, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        context.Set<Attendance>().Add(new Attendance { RegistrationId = row.RegistrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = setup.Officer.Id, RecordedAt = Start });
        await context.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<DomainException>(() => Service(context, setup.Officer.Id, Start.AddHours(-1)).CancelAsync(row.RegistrationId, "reason", ct));
        context.ChangeTracker.Clear();
        Assert.Equal(status, (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditFailureRollsBackApprovalOrBothCancellationAudits(bool decline)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.Officer.Id, Start.AddHours(-1), failAuditCall: decline ? 2 : 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => decline ? service.DeclineAsync(row.RegistrationId, "reason", ct) : service.ApproveAsync(row.RegistrationId, ct));
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Pending, actual.RegistrationStatusId);
        Assert.Null(actual.ReviewedAt);
        Assert.Null(actual.QrToken);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Fact]
    public async Task ConcurrentApprovalsSerializeTheLastSeatAcrossSqlConnections()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct, 1);
        var first = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var second = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await using var blocker = MemberEventTestSupport.Context(fixture.ConnectionString);
        await using var transaction = await blocker.Database.BeginTransactionAsync(ct);
        await new EventRepository(new UnitOfWork(blocker)).LockEventAsync(setup.Event.EventId, ct);
        var gate = new EventLockAttempts(2);
        await using var context1 = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        await using var context2 = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var approval1 = Service(context1, setup.Officer.Id, Start.AddHours(-1)).ApproveAsync(first.RegistrationId, ct);
        var approval2 = Service(context2, setup.Officer.Id, Start.AddHours(-1)).ApproveAsync(second.RegistrationId, ct);
        var committed = false;
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            Assert.False(approval1.IsCompleted);
            Assert.False(approval2.IsCompleted);
            await transaction.CommitAsync(ct);
            committed = true;
            await Task.WhenAll(approval1, approval2).WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
        finally { if (!committed) await transaction.RollbackAsync(CancellationToken.None); }
        await using var verify = MemberEventTestSupport.Context(fixture.ConnectionString);
        var rows = await verify.Registrations.Where(r => r.EventId == setup.Event.EventId).ToListAsync(ct);
        Assert.Single(rows, r => r.RegistrationStatusId == RegistrationStatusCode.Confirmed && r.QrToken != null);
        Assert.Single(rows, r => r.RegistrationStatusId == RegistrationStatusCode.Waitlisted && r.QrToken == null);
        Assert.Equal(2, await verify.ActivityLogs.CountAsync(l => l.Action == "registration.approved" && (l.EntityId == first.RegistrationId || l.EntityId == second.RegistrationId), ct));
    }

    [Fact]
    public async Task WaitingForEventLockRechecksStateAndCapturesTimeAfterLock()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await using var blocker = MemberEventTestSupport.Context(fixture.ConnectionString);
        await using var transaction = await blocker.Database.BeginTransactionAsync(ct);
        await new EventRepository(new UnitOfWork(blocker)).LockEventAsync(setup.Event.EventId, ct);
        var gate = new EventLockAttempts(1);
        var clock = new MutableClock(Start.AddTicks(-1));
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var approval = Service(context, setup.Officer.Id, clock).ApproveAsync(row.RegistrationId, ct);
        var committed = false;
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            Assert.False(approval.IsCompleted);
            clock.Now = Start;
            await transaction.CommitAsync(ct);
            committed = true;
            var error = await Assert.ThrowsAsync<DomainException>(() => approval);
            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
        }
        finally { if (!committed) await transaction.RollbackAsync(CancellationToken.None); }
        context.ChangeTracker.Clear();
        Assert.Equal(RegistrationStatusCode.Pending, (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Theory]
    [InlineData("event")]
    [InlineData("registration")]
    [InlineData("attendance")]
    [InlineData("owner")]
    public async Task WaitingForEventLockRechecksCompetingChanges(string change)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, change == "attendance" ? RegistrationStatusCode.Confirmed : RegistrationStatusCode.Pending, ct);
        Member replacement = null!;
        if (change == "owner")
        {
            var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"replacement-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
            replacement = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        }
        await using var blocker = MemberEventTestSupport.Context(fixture.ConnectionString);
        await using var transaction = await blocker.Database.BeginTransactionAsync(ct);
        await new EventRepository(new UnitOfWork(blocker)).LockEventAsync(setup.Event.EventId, ct);
        var gate = new EventLockAttempts(1);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var service = Service(context, setup.Officer.Id, Start.AddTicks(-1));
        var command = change == "attendance" ? service.CancelAsync(row.RegistrationId, "reason", ct) : service.ApproveAsync(row.RegistrationId, ct);
        var committed = false;
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            Assert.False(command.IsCompleted);
            if (change == "event") (await blocker.Events.SingleAsync(e => e.EventId == setup.Event.EventId, ct)).EventStatusId = EventStatusCode.Cancelled;
            if (change == "registration") (await blocker.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId = RegistrationStatusCode.Waitlisted;
            if (change == "owner") (await blocker.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).MemberId = replacement.MemberId;
            if (change == "attendance") blocker.Set<Attendance>().Add(new Attendance { RegistrationId = row.RegistrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = setup.Officer.Id, RecordedAt = Start });
            await blocker.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            await Assert.ThrowsAsync<DomainException>(() => command);
        }
        finally { if (!committed) await transaction.RollbackAsync(CancellationToken.None); }
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(change == "registration" ? RegistrationStatusCode.Waitlisted : row.RegistrationStatusId, actual.RegistrationStatusId);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Fact]
    public async Task ApprovalOneTickBeforeStartRemainsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var row = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        await Service(context, setup.Officer.Id, Start.AddTicks(-1)).ApproveAsync(row.RegistrationId, ct);
        context.ChangeTracker.Clear();
        Assert.Equal(RegistrationStatusCode.Confirmed, (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId);
    }

    [Fact]
    public async Task BrowserQueuePaginatesAndFiltersAndEnforcesPolicyAndAntiforgery()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"browser-officer-{Guid.NewGuid():N}@test.invalid";
        var memberEmail = $"browser-member-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, ct);
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        var ids = await SeedQueueAsync(setup, ct);
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/RegistrationReview/Index", ct)).StatusCode);
        using var member = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(member, memberEmail, password, ct);
        var deniedGet = await member.GetAsync("/RegistrationReview/Index", ct);
        Assert.Equal(HttpStatusCode.Redirect, deniedGet.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", MemberEventTestSupport.LocationPath(deniedGet), StringComparison.Ordinal);
        var forbidden = await member.PostAsync($"/RegistrationReview/Approve?id={ids[0]}", MemberEventTestSupport.Form(new()), ct);
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", MemberEventTestSupport.LocationPath(forbidden), StringComparison.Ordinal);
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(officer, officerEmail, password, ct);
        var query = $"status=pending&eventId={setup.Event.EventId}";
        var first = await officer.GetStringAsync($"/RegistrationReview/Index?{query}&page=1", ct);
        Assert.Contains("Page 1 of 2", first, StringComparison.Ordinal);
        Assert.Equal(ids.Take(25), QueueIds(first));
        Assert.Contains($"status=pending&amp;eventId={setup.Event.EventId}&amp;page=2", first, StringComparison.Ordinal);
        var second = await officer.GetStringAsync($"/RegistrationReview/Index?{query}&page=2", ct);
        Assert.Equal(ids.Skip(25), QueueIds(second));
        foreach (var action in new[] { "Approve", "Decline", "Cancel" })
        {
            var missingToken = await officer.PostAsync($"/RegistrationReview/{action}?id={ids[0]}", MemberEventTestSupport.Form(new()), ct);
            Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        }
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(officer, $"/RegistrationReview/Index?{query}", ct);
        var response = await officer.PostAsync("/RegistrationReview/Approve", MemberEventTestSupport.Form(new()
        {
            ["id"] = ids[0].ToString(), ["status"] = "pending", ["eventId"] = setup.Event.EventId.ToString(), ["page"] = "1", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"eventId={setup.Event.EventId}", MemberEventTestSupport.LocationPath(response), StringComparison.Ordinal);
        var declined = await officer.PostAsync("/RegistrationReview/Decline", MemberEventTestSupport.Form(new()
        {
            ["id"] = ids[1].ToString(), ["status"] = "pending", ["eventId"] = setup.Event.EventId.ToString(), ["reason"] = "Browser decline", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, declined.StatusCode);
        var cancelled = await officer.PostAsync("/RegistrationReview/Cancel", MemberEventTestSupport.Form(new()
        {
            ["id"] = ids[0].ToString(), ["status"] = "confirmed", ["eventId"] = setup.Event.EventId.ToString(), ["reason"] = "Browser cancellation", ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        await using var verify = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(RegistrationStatusCode.Cancelled, (await verify.Registrations.SingleAsync(r => r.RegistrationId == ids[0], ct)).RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Cancelled, (await verify.Registrations.SingleAsync(r => r.RegistrationId == ids[1], ct)).RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Pending, (await verify.Registrations.SingleAsync(r => r.RegistrationId == ids[2], ct)).RegistrationStatusId);
    }

    private static int[] QueueIds(string html) => Regex.Matches(html, "data-registration-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToArray();

    private async Task<int[]> SeedQueueAsync((User Officer, Event Event) setup, CancellationToken ct)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var users = Enumerable.Range(0, 26).Select(i => new User { UserId = $"queue-{Guid.NewGuid():N}@test.invalid", Name = $"Review queue {i:D2}", Password = setup.Officer.Password, IsActive = true, RoleId = RoleCode.Member }).ToArray();
        context.Users.AddRange(users);
        await context.SaveChangesAsync(ct);
        var members = users.Select(u => new Member { UserId = u.Id, StudentNumber = $"Q-{Guid.NewGuid():N}", Course = "BSIT", YearLevel = 1, Section = "A", MembershipStatusId = MembershipStatusCode.Active, CreatedBy = setup.Officer.Id, CreatedAt = Start.AddDays(-2) }).ToArray();
        context.Members.AddRange(members);
        await context.SaveChangesAsync(ct);
        var registrations = members.Select(m => new Registration { MemberId = m.MemberId, EventId = setup.Event.EventId, RegistrationStatusId = RegistrationStatusCode.Pending, CourseSnapshot = "BSIT", YearLevelSnapshot = 1, SectionSnapshot = "A", SubmittedAt = Start.AddDays(-2), CreatedAt = Start.AddDays(-2), CreatedBy = setup.Officer.Id }).ToArray();
        context.Registrations.AddRange(registrations);
        await context.SaveChangesAsync(ct);
        return registrations.Select(r => r.RegistrationId).ToArray();
    }

    private async Task<(User Officer, Event Event)> SetupAsync(CancellationToken ct, int limit = 20)
    {
        var officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"officer-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Officer, ct);
        var (type, venue) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, type, venue, EventStatusCode.Published, Start, Start.AddDays(-1), limit, null, "Review event", ct);
        return (officer, eventItem);
    }

    private async Task<Registration> AddRegistrationAsync((User Officer, Event Event) setup, RegistrationStatusCode status, CancellationToken ct)
    {
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"review-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        return await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, setup.Event.EventId, member.MemberId, status, setup.Officer.Id, ct);
    }

    private static RegistrationReviewService Service(AsiBasecodeDBContext context, int actorId, DateTime now, int failAuditCall = 0)
        => Service(context, actorId, new Clock(now), failAuditCall);

    private static RegistrationReviewService Service(AsiBasecodeDBContext context, int actorId, TimeProvider clock, int failAuditCall = 0)
    {
        var uow = new UnitOfWork(context);
        var actor = new Actor(actorId);
        var registrations = new RegistrationRepository(uow);
        var events = new EventRepository(uow);
        IActivityLogWriter writer = new ActivityLogWriter(context, actor, clock);
        if (failAuditCall != 0) writer = new FailingWriter(writer, failAuditCall);
        return new RegistrationReviewService(registrations, new UserRepository(uow), new MemberRepository(uow), events, uow, actor,
            new RegistrationCancellationService(registrations, events, uow, actor, writer, clock), writer, clock);
    }

    private sealed class Actor(int id) : ICurrentActor { public bool IsAuthenticated => true; public int UserId => id; public string Role => "officer"; }
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc)); }
    private sealed class MutableClock(DateTime now) : TimeProvider { public DateTime Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc)); }
    private sealed class EventLockAttempts(int required) : DbCommandInterceptor
    {
        private int count;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("dbo.events", StringComparison.OrdinalIgnoreCase) && Interlocked.Increment(ref count) == required) Reached.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
    private sealed class FailingWriter(IActivityLogWriter inner, int failAt) : IActivityLogWriter
    {
        private int calls;
        public void Append(AuditEntry entry) { if (++calls == failAt) throw new InvalidOperationException("audit failure"); inner.Append(entry); }
    }
}
