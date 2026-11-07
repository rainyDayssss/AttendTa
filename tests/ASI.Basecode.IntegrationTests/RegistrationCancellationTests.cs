using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class RegistrationCancellationTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task CancellationPreservesTrackedReviewIsIdempotentAndRollsBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, member, eventItem) = await SeedAsync(ct);
        var row = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var seedRow = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        seedRow.QrToken = new string('a', 64);
        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
        var uow = new UnitOfWork(context);
        var actor = new Actor(user.Id);
        var service = Service(uow, actor, new Clock(eventItem.StartsAt.AddDays(-1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelRegistrationAsync(row.RegistrationId, CancellationCause.Declined, "reason", ct));
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await new UserRepository(uow).LockUserAsync(user.Id, token);
            await new EventRepository(uow).LockEventAsync(eventItem.EventId, token);
            var tracked = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, token);
            tracked.ReviewNotes = "review retained";
            await Assert.ThrowsAsync<DomainException>(() => service.CancelRegistrationAsync(row.RegistrationId, CancellationCause.Declined, " ", token));
            await service.CancelRegistrationAsync(row.RegistrationId, CancellationCause.Declined, "declined", token);
            await service.CancelRegistrationAsync(row.RegistrationId, CancellationCause.Declined, "different", token);
        }, ct);
        context.ChangeTracker.Clear();
        var result = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Cancelled, result.RegistrationStatusId);
        Assert.Equal("review retained", result.ReviewNotes);
        Assert.Equal("declined", result.CancellationReason);
        Assert.Equal(user.Id, result.CancelledBy);
        Assert.Null(result.QrToken);
        var logs = await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct);
        Assert.Single(logs);
        Assert.DoesNotContain("token", logs[0].NewValues!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewAndCascadeUseExactCutoffAndRecheckValidAttendance()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, member, eventItem) = await SeedAsync(ct);
        var row = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Pending, user.Id, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var uow = new UnitOfWork(context);
        var actor = new Actor(user.Id);
        foreach (var (now, count) in new[] { (eventItem.StartsAt.AddTicks(-1), 1), (eventItem.StartsAt, 0), (eventItem.StartsAt.AddTicks(1), 0) })
            Assert.Equal(count, (await Service(uow, actor, new Clock(now)).PreviewUpcomingForUserAsync(user.Id, ct)).Count);

        context.Set<Attendance>().Add(new Attendance { RegistrationId = row.RegistrationId, CheckInMethodId = CheckInMethodCode.QrCode, RecordedBy = user.Id, RecordedAt = eventItem.StartsAt });
        await context.SaveChangesAsync(ct);
        var service = Service(uow, actor, new Clock(eventItem.StartsAt.AddTicks(-1)));
        Assert.Empty(await service.PreviewUpcomingForUserAsync(user.Id, ct));
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await new UserRepository(uow).LockUserAsync(user.Id, token);
            await service.CancelUpcomingForUserAsync(user.Id, CancellationCause.AccountDeactivated, "deactivated", token);
        }, ct);
        context.ChangeTracker.Clear();
        Assert.Equal(RegistrationStatusCode.Pending, (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId);
        var attendance = await context.Set<Attendance>().SingleAsync(a => a.RegistrationId == row.RegistrationId, ct);
        attendance.InvalidatedAt = eventItem.StartsAt;
        attendance.InvalidatedBy = user.Id;
        attendance.InvalidationReason = "invalid";
        await context.SaveChangesAsync(ct);
        Assert.Single(await service.PreviewUpcomingForUserAsync(user.Id, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async token =>
        {
            await new UserRepository(uow).LockUserAsync(user.Id, token);
            await service.CancelUpcomingForUserAsync(user.Id, CancellationCause.AccountDeactivated, "deactivated", token);
            throw new InvalidOperationException("force rollback");
        }, ct));
        Assert.Equal(RegistrationStatusCode.Pending, (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await new UserRepository(uow).LockUserAsync(user.Id, token);
            await service.CancelUpcomingForUserAsync(user.Id, CancellationCause.AccountDeactivated, "deactivated", token);
        }, ct);
        context.ChangeTracker.Clear();
        Assert.Equal(RegistrationStatusCode.Cancelled, (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId);
    }

    [Fact]
    public async Task ReverseDiscoveryLocksEventsInAscendingSqlParameterOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, member, first) = await SeedAsync(ct);
        var second = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, user.Id, first.TypeId, first.VenueId,
            EventStatusCode.Published, first.StartsAt.AddDays(1), first.RegistrationDeadline, 20, null, "Second", ct);
        await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, first.EventId, member.MemberId, RegistrationStatusCode.Pending, user.Id, ct);
        await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, second.EventId, member.MemberId, RegistrationStatusCode.Waitlisted, user.Id, ct);
        var recorder = new EventLockInterceptor();
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, recorder);
        var uow = new UnitOfWork(context);
        var actor = new Actor(user.Id);
        var clock = new Clock(first.StartsAt.AddDays(-2));
        var reverse = new ReverseDiscoveryRepository(new RegistrationRepository(uow));
        var service = new RegistrationCancellationService(reverse, new EventRepository(uow), uow, actor, new ActivityLogWriter(context, actor, clock), clock);
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await new UserRepository(uow).LockUserAsync(user.Id, token);
            await service.CancelUpcomingForUserAsync(user.Id, CancellationCause.AccountDeactivated, "deactivated", token);
        }, ct);
        Assert.Equal(new[] { second.EventId, first.EventId }, reverse.Discovered);
        Assert.Equal(new[] { first.EventId, second.EventId }, recorder.EventIds);
        Assert.Equal(2, await context.Registrations.CountAsync(r => r.MemberId == member.MemberId && r.RegistrationStatusId == RegistrationStatusCode.Cancelled, ct));
    }

    [Theory]
    [InlineData("attendance")]
    [InlineData("cancelled")]
    [InlineData("cutoff")]
    public async Task CascadeRechecksAfterCompetingEventLock(string change)
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, member, eventItem) = await SeedAsync(ct);
        var row = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);
        var gate = new EventLockInterceptor();
        await using var competitor = MemberEventTestSupport.Context(fixture.ConnectionString);
        await using var competingTransaction = await competitor.Database.BeginTransactionAsync(ct);
        var competingUow = new UnitOfWork(competitor);
        await new EventRepository(competingUow).LockEventAsync(eventItem.EventId, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, gate);
        var uow = new UnitOfWork(context);
        var clock = new MutableClock(eventItem.StartsAt.AddTicks(-1));
        var service = Service(uow, new Actor(user.Id), clock);
        var cascade = uow.ExecuteInTransactionAsync(async token =>
        {
            await new UserRepository(uow).LockUserAsync(user.Id, token);
            await service.CancelUpcomingForUserAsync(user.Id, CancellationCause.AccountDeactivated, "deactivated", token);
        }, ct);
        var committed = false;
        try
        {
            await gate.LockAttempted.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.False(cascade.IsCompleted);
            if (change == "attendance")
                competitor.Set<Attendance>().Add(new Attendance { RegistrationId = row.RegistrationId, CheckInMethodId = CheckInMethodCode.QrCode, RecordedBy = user.Id, RecordedAt = eventItem.StartsAt });
            else if (change == "cancelled")
            {
                var existing = await competitor.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
                existing.RegistrationStatusId = RegistrationStatusCode.Cancelled;
                existing.CancelledBy = user.Id;
                existing.CancelledAt = eventItem.StartsAt.AddDays(-1);
                existing.CancellationReason = "earlier";
            }
            else clock.Now = eventItem.StartsAt;
            await competitor.SaveChangesAsync(ct);
            await competingTransaction.CommitAsync(ct);
            committed = true;
            await cascade.WaitAsync(TimeSpan.FromSeconds(10), ct);
        }
        finally
        {
            if (!committed) await competingTransaction.RollbackAsync(CancellationToken.None);
        }
        context.ChangeTracker.Clear();
        var actual = await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct);
        Assert.Equal(change == "cancelled" ? RegistrationStatusCode.Cancelled : RegistrationStatusCode.Confirmed, actual.RegistrationStatusId);
        if (change == "cancelled") Assert.Equal("earlier", actual.CancellationReason);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    [Fact]
    public async Task AuditFailureRollsBackAllRowsAndPreviouslyAppendedAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, member, eventItem) = await SeedAsync(ct);
        var other = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"other-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var otherMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, other.Id, MembershipStatusCode.Active, ct);
        var first = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);
        var second = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, otherMember.MemberId, RegistrationStatusCode.Waitlisted, user.Id, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var uow = new UnitOfWork(context);
        var actor = new Actor(user.Id);
        var clock = new Clock(eventItem.StartsAt.AddDays(-1));
        var writer = new FailSecondAudit(new ActivityLogWriter(context, actor, clock));
        var service = new RegistrationCancellationService(new RegistrationRepository(uow), new EventRepository(uow), uow, actor, writer, clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async token =>
        {
            await new EventRepository(uow).LockEventAsync(eventItem.EventId, token);
            await service.CancelAllForEventAsync(eventItem.EventId, CancellationCause.EventCancelled, "cancelled", token);
        }, ct));
        Assert.Equal(2, writer.Calls);
        var rows = await context.Registrations.AsNoTracking().Where(r => r.EventId == eventItem.EventId).OrderBy(r => r.RegistrationId).ToListAsync(ct);
        Assert.Equal(new[] { RegistrationStatusCode.Confirmed, RegistrationStatusCode.Waitlisted }, rows.Select(r => r.RegistrationStatusId));
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && (l.EntityId == first.RegistrationId || l.EntityId == second.RegistrationId)).ToListAsync(ct));
    }

    [Fact]
    public async Task IndividualCancellationDoesNotPromoteWaitlistedMember()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, member, eventItem) = await SeedAsync(ct);
        var other = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"wait-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var otherMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, other.Id, MembershipStatusCode.Active, ct);
        var confirmed = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, user.Id, ct);
        var waitlisted = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, otherMember.MemberId, RegistrationStatusCode.Waitlisted, user.Id, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var uow = new UnitOfWork(context);
        var service = Service(uow, new Actor(user.Id), new Clock(eventItem.StartsAt.AddDays(-1)));
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await new UserRepository(uow).LockUserAsync(user.Id, token);
            await new EventRepository(uow).LockEventAsync(eventItem.EventId, token);
            await service.CancelRegistrationAsync(confirmed.RegistrationId, CancellationCause.MemberRequested, null!, token);
        }, ct);
        context.ChangeTracker.Clear();
        Assert.Equal(RegistrationStatusCode.Waitlisted, (await context.Registrations.SingleAsync(r => r.RegistrationId == waitlisted.RegistrationId, ct)).RegistrationStatusId);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == waitlisted.RegistrationId).ToListAsync(ct));
    }

    [Fact]
    public async Task EventCancellationCancelsOpenRowsOnceAndPreservesPreviousCancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, member, eventItem) = await SeedAsync(ct);
        var waitUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"event-wait-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var oldUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"event-old-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var waitMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, waitUser.Id, MembershipStatusCode.Active, ct);
        var oldMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, oldUser.Id, MembershipStatusCode.Active, ct);
        var pending = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Pending, user.Id, ct);
        var wait = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, waitMember.MemberId, RegistrationStatusCode.Waitlisted, user.Id, ct);
        var old = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, oldMember.MemberId, RegistrationStatusCode.Cancelled, user.Id, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var oldRow = await context.Registrations.SingleAsync(r => r.RegistrationId == old.RegistrationId, ct);
        oldRow.CancelledBy = oldUser.Id;
        oldRow.CancelledAt = eventItem.StartsAt.AddDays(-5);
        oldRow.CancellationReason = "previous cancellation";
        await context.SaveChangesAsync(ct);
        context.ChangeTracker.Clear();
        var uow = new UnitOfWork(context);
        var now = eventItem.StartsAt.AddDays(-1);
        var service = Service(uow, new Actor(user.Id), new Clock(now));
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await new EventRepository(uow).LockEventAsync(eventItem.EventId, token);
            await service.CancelAllForEventAsync(eventItem.EventId, CancellationCause.EventCancelled, "event cancelled", token);
            await service.CancelAllForEventAsync(eventItem.EventId, CancellationCause.EventCancelled, "new reason", token);
        }, ct);
        context.ChangeTracker.Clear();
        var rows = await context.Registrations.AsNoTracking().Where(r => r.EventId == eventItem.EventId).ToDictionaryAsync(r => r.RegistrationId, ct);
        foreach (var id in new[] { pending.RegistrationId, wait.RegistrationId })
        {
            Assert.Equal(RegistrationStatusCode.Cancelled, rows[id].RegistrationStatusId);
            Assert.Equal(user.Id, rows[id].CancelledBy);
            Assert.Equal(now, rows[id].CancelledAt);
            Assert.Equal("event cancelled", rows[id].CancellationReason);
            Assert.Null(rows[id].QrToken);
        }
        Assert.Equal(oldUser.Id, rows[old.RegistrationId].CancelledBy);
        Assert.Equal(eventItem.StartsAt.AddDays(-5), rows[old.RegistrationId].CancelledAt);
        Assert.Equal("previous cancellation", rows[old.RegistrationId].CancellationReason);
        var logs = await context.ActivityLogs.Where(l => l.EntityType == "registrations" && (l.EntityId == pending.RegistrationId || l.EntityId == wait.RegistrationId || l.EntityId == old.RegistrationId)).ToListAsync(ct);
        Assert.Equal(2, logs.Count);
        Assert.DoesNotContain(logs, l => l.EntityId == old.RegistrationId);
        Assert.All(logs, log =>
        {
            Assert.Equal("registration.cancelled", log.Action);
            Assert.DoesNotContain("token", log.NewValues!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", log.OldValues!, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task MutationsRefuseMissingTransactionAndInvalidActorWithoutWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, member, eventItem) = await SeedAsync(ct);
        var row = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Pending, user.Id, ct);
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var uow = new UnitOfWork(context);
        var clock = new Clock(eventItem.StartsAt.AddDays(-1));
        var service = Service(uow, new Actor(user.Id), clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelRegistrationAsync(row.RegistrationId, CancellationCause.MemberRequested, null!, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAllForEventAsync(eventItem.EventId, CancellationCause.EventCancelled, "reason", ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelUpcomingForUserAsync(user.Id, CancellationCause.AccountDeactivated, "reason", ct));
        var invalidActor = new Actor(0);
        var invalidService = Service(uow, invalidActor, clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async token =>
        {
            await invalidService.CancelUpcomingForUserAsync(user.Id, CancellationCause.AccountDeactivated, "reason", token);
        }, ct));
        Assert.Equal(RegistrationStatusCode.Pending, (await context.Registrations.SingleAsync(r => r.RegistrationId == row.RegistrationId, ct)).RegistrationStatusId);
        Assert.Empty(await context.ActivityLogs.Where(l => l.EntityType == "registrations" && l.EntityId == row.RegistrationId).ToListAsync(ct));
    }

    private async Task<(User, Member, Event)> SeedAsync(CancellationToken ct)
    {
        var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"cancel-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
        var (type, venue) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, user.Id, ct);
        var start = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, user.Id, type, venue, EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Test", ct);
        return (user, member, eventItem);
    }

    private static RegistrationCancellationService Service(UnitOfWork uow, Actor actor, TimeProvider clock) =>
        new(new RegistrationRepository(uow), new EventRepository(uow), uow, actor, new ActivityLogWriter((AsiBasecodeDBContext)uow.Database, actor, clock), clock);

    private sealed class Actor(int id) : ICurrentActor { public bool IsAuthenticated => true; public int UserId => id; public string Role => "member"; }
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc)); }
    private sealed class MutableClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
    }

    private sealed class EventLockInterceptor : DbCommandInterceptor
    {
        public List<int> EventIds { get; } = [];
        public TaskCompletionSource LockAttempted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains("dbo.events", StringComparison.OrdinalIgnoreCase))
            {
                EventIds.Add(Convert.ToInt32(command.Parameters[0].Value));
                LockAttempted.TrySetResult();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailSecondAudit(IActivityLogWriter inner) : IActivityLogWriter
    {
        public int Calls { get; private set; }
        public void Append(AuditEntry entry)
        {
            Calls++;
            if (Calls == 2) throw new InvalidOperationException("audit failure");
            inner.Append(entry);
        }
    }

    private sealed class ReverseDiscoveryRepository(IRegistrationRepository inner) : IRegistrationRepository
    {
        public Task<RegistrationReviewIdentity> DiscoverReviewAsync(int id, CancellationToken ct = default) => inner.DiscoverReviewAsync(id, ct);
        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) => inner.DiscoverPromotionCandidateAsync(eventId, ct);
        public Task<bool> HasEligibleWaitlistAsync(int id, CancellationToken ct = default) => inner.HasEligibleWaitlistAsync(id, ct);
        public Task<bool> HasValidAttendanceAsync(int id, CancellationToken ct = default) => inner.HasValidAttendanceAsync(id, ct);
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken ct = default) => inner.HasValidAttendanceForEventAsync(eventId, ct);
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken ct = default) => inner.HasIssuedCertificateForEventAsync(eventId, ct);
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken ct = default) => inner.CountReviewQueueAsync(status, eventId, ct);
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken ct = default) => inner.GetReviewQueueAsync(status, eventId, offset, limit, ct);
        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken ct = default) => inner.GetMyPassRowsAsync(userId, ct);
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken ct = default) => inner.GetOwnedPassAsync(registrationId, userId, ct);
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken ct = default) => inner.FindConfirmedRegistrationIdByTokenAsync(eventId, token, ct);
        public IReadOnlyList<int> Discovered { get; private set; } = [];
        public async Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int id, CancellationToken ct = default)
        {
            Discovered = (await inner.DiscoverOpenEventIdsForUserAsync(id, ct)).OrderByDescending(x => x).ToArray();
            return Discovered;
        }
        public Task<bool> IsConfirmedAsync(int id, CancellationToken ct = default) => inner.IsConfirmedAsync(id, ct);
        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken ct = default) => inner.ExistsAsync(eventId, memberId, ct);
        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken ct = default) => inner.GetMemberRegistrationAsync(eventId, userId, ct);
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken ct = default) => inner.CountMyRegistrationsAsync(userId, ct);
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken ct = default) => inner.GetMyRegistrationsAsync(userId, skip, take, ct);
        public Task<int> CountConfirmedAsync(int id, CancellationToken ct = default) => inner.CountConfirmedAsync(id, ct);
        public Task<int> CountConfirmedLookupAsync(int id, string query, CancellationToken ct = default) => inner.CountConfirmedLookupAsync(id, query, ct);
        public Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(int id, string query, int skip, int take, CancellationToken ct = default) => inner.GetConfirmedLookupPageAsync(id, query, skip, take, ct);
        public Task<string> GetConfirmedStudentNumberAsync(int id, int registrationId, CancellationToken ct = default) => inner.GetConfirmedStudentNumberAsync(id, registrationId, ct);
        public Task AddAsync(Registration row, CancellationToken ct = default) => inner.AddAsync(row, ct);
        public Task<Registration> GetForCancellationAsync(int id, CancellationToken ct = default) => inner.GetForCancellationAsync(id, ct);
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int id, CancellationToken ct = default) => inner.GetOpenForEventAsync(id, ct);
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int id, DateTime now, int? eventId = null, CancellationToken ct = default) => inner.GetUpcomingForUserAsync(id, now, eventId, ct);
    }
}
