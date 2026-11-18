using System.Net;
using System.Data.Common;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
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
        Assert.Contains("Page 1 of 6", first, StringComparison.Ordinal);
        Assert.Equal(ids.Take(5), QueueIds(first));
        Assert.Contains($"status=pending&amp;eventId={setup.Event.EventId}&amp;page=2", first, StringComparison.Ordinal);
        var second = await officer.GetStringAsync($"/RegistrationReview/Index?{query}&page=2", ct);
        Assert.Equal(ids.Skip(5).Take(5), QueueIds(second));
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

    [Fact]
    public async Task BulkApprovalConfirmsTheEarliestSignupAndWaitlistsTheRestWithExactAuditCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct, limit: 1);
        var latest = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var earliest = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var middle = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await SetSubmittedAtAsync(earliest.RegistrationId, Start.AddDays(-3), ct);
        await SetSubmittedAtAsync(middle.RegistrationId, Start.AddDays(-2), ct);
        await SetSubmittedAtAsync(latest.RegistrationId, Start.AddDays(-1), ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        // Posted newest first: the batch must still decide in the queue's own SubmittedAt order.
        var outcomes = await Service(context, setup.Officer.Id, Start.AddHours(-1)).ApproveManyAsync(
            new[] { latest.RegistrationId, middle.RegistrationId, earliest.RegistrationId },
            eventId: null,
            ct);

        Assert.Equal(3, outcomes.Count);
        var confirmed = outcomes.Single(outcome => outcome.Result == RegistrationApprovalResult.Confirmed);
        Assert.Equal(earliest.RegistrationId, confirmed.RegistrationId);
        Assert.Equal(2, outcomes.Count(outcome => outcome.Result == RegistrationApprovalResult.Waitlisted));
        context.ChangeTracker.Clear();
        var rows = await context.Registrations.Where(row => row.EventId == setup.Event.EventId).ToListAsync(ct);
        var seat = Assert.Single(rows, row => row.RegistrationStatusId == RegistrationStatusCode.Confirmed);
        Assert.Equal(earliest.RegistrationId, seat.RegistrationId);
        Assert.Matches("^[0-9a-f]{64}$", seat.QrToken!);
        Assert.Equal(2, rows.Count(row => row.RegistrationStatusId == RegistrationStatusCode.Waitlisted));
        Assert.All(rows.Where(row => row.RegistrationStatusId == RegistrationStatusCode.Waitlisted), row => Assert.Null(row.QrToken));
        Assert.Equal(3, await context.ActivityLogs.CountAsync(log => log.Action == "registration.approved"
            && (log.EntityId == earliest.RegistrationId || log.EntityId == middle.RegistrationId || log.EntityId == latest.RegistrationId), ct));
    }

    [Fact]
    public async Task BulkApprovalRefusesARecordDecidedElsewhereWithoutWritingAnythingForIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var pending = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var decided = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await using (var writer = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            (await writer.Registrations.SingleAsync(row => row.RegistrationId == decided.RegistrationId, ct)).RegistrationStatusId = RegistrationStatusCode.Confirmed;
            await writer.SaveChangesAsync(ct);
        }

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var outcomes = await Service(context, setup.Officer.Id, Start.AddHours(-1)).ApproveManyAsync(
            new[] { pending.RegistrationId, decided.RegistrationId },
            eventId: null,
            ct);

        Assert.Equal(2, outcomes.Count);
        Assert.Equal(RegistrationApprovalResult.Confirmed, outcomes.Single(outcome => outcome.RegistrationId == pending.RegistrationId).Result);
        var refused = outcomes.Single(outcome => outcome.RegistrationId == decided.RegistrationId);
        Assert.Equal(RegistrationApprovalResult.Refused, refused.Result);
        Assert.Equal("Registration status changed. Refresh and try again.", refused.Message);
        context.ChangeTracker.Clear();
        var unchanged = await context.Registrations.SingleAsync(row => row.RegistrationId == decided.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Confirmed, unchanged.RegistrationStatusId);
        Assert.Null(unchanged.ReviewedAt);
        Assert.False(await context.ActivityLogs.AnyAsync(log => log.EntityType == "registrations" && log.EntityId == decided.RegistrationId, ct));
        Assert.Equal(1, await context.ActivityLogs.CountAsync(log => log.EntityType == "registrations" && log.EntityId == pending.RegistrationId, ct));
    }

    [Fact]
    public async Task BulkApprovalPanelPreservesConfirmedAndWaitlistedIdentitiesAsSuccesses()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct, limit: 1);
        var first = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var second = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await SetSubmittedAtAsync(first.RegistrationId, Start.AddDays(-3), ct);
        await SetSubmittedAtAsync(second.RegistrationId, Start.AddDays(-2), ct);
        string firstNumber, secondNumber;
        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            firstNumber = (await context.Members.SingleAsync(member => member.MemberId == first.MemberId, ct)).StudentNumber;
            secondNumber = (await context.Members.SingleAsync(member => member.MemberId == second.MemberId, ct)).StudentNumber;
        }

        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"mixed-bulk-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, officerEmail, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, "/RegistrationReview/Index?status=pending", ct);
        using var response = await client.PostAsync("/RegistrationReview/ApproveSelected", BulkForm(new[] { second.RegistrationId, first.RegistrationId }, token, "pending"), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        var panel = Regex.Match(html, "<section class=\"bulk-result\"[^>]*>(.*?)</section>", RegexOptions.Singleline);
        Assert.True(panel.Success);
        Assert.Contains("2 of 2 succeeded; 0 failed", panel.Value, StringComparison.Ordinal);
        var details = Regex.Match(panel.Value, "<details class=\"bulk-result-details\">(.*?)</details>", RegexOptions.Singleline);
        Assert.True(details.Success); // No open attribute: the list starts collapsed.
        var rows = Regex.Matches(details.Value, "<li class=\"bulk-result-item\">(.*?)</li>", RegexOptions.Singleline);
        Assert.Equal(2, rows.Count);
        Assert.Contains(firstNumber, rows[0].Value, StringComparison.Ordinal);
        Assert.Contains("Confirmed", rows[0].Value, StringComparison.Ordinal);
        Assert.Contains("Confirmed. The pass is ready.", rows[0].Value, StringComparison.Ordinal);
        Assert.Contains(secondNumber, rows[1].Value, StringComparison.Ordinal);
        Assert.Contains("Waitlisted", rows[1].Value, StringComparison.Ordinal);
        Assert.Contains("Waitlisted. No seat was free", rows[1].Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BulkApprovalSubmittedTwiceOnTheReviewQueueCreatesNoDuplicateRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"bulk-officer-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, ct);
        var first = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var second = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var ids = new[] { first.RegistrationId, second.RegistrationId };

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, officerEmail, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, "/RegistrationReview/Index?status=pending", ct);

        // A hand-edited status falls back to the queue's own pending key instead of echoing itself.
        using (var unknown = await client.PostAsync("/RegistrationReview/ApproveSelected", BulkForm(Array.Empty<int>(), token, "foo"), ct))
        {
            Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
            var html = await unknown.Content.ReadAsStringAsync(ct);
            Assert.DoesNotContain("status=foo", html, StringComparison.Ordinal);
            Assert.Contains("status=pending", Regex.Match(html, "<form[^>]*action=\"[^\"]*ApproveSelected[^\"]*\"[^>]*>").Value, StringComparison.Ordinal);
        }

        using (var accepted = await client.PostAsync("/RegistrationReview/ApproveSelected", BulkForm(ids, token, "pending"), ct))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var html = await accepted.Content.ReadAsStringAsync(ct);
            Assert.Contains("Approval results", html, StringComparison.Ordinal);
            Assert.Contains("2 of 2 succeeded", html, StringComparison.Ordinal);
            Assert.Contains("0 failed", html, StringComparison.Ordinal);
            Assert.Contains("<details class=\"bulk-result-details\">", html, StringComparison.Ordinal);
            Assert.Contains("Confirmed", html, StringComparison.Ordinal);
            // The decided rows have left the pending table.
            Assert.DoesNotContain($"data-registration-id=\"{first.RegistrationId}\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain($"data-registration-id=\"{second.RegistrationId}\"", html, StringComparison.Ordinal);
        }

        using (var repeated = await client.PostAsync("/RegistrationReview/ApproveSelected", BulkForm(ids, token, "pending"), ct))
        {
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            var html = await repeated.Content.ReadAsStringAsync(ct);
            Assert.Contains("0 of 2 succeeded", html, StringComparison.Ordinal);
            Assert.Contains("2 failed", html, StringComparison.Ordinal);
            Assert.Contains("Registration status changed. Refresh and try again.", html, StringComparison.Ordinal);
        }

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(2, await observer.ActivityLogs.CountAsync(log => log.Action == "registration.approved"
            && (log.EntityId == first.RegistrationId || log.EntityId == second.RegistrationId), ct));
    }

    [Fact]
    public async Task BulkApprovalKeepsThePageSizeAndMarksThePostRerenderAsUnfittable()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"sized-officer-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, ct);
        var ids = await SeedQueueAsync(setup, ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, officerEmail, password, ct);

        // A page the fit sized above the default carries that size into the batch dialog, or the
        // re-render comes back at the default and the reader loses their place.
        const string sized = "/RegistrationReview/Index?status=pending&pageSize=12";
        var queue = await client.GetStringAsync(sized, ct);
        Assert.Contains("data-paging-size=\"12\"", queue, StringComparison.Ordinal);
        Assert.Contains("pageSize=12", Regex.Match(queue, "<form[^>]*action=\"[^\"]*ApproveSelected[^\"]*\"[^>]*>").Value, StringComparison.Ordinal);
        // A GET view is fittable; only a document a POST produced stands the fit down.
        Assert.DoesNotContain("data-paging-fit=\"off\"", queue, StringComparison.Ordinal);

        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, sized, ct);
        // Cookie validation must not capture stale options before POST normalization. Form page
        // takes precedence over query page: old page 4 at size 2 maps to page 2 at size 5.
        using var normalized = await client.PostAsync(
            $"/RegistrationReview/ApproveSelected?eventId={setup.Event.EventId}&status=pending&pageSize=2&page=1",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["page"] = "4" }), ct);
        Assert.Equal(HttpStatusCode.OK, normalized.StatusCode);
        var normalizedHtml = System.Net.WebUtility.HtmlDecode(await normalized.Content.ReadAsStringAsync(ct));
        Assert.Contains("data-paging-size=\"5\"", normalizedHtml, StringComparison.Ordinal);
        Assert.Contains("Showing 6–10 of 26 matching registrations", normalizedHtml, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            "/RegistrationReview/ApproveSelected?status=pending&pageSize=12",
            BulkForm(new[] { ids[0] }, token, "pending"),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        // The re-render sits on the POST-only action, so its pager must stop the fit: a fit there
        // would issue a GET to that action and land the reader on a 405.
        Assert.Contains("data-paging-fit=\"off\"", html, StringComparison.Ordinal);
        Assert.Contains("data-paging-size=\"12\"", html, StringComparison.Ordinal);
        // Taking another batch from the re-render still posts the reader's size.
        Assert.Contains("pageSize=12", Regex.Match(html, "<form[^>]*action=\"[^\"]*ApproveSelected[^\"]*\"[^>]*>").Value, StringComparison.Ordinal);

        // The dossier re-render sits on its own POST-only bulk action, so its pager stands the fit
        // down for the same reason.
        var dossierToken = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/Event/Details?id={setup.Event.EventId}&status=pending", ct);
        using var dossier = await client.PostAsync(
            $"/Event/ApproveSelected?id={setup.Event.EventId}",
            BulkForm(new[] { ids[1] }, dossierToken, "pending", setup.Event.EventId, "id"),
            ct);

        Assert.Equal(HttpStatusCode.OK, dossier.StatusCode);
        var dossierHtml = await dossier.Content.ReadAsStringAsync(ct);
        Assert.Contains("data-paging-fit=\"off\"", dossierHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BulkApprovalOnTheDossierRefusesARegistrationOfAnotherEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var other = await SetupAsync(ct);
        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"dossier-officer-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, ct);
        var mine = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var foreign = await AddRegistrationAsync(other, RegistrationStatusCode.Pending, ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, officerEmail, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/Event/Details?id={setup.Event.EventId}", ct);

        using var response = await client.PostAsync(
            "/Event/ApproveSelected",
            BulkForm(new[] { mine.RegistrationId, foreign.RegistrationId }, token, "pending", setup.Event.EventId, "id"),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("Approval results", html, StringComparison.Ordinal);
        Assert.Contains("1 failed", html, StringComparison.Ordinal);
        Assert.Contains("<details class=\"bulk-result-details\">", html, StringComparison.Ordinal);
        Assert.Contains("not this event", html, StringComparison.Ordinal);
        // Every re-rendered dossier link is one bare path plus one query string, and it keeps the
        // dossier's own id so the status tabs and pager still resolve the event.
        AssertDossierLinks(html);
        var links = Regex.Matches(html, "href=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Contains(links, link => link.Contains($"id={setup.Event.EventId}", StringComparison.Ordinal)
            || link.Contains($"/Event/{setup.Event.EventId}", StringComparison.Ordinal));

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(RegistrationStatusCode.Confirmed,
            (await observer.Registrations.AsNoTracking().SingleAsync(row => row.RegistrationId == mine.RegistrationId, ct)).RegistrationStatusId);
        var untouched = await observer.Registrations.AsNoTracking().SingleAsync(row => row.RegistrationId == foreign.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Pending, untouched.RegistrationStatusId);
        Assert.Null(untouched.ReviewedAt);
        Assert.False(await observer.ActivityLogs.AnyAsync(log => log.EntityType == "registrations" && log.EntityId == foreign.RegistrationId, ct));
    }

    [Fact]
    public async Task BulkApprovalEndpointsRequireAntiforgeryAndManagementPolicyOnBothSurfaces()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"guard-officer-{Guid.NewGuid():N}@test.invalid";
        var memberEmail = $"guard-member-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, ct);
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        var row = await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        var ids = new[] { row.RegistrationId };

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);

        using (var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        {
            foreach (var (path, form) in new[]
            {
                ("/RegistrationReview/ApproveSelected", BulkForm(ids, null, "pending")),
                ("/Event/ApproveSelected", BulkForm(ids, null, "pending", setup.Event.EventId, "id"))
            })
            {
                using var response = await anonymous.PostAsync(path, form, ct);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(response), StringComparison.Ordinal);
            }
        }

        using (var member = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        {
            await MemberEventTestSupport.SignInAsync(member, memberEmail, password, ct);
            var token = await LayoutTokenAsync(member, ct);
            foreach (var (path, form) in new[]
            {
                ("/RegistrationReview/ApproveSelected", BulkForm(ids, token, "pending")),
                ("/Event/ApproveSelected", BulkForm(ids, token, "pending", setup.Event.EventId, "id"))
            })
            {
                using var response = await member.PostAsync(path, form, ct);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.StartsWith("/Account/AccessDenied", MemberEventTestSupport.LocationPath(response), StringComparison.Ordinal);
            }
        }

        using (var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        {
            await MemberEventTestSupport.SignInAsync(officer, officerEmail, password, ct);
            foreach (var (path, form) in new[]
            {
                ("/RegistrationReview/ApproveSelected", BulkForm(ids, null, "pending")),
                ("/Event/ApproveSelected", BulkForm(ids, null, "pending", setup.Event.EventId, "id"))
            })
            {
                using var response = await officer.PostAsync(path, form, ct);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var unchanged = await observer.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Pending, unchanged.RegistrationStatusId);
        Assert.False(await observer.ActivityLogs.AnyAsync(log => log.EntityType == "registrations" && log.EntityId == row.RegistrationId, ct));
    }

    [Fact]
    public async Task BulkSelectionAppearsOnlyWherePendingRowsCanBeDecided()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SetupAsync(ct);
        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"surface-officer-{Guid.NewGuid():N}@test.invalid";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, ct);
        await AddRegistrationAsync(setup, RegistrationStatusCode.Pending, ct);
        await AddRegistrationAsync(setup, RegistrationStatusCode.Confirmed, ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, officerEmail, password, ct);

        var pendingQueue = await client.GetStringAsync("/RegistrationReview/Index?status=pending", ct);
        Assert.Contains("data-bulk-root", pendingQueue, StringComparison.Ordinal);
        Assert.Contains("Approve selected", pendingQueue, StringComparison.Ordinal);
        Assert.Contains("bulk-approve-dialog", pendingQueue, StringComparison.Ordinal);
        // Nothing arrives already ticked, and the bulk action starts disabled.
        Assert.Contains("0 selected", pendingQueue, StringComparison.Ordinal);
        Assert.Matches(new Regex("<button[^>]*data-bulk-open=\"bulk-approve-dialog\"[^>]*disabled"), pendingQueue);
        var rowInput = Regex.Match(pendingQueue, "<input[^>]*data-bulk-row[^>]*>").Value;
        Assert.False(string.IsNullOrWhiteSpace(rowInput));
        Assert.DoesNotContain("checked", rowInput, StringComparison.OrdinalIgnoreCase);
        // The confirmation dialog speaks this surface's noun, not the membership queue's.
        Assert.Contains("aria-label=\"Selected registrations\"", pendingQueue, StringComparison.Ordinal);
        Assert.Contains("Each registration is decided on its own", pendingQueue, StringComparison.Ordinal);

        // A tab with nothing selectable renders the table alone: no bar, no dialog, no column.
        var confirmedQueue = await client.GetStringAsync("/RegistrationReview/Index?status=confirmed", ct);
        Assert.DoesNotContain("data-bulk-open", confirmedQueue, StringComparison.Ordinal);
        Assert.DoesNotContain("bulk-approve-dialog", confirmedQueue, StringComparison.Ordinal);
        Assert.DoesNotContain("data-bulk-row", confirmedQueue, StringComparison.Ordinal);
        Assert.DoesNotContain("select-column", confirmedQueue, StringComparison.Ordinal);

        var pendingDossier = await client.GetStringAsync($"/Event/Details?id={setup.Event.EventId}&status=pending", ct);
        Assert.Contains("data-bulk-root", pendingDossier, StringComparison.Ordinal);
        Assert.Contains("bulk-approve-dialog", pendingDossier, StringComparison.Ordinal);
        // The dossier loads the shared script through its own section because record-dialog owns the
        // feature slot.
        Assert.Contains("bulk-selection.js", pendingDossier, StringComparison.Ordinal);
        // Status tabs and pager append their own query string, so every link stays well formed.
        AssertDossierLinks(pendingDossier);

        // A batch taken from a searched roster keeps the search: the dialog posts the current view.
        var searchedDossier = await client.GetStringAsync($"/Event/Details?id={setup.Event.EventId}&status=pending&search=Member", ct);
        Assert.Contains("search=Member", Regex.Match(searchedDossier, "<form[^>]*action=\"[^\"]*ApproveSelected[^\"]*\"[^>]*>").Value, StringComparison.Ordinal);

        var confirmedDossier = await client.GetStringAsync($"/Event/Details?id={setup.Event.EventId}&status=confirmed", ct);
        Assert.DoesNotContain("data-bulk-open", confirmedDossier, StringComparison.Ordinal);
        Assert.DoesNotContain("bulk-approve-dialog", confirmedDossier, StringComparison.Ordinal);
        Assert.DoesNotContain("data-bulk-row", confirmedDossier, StringComparison.Ordinal);
        AssertDossierLinks(confirmedDossier);
    }

    /// <summary>
    /// Asserts every dossier link is one bare path plus at most one query string. A path that
    /// already carried "?id=…" turns the next append into "?id=3?status=…", which binds id as
    /// "3?status=…" and 404s the page.
    /// </summary>
    private static void AssertDossierLinks(string html)
    {
        var links = Regex.Matches(html, "href=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.True(link.Count(character => character == '?') <= 1, $"Malformed dossier link: {link}"));
    }

    private static FormUrlEncodedContent BulkForm(IEnumerable<int> ids, string? token, string? status = null, int? eventKey = null, string eventKeyName = "eventId")
    {
        var values = ids.Select(id => new KeyValuePair<string, string>("ids", id.ToString())).ToList();
        if (status != null) values.Add(new("status", status));
        if (eventKey.HasValue) values.Add(new(eventKeyName, eventKey.Value.ToString()));
        if (token != null) values.Add(new("__RequestVerificationToken", token));
        return new FormUrlEncodedContent(values);
    }

    /// <summary>
    /// Reads the layout's request token, which any authenticated page carries in its meta tag, so a
    /// member client can post a token-bearing request without reaching a management form.
    /// </summary>
    private static async Task<string> LayoutTokenAsync(HttpClient client, CancellationToken ct)
    {
        var html = await client.GetStringAsync("/Home/Index", ct);
        var token = Regex.Match(html, "<meta name=\"RequestVerificationToken\" content=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private async Task SetSubmittedAtAsync(int registrationId, DateTime submittedAt, CancellationToken ct)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        (await context.Registrations.SingleAsync(row => row.RegistrationId == registrationId, ct)).SubmittedAt = submittedAt;
        await context.SaveChangesAsync(ct);
    }

    private async Task<int[]> SeedQueueAsync((User Officer, Event Event) setup, CancellationToken ct)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var users = Enumerable.Range(0, 26).Select(i => new User { Email = $"queue-{Guid.NewGuid():N}@test.invalid", Name = $"Review queue {i:D2}", Password = setup.Officer.Password, IsActive = true, RoleId = RoleCode.Member }).ToArray();
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
