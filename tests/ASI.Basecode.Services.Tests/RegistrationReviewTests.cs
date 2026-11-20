using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public sealed class RegistrationReviewTests
{
    private static readonly DateTime Start = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void DisplayAvailabilityUsesExactCutoff(long ticks, bool available)
    {
        var reason = RegistrationCancellationAvailability.Reason(EventStatusCode.Published, Start, false, Start.AddTicks(ticks));
        Assert.Equal(available, reason == null);
    }

    [Fact]
    public void DisplayAvailabilityListsEveryBlocker()
    {
        var reason = RegistrationCancellationAvailability.Reason(EventStatusCode.Completed, Start, true, Start);
        Assert.Equal("Cancellation unavailable: Event completed; event has started; valid attendance recorded.", reason);
    }

    [Theory]
    [InlineData(null, RegistrationStatusCode.Pending)]
    [InlineData("pending", RegistrationStatusCode.Pending)]
    [InlineData(" CONFIRMED ", RegistrationStatusCode.Confirmed)]
    [InlineData("waitlisted", RegistrationStatusCode.Waitlisted)]
    [InlineData("cancelled", RegistrationStatusCode.Cancelled)]
    public void QueueStatusIsNormalized(string? status, RegistrationStatusCode expected) =>
        Assert.Equal(expected, RegistrationReviewService.ParseStatus(status));

    [Theory]
    [InlineData(false, "admin")]
    [InlineData(true, "member")]
    public async Task ReviewCommandsRejectNonManagementBeforeAccessingData(bool authenticated, string role)
    {
        var actor = new Actor(authenticated, role);
        var service = new RegistrationReviewService(null!, null!, null!, null!, null!, actor, null!, null!, TimeProvider.System);
        var error = await Assert.ThrowsAsync<DomainException>(() => service.ApproveAsync(1, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
    }

    [Theory]
    [InlineData(false, "admin")]
    [InlineData(true, "member")]
    public async Task PromotionRejectsNonManagementBeforeAccessingData(bool authenticated, string role)
    {
        var actor = new Actor(authenticated, role);
        var service = new RegistrationReviewService(null!, null!, null!, null!, null!, actor, null!, null!, TimeProvider.System);
        var error = await Assert.ThrowsAsync<DomainException>(() => service.PromoteNextAsync(1, 1, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task ApprovalSucceedsOneTickBeforeStartAndRefusesAtOrAfter(long offsetTicks, bool succeeds)
    {
        var harness = new ReviewHarness(RegistrationStatusCode.Pending, Start.AddTicks(offsetTicks));

        if (succeeds)
        {
            await harness.Service.ApproveAsync(ReviewHarness.RegistrationId, TestContext.Current.CancellationToken);

            Assert.Equal(RegistrationStatusCode.Confirmed, harness.Registration.RegistrationStatusId);
            Assert.Equal(ReviewHarness.ActorUserId, harness.Registration.ReviewedBy);
            Assert.Equal(Start.AddTicks(-1), harness.Registration.ReviewedAt);
            Assert.Equal(ReviewHarness.ActorUserId, harness.Registration.ConfirmedBy);
            Assert.Equal(Start.AddTicks(-1), harness.Registration.ConfirmedAt);
            AssertQrToken(harness.Registration.QrToken);
            var entry = Assert.Single(harness.Audit.Entries);
            Assert.Equal(AuditActions.RegistrationApproved, entry.Action);
            Assert.Equal("registrations", entry.EntityType);
            Assert.Equal(ReviewHarness.RegistrationId, entry.EntityId);
            Assert.Empty(harness.Cancellation.Calls);
        }
        else
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                harness.Service.ApproveAsync(ReviewHarness.RegistrationId, TestContext.Current.CancellationToken));

            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
            Assert.Equal(RegistrationStatusCode.Pending, harness.Registration.RegistrationStatusId);
            Assert.Null(harness.Registration.ReviewedBy);
            Assert.Null(harness.Registration.ReviewedAt);
            Assert.Null(harness.Registration.ConfirmedBy);
            Assert.Null(harness.Registration.ConfirmedAt);
            Assert.Null(harness.Registration.QrToken);
            Assert.Empty(harness.Cancellation.Calls);
            Assert.Empty(harness.Audit.Entries);
        }
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task DeclineSucceedsOneTickBeforeStartAndRefusesAtOrAfter(long offsetTicks, bool succeeds)
    {
        var harness = new ReviewHarness(RegistrationStatusCode.Pending, Start.AddTicks(offsetTicks));

        if (succeeds)
        {
            await harness.Service.DeclineAsync(ReviewHarness.RegistrationId, "  Not eligible  ", TestContext.Current.CancellationToken);

            Assert.Equal(RegistrationStatusCode.Cancelled, harness.Registration.RegistrationStatusId);
            var call = Assert.Single(harness.Cancellation.Calls);
            Assert.Equal(ReviewHarness.RegistrationId, call.RegistrationId);
            Assert.Equal(CancellationCause.Declined, call.Cause);
            Assert.Equal("Not eligible", call.Reason);
            var entry = Assert.Single(harness.Audit.Entries);
            Assert.Equal(AuditActions.RegistrationDeclined, entry.Action);
            Assert.Equal("Not eligible", entry.Reason);
        }
        else
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                harness.Service.DeclineAsync(ReviewHarness.RegistrationId, "Not eligible", TestContext.Current.CancellationToken));

            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
            Assert.Equal(RegistrationStatusCode.Pending, harness.Registration.RegistrationStatusId);
            Assert.Null(harness.Registration.ReviewedAt);
            Assert.Null(harness.Registration.ReviewNotes);
            Assert.Empty(harness.Cancellation.Calls);
            Assert.Empty(harness.Audit.Entries);
        }
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task AdministrativeCancellationSucceedsOneTickBeforeStartAndRefusesAtOrAfter(long offsetTicks, bool succeeds)
    {
        var harness = new ReviewHarness(RegistrationStatusCode.Confirmed, Start.AddTicks(offsetTicks));

        if (succeeds)
        {
            await harness.Service.CancelAsync(ReviewHarness.RegistrationId, "  Schedule clash  ", TestContext.Current.CancellationToken);

            Assert.Equal(RegistrationStatusCode.Cancelled, harness.Registration.RegistrationStatusId);
            Assert.Null(harness.Registration.QrToken);
            var call = Assert.Single(harness.Cancellation.Calls);
            Assert.Equal(CancellationCause.AdminCancelled, call.Cause);
            Assert.Equal("Schedule clash", call.Reason);
            var entry = Assert.Single(harness.Audit.Entries);
            Assert.Equal(AuditActions.RegistrationAdminCancelled, entry.Action);
            Assert.Equal("Schedule clash", entry.Reason);
        }
        else
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                harness.Service.CancelAsync(ReviewHarness.RegistrationId, "Schedule clash", TestContext.Current.CancellationToken));

            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
            Assert.Equal(RegistrationStatusCode.Confirmed, harness.Registration.RegistrationStatusId);
            AssertQrToken(harness.Registration.QrToken);
            Assert.Empty(harness.Cancellation.Calls);
            Assert.Empty(harness.Audit.Entries);
        }
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task PromotionSucceedsOneTickBeforeStartAndRefusesAtOrAfter(long offsetTicks, bool succeeds)
    {
        var harness = new ReviewHarness(RegistrationStatusCode.Waitlisted, Start.AddTicks(offsetTicks));

        if (succeeds)
        {
            await harness.Service.PromoteNextAsync(ReviewHarness.EventId, ReviewHarness.RegistrationId, TestContext.Current.CancellationToken);

            Assert.Equal(RegistrationStatusCode.Confirmed, harness.Registration.RegistrationStatusId);
            Assert.Equal(ReviewHarness.ActorUserId, harness.Registration.ConfirmedBy);
            Assert.Equal(Start.AddTicks(-1), harness.Registration.ConfirmedAt);
            AssertQrToken(harness.Registration.QrToken);
            var entry = Assert.Single(harness.Audit.Entries);
            Assert.Equal(AuditActions.RegistrationPromoted, entry.Action);
            Assert.Equal(ReviewHarness.RegistrationId, entry.EntityId);
        }
        else
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                harness.Service.PromoteNextAsync(ReviewHarness.EventId, ReviewHarness.RegistrationId, TestContext.Current.CancellationToken));

            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
            Assert.Equal(RegistrationStatusCode.Waitlisted, harness.Registration.RegistrationStatusId);
            Assert.Null(harness.Registration.ConfirmedBy);
            Assert.Null(harness.Registration.ConfirmedAt);
            Assert.Null(harness.Registration.QrToken);
            Assert.Empty(harness.Audit.Entries);
        }
    }

    [Fact]
    public async Task PromotionRefusesAChangedCandidateWithoutPromotingTheReplacement()
    {
        var harness = new ReviewHarness(RegistrationStatusCode.Waitlisted, Start.AddTicks(-1));
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.PromoteNextAsync(ReviewHarness.EventId, ReviewHarness.RegistrationId + 1, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.Equal(RegistrationStatusCode.Waitlisted, harness.Registration.RegistrationStatusId);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task BulkApprovalConfirmsTheEarliestSignupAndWaitlistsTheRestWhenSeatsAreShort()
    {
        var harness = new BatchHarness(participantLimit: 1);
        // Posted newest first: the batch must still decide in the queue's own SubmittedAt order.
        harness.AddPending(31, Start.AddDays(-1));
        harness.AddPending(12, Start.AddDays(-3));
        harness.AddPending(23, Start.AddDays(-2));

        var outcomes = await harness.Service.ApproveManyAsync(
            new[] { 31, 12, 23 },
            eventId: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, outcomes.Count);
        var earliest = outcomes.Single(outcome => outcome.RegistrationId == 12);
        Assert.Equal(RegistrationApprovalResult.Confirmed, earliest.Result);
        Assert.Equal(harness.NameOf(12), earliest.MemberName);
        Assert.Equal(harness.StudentNumberOf(12), earliest.StudentNumber);
        Assert.Equal(RegistrationStatusCode.Confirmed, harness.Row(12).RegistrationStatusId);
        AssertQrToken(harness.Row(12).QrToken);
        Assert.All(outcomes.Where(outcome => outcome.RegistrationId != 12),
            outcome => Assert.Equal(RegistrationApprovalResult.Waitlisted, outcome.Result));
        Assert.Equal(RegistrationStatusCode.Waitlisted, harness.Row(31).RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Waitlisted, harness.Row(23).RegistrationStatusId);
        Assert.Null(harness.Row(31).QrToken);
        Assert.Equal(3, harness.Audit.Entries.Count);
        Assert.All(harness.Audit.Entries, entry => Assert.Equal(AuditActions.RegistrationApproved, entry.Action));
        // One transaction per id: the approval path owns its own transaction.
        Assert.Equal(3, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task BulkApprovalRefusesARecordDecidedElsewhereWithoutWritingAnything()
    {
        var harness = new BatchHarness();
        harness.AddPending(7, Start.AddDays(-2));
        harness.AddPending(8, Start.AddDays(-1));
        harness.Decide(8, RegistrationStatusCode.Confirmed);

        var outcomes = await harness.Service.ApproveManyAsync(
            new[] { 7, 8 },
            eventId: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, outcomes.Count);
        Assert.Equal(RegistrationApprovalResult.Confirmed, outcomes.Single(outcome => outcome.RegistrationId == 7).Result);
        var refused = outcomes.Single(outcome => outcome.RegistrationId == 8);
        Assert.Equal(RegistrationApprovalResult.Refused, refused.Result);
        Assert.Equal(harness.NameOf(8), refused.MemberName);
        Assert.Equal("Registration status changed. Refresh and try again.", refused.Message);
        Assert.Equal(RegistrationStatusCode.Confirmed, harness.Row(8).RegistrationStatusId);
        Assert.Null(harness.Row(8).ReviewedAt);
        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Equal(7, audit.EntityId);
        // A refused record still opens and rolls back only its own transaction.
        Assert.Equal(2, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task BulkApprovalDecidesADuplicateIdOnceAndReportsItOnce()
    {
        var harness = new BatchHarness();
        harness.AddPending(7, Start.AddDays(-2));
        harness.AddPending(8, Start.AddDays(-1));

        var outcomes = await harness.Service.ApproveManyAsync(
            new[] { 7, 7, 8 },
            eventId: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, outcomes.Count);
        Assert.Equal(new[] { 7, 8 }, outcomes.Select(outcome => outcome.RegistrationId).ToArray());
        Assert.All(outcomes, outcome => Assert.Equal(RegistrationApprovalResult.Confirmed, outcome.Result));
        Assert.Equal(2, harness.Audit.Entries.Count);
        Assert.Equal(2, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task BulkApprovalRefusesARegistrationOfAnotherEventBeforeAnyDecision()
    {
        var harness = new BatchHarness();
        harness.AddPending(7, Start.AddDays(-2));
        harness.AddPending(8, Start.AddDays(-1), eventId: 99);

        var outcomes = await harness.Service.ApproveManyAsync(
            new[] { 7, 8 },
            eventId: BatchHarness.EventId,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, outcomes.Count);
        Assert.Equal(RegistrationApprovalResult.Confirmed, outcomes.Single(outcome => outcome.RegistrationId == 7).Result);
        var refused = outcomes.Single(outcome => outcome.RegistrationId == 8);
        Assert.Equal(RegistrationApprovalResult.Refused, refused.Result);
        Assert.Contains("not this event", refused.Message, StringComparison.Ordinal);
        Assert.Equal(RegistrationStatusCode.Pending, harness.Row(8).RegistrationStatusId);
        Assert.Null(harness.Row(8).ReviewedAt);
        Assert.Single(harness.Audit.Entries);
        // The mismatched id is refused before its transaction opens.
        Assert.Equal(1, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task RepeatingTheSameRegistrationBatchRefusesEveryRowAndWritesNothingNew()
    {
        var harness = new BatchHarness();
        harness.AddPending(7, Start.AddDays(-2));
        harness.AddPending(8, Start.AddDays(-1));

        var first = await harness.Service.ApproveManyAsync(new[] { 7, 8 }, eventId: null, TestContext.Current.CancellationToken);
        var second = await harness.Service.ApproveManyAsync(new[] { 7, 8 }, eventId: null, TestContext.Current.CancellationToken);

        Assert.All(first, outcome => Assert.Equal(RegistrationApprovalResult.Confirmed, outcome.Result));
        Assert.All(second, outcome => Assert.Equal(RegistrationApprovalResult.Refused, outcome.Result));
        Assert.All(second, outcome => Assert.Equal("Registration status changed. Refresh and try again.", outcome.Message));
        Assert.Equal(2, harness.Audit.Entries.Count);
        Assert.Equal(RegistrationStatusCode.Confirmed, harness.Row(7).RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Confirmed, harness.Row(8).RegistrationStatusId);
    }

    [Fact]
    public async Task EmptyRegistrationBatchIsRejectedBeforeAnyDecision()
    {
        var harness = new BatchHarness();
        harness.AddPending(7, Start.AddDays(-2));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.ApproveManyAsync(Array.Empty<int>(), null, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(BulkApproval.RegistrationEmptySelectionMessage, exception.Message);
        Assert.Empty(harness.Audit.Entries);
        Assert.Equal(0, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task OverBoundRegistrationBatchIsRejectedBeforeAnyDecision()
    {
        var harness = new BatchHarness();
        var ids = Enumerable.Range(1, BulkApproval.MaxBatchSize + 1).ToArray();

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.ApproveManyAsync(ids, null, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal($"You can approve up to {BulkApproval.MaxBatchSize} registrations at a time.", exception.Message);
        Assert.Empty(harness.Audit.Entries);
        Assert.Equal(0, harness.UnitOfWork.TransactionCount);
    }

    [Theory]
    [InlineData(false, "admin")]
    [InlineData(true, "member")]
    public async Task BulkApprovalRejectsNonManagementBeforeAccessingData(bool authenticated, string role)
    {
        var harness = new BatchHarness();
        harness.AddPending(7, Start.AddDays(-2));
        harness.Actor.IsAuthenticatedValue = authenticated;
        harness.Actor.RoleValue = role;

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.ApproveManyAsync(new[] { 7 }, null, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
        Assert.Equal(RegistrationStatusCode.Pending, harness.Row(7).RegistrationStatusId);
        Assert.Empty(harness.Audit.Entries);
        Assert.Equal(0, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task BulkApprovalAtOrAfterTheStartCutoffRefusesEveryRowAndChangesNothing()
    {
        var harness = new BatchHarness();
        harness.AddPending(7, Start.AddDays(-2));
        harness.AddPending(8, Start.AddDays(-1));
        // Exactly at start: the cutoff is inclusive, so no record may be reviewed.
        harness.Clock.Now = Start;

        var outcomes = await harness.Service.ApproveManyAsync(new[] { 7, 8 }, eventId: null, TestContext.Current.CancellationToken);

        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, outcome => Assert.Equal(RegistrationApprovalResult.Refused, outcome.Result));
        Assert.All(outcomes, outcome => Assert.Equal("This event has already started.", outcome.Message));
        Assert.All(outcomes, outcome => Assert.Equal(RegistrationStatusCode.Pending, harness.Row(outcome.RegistrationId).RegistrationStatusId));
        Assert.All(outcomes, outcome => Assert.Null(harness.Row(outcome.RegistrationId).QrToken));
        Assert.Empty(harness.Audit.Entries);
        // Each refusal opens and rolls back only its own transaction.
        Assert.Equal(2, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task BulkApprovalOnAnUnpublishedEventRefusesEveryRowAndChangesNothing()
    {
        var harness = new BatchHarness();
        harness.AddPending(7, Start.AddDays(-2));
        harness.AddPending(8, Start.AddDays(-1));
        harness.EventItem(BatchHarness.EventId)!.EventStatusId = EventStatusCode.Draft;

        var outcomes = await harness.Service.ApproveManyAsync(new[] { 7, 8 }, eventId: null, TestContext.Current.CancellationToken);

        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, outcome => Assert.Equal(RegistrationApprovalResult.Refused, outcome.Result));
        Assert.All(outcomes, outcome => Assert.Equal("Event status changed. Refresh and try again.", outcome.Message));
        Assert.All(outcomes, outcome => Assert.Equal(RegistrationStatusCode.Pending, harness.Row(outcome.RegistrationId).RegistrationStatusId));
        Assert.Empty(harness.Audit.Entries);
        Assert.Equal(2, harness.UnitOfWork.TransactionCount);
    }

    private static void AssertQrToken(string? token)
    {
        Assert.NotNull(token);
        Assert.Equal(64, token!.Length);
        Assert.All(token, character => Assert.True(char.IsAsciiHexDigitLower(character)));
    }

    private sealed record Actor(bool IsAuthenticated, string Role, int UserId = 1) : ICurrentActor;

    /// <summary>
    /// One published event, one eligible member and one registration, wired to a fake clock. The
    /// service reads the clock once inside the transaction, so the constructor's <c>now</c> is the
    /// exact instant every guard compares against <c>starts_at</c>.
    /// </summary>
    private sealed class ReviewHarness
    {
        public const int ActorUserId = 5;
        public const int MemberId = 7;
        public const int EventId = 3;
        public const int RegistrationId = 11;

        public ReviewHarness(RegistrationStatusCode status, DateTime now)
        {
            Clock = new FixedClock { Now = now };
            Actor = new Actor(true, RoleCodes.Admin, ActorUserId);
            User = new User { Id = ActorUserId, Email = "officer@attendta.test", Name = "Officer", Password = string.Empty, RoleId = RoleCode.Officer, IsActive = true };
            Member = new Member { MemberId = MemberId, UserId = ActorUserId, StudentNumber = "SN-7", MembershipStatusId = MembershipStatusCode.Active };
            EventItem = new Event
            {
                EventId = EventId,
                Title = "Boundary",
                TypeId = 1,
                VenueId = 1,
                StartsAt = Start,
                RegistrationDeadline = Start.AddDays(-1),
                ParticipantLimit = 10,
                EventStatusId = EventStatusCode.Published,
                CreatedBy = ActorUserId,
                CreatedAt = Start.AddDays(-2)
            };
            Registration = new Registration
            {
                RegistrationId = RegistrationId,
                MemberId = MemberId,
                EventId = EventId,
                RegistrationStatusId = status,
                CourseSnapshot = "BSIT",
                YearLevelSnapshot = 1,
                SectionSnapshot = "A",
                SubmittedAt = Start.AddDays(-2),
                QrToken = status == RegistrationStatusCode.Confirmed ? new string('a', 64) : null,
                CreatedBy = ActorUserId,
                CreatedAt = Start.AddDays(-2)
            };
            Registrations = new FakeRegistrations(this);
            Users = new FakeUsers(this);
            Members = new FakeMembers(this);
            Events = new FakeEvents(this);
            Cancellation = new FakeCancellation(this);
            Audit = new RecordingAudit();
            Service = new RegistrationReviewService(
                Registrations,
                Users,
                Members,
                Events,
                new ImmediateUnitOfWork(),
                Actor,
                Cancellation,
                Audit,
                Clock);
        }

        public FixedClock Clock { get; }
        public Actor Actor { get; }
        public User User { get; }
        public Member Member { get; }
        public Event EventItem { get; }
        public Registration Registration { get; }
        public FakeRegistrations Registrations { get; }
        public FakeUsers Users { get; }
        public FakeMembers Members { get; }
        public FakeEvents Events { get; }
        public FakeCancellation Cancellation { get; }
        public RecordingAudit Audit { get; }
        public RegistrationReviewService Service { get; }
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTime Now { get; set; }
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
    }

    private sealed class ImmediateUnitOfWork : IUnitOfWork
    {
        public DbContext Database => throw new NotSupportedException();
        public void SaveChanges() { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) => work(cancellationToken);
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default) => work(cancellationToken);
    }

    private sealed class RecordingAudit : IActivityLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeRegistrations(ReviewHarness harness) : IRegistrationRepository
    {
        public Task<RegistrationIdentity> DiscoverIdentityAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(id == ReviewHarness.RegistrationId
                ? new RegistrationIdentity(ReviewHarness.ActorUserId, ReviewHarness.MemberId, ReviewHarness.EventId)
                : null!);

        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) =>
            Task.FromResult(eventId == ReviewHarness.EventId
                ? new RegistrationPromotionCandidate(ReviewHarness.RegistrationId, ReviewHarness.ActorUserId, ReviewHarness.MemberId, ReviewHarness.EventId, harness.Registration.ReviewedAt)
                : null!);

        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<Registration> GetForUpdateAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(id == ReviewHarness.RegistrationId ? harness.Registration : null!);

        public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> CountConfirmedLookupAsync(int eventId, string query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(int eventId, string query, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetConfirmedStudentNumberAsync(int eventId, int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasEligibleWaitlistAsync(int eventId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasValidAttendanceAsync(int registrationId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<bool> IsConfirmedAsync(int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Registration registration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int userId, DateTime nowUtc, int? eventId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<RegistrationSummary>> GetSummariesAsync(IReadOnlyCollection<int> registrationIds, CancellationToken ct = default)
        {
            IReadOnlyList<RegistrationSummary> summaries = registrationIds.Contains(ReviewHarness.RegistrationId)
                ? new[]
                {
                    new RegistrationSummary(ReviewHarness.RegistrationId, ReviewHarness.EventId, harness.EventItem.Title,
                        harness.User.Name, harness.Member.StudentNumber, harness.Registration.SubmittedAt,
                        harness.Registration.RegistrationStatusId)
                }
                : Array.Empty<RegistrationSummary>();
            return Task.FromResult(summaries);
        }
    }

    private sealed class FakeUsers(ReviewHarness harness) : IUserRepository
    {
        public Task<User> LockUserAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == ReviewHarness.ActorUserId ? harness.User : null!);

        public Task<User> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeMembers(ReviewHarness harness) : IMemberRepository
    {
        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == ReviewHarness.ActorUserId ? harness.Member : null!);

        public IQueryable<MemberRecord> GetMemberRecords() => throw new NotSupportedException();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void EnsureMemberCanRefresh(int memberId) => throw new NotSupportedException();
        public Task<Member> GetMemberForEditAsync(int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeEvents(ReviewHarness harness) : IEventRepository
    {
        public Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(eventId == ReviewHarness.EventId ? harness.EventItem : null!);

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Event> GetEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddEventAsync(Event eventItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateEventAsync(Event eventItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublishedEventListItem>> GetPublishedPageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublishedEventDetail> GetPublishedEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMemberVisibleAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublishedEventListItem>> GetMemberVisiblePageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublishedEventDetail> GetMemberVisibleEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCancellation(ReviewHarness harness) : IRegistrationCancellationService
    {
        public List<(int RegistrationId, CancellationCause Cause, string Reason)> Calls { get; } = [];

        public Task CancelRegistrationAsync(int registrationId, CancellationCause cause, string reason, CancellationToken cancellationToken = default)
        {
            Calls.Add((registrationId, cause, reason));
            var row = harness.Registration;
            row.RegistrationStatusId = RegistrationStatusCode.Cancelled;
            row.QrToken = null;
            row.CancelledBy = ReviewHarness.ActorUserId;
            row.CancelledAt = harness.Clock.Now;
            row.CancellationReason = reason;
            return Task.CompletedTask;
        }

        public Task CancelAllForEventAsync(int eventId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelUpcomingForUserAsync(int userId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationPreview>> PreviewUpcomingForUserAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MutableActor : ICurrentActor
    {
        public bool IsAuthenticatedValue { get; set; } = true;
        public string RoleValue { get; set; } = RoleCodes.Admin;
        public int UserId { get; set; } = BatchHarness.ActorUserId;
        public bool IsAuthenticated => IsAuthenticatedValue;
        public string Role => RoleValue;
    }

    /// <summary>
    /// Counts the transactions the service opens, so a batch test can prove the approval path ran
    /// once per id rather than inside one wrapper transaction.
    /// </summary>
    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int TransactionCount { get; private set; }
        public DbContext Database => throw new NotSupportedException();
        public void SaveChanges() { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            await ExecuteInTransactionAsync<object>(async ct => { await work(ct); return null!; }, cancellationToken);

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return work(cancellationToken);
        }
    }

    /// <summary>
    /// A batch world: one published event with a seat limit and N pending registrations, each owned
    /// by its own eligible member. The service reads the clock once per record, so the constructor's
    /// <c>now</c> is the exact instant every guard compares against <c>starts_at</c>.
    /// </summary>
    private sealed class BatchHarness
    {
        public const int ActorUserId = 5;
        public const int EventId = 3;

        private readonly Dictionary<int, RegistrationIdentity> identities = [];
        private readonly Dictionary<int, Registration> registrations = [];
        private readonly Dictionary<int, User> users = [];
        private readonly Dictionary<int, Member> members = [];
        private readonly Dictionary<int, Event> events = [];

        public BatchHarness(int participantLimit = 10)
        {
            Clock = new FixedClock { Now = Start.AddHours(-1) };
            Actor = new MutableActor();
            users[ActorUserId] = new User { Id = ActorUserId, Email = "officer@attendta.test", Name = "Officer", Password = string.Empty, RoleId = RoleCode.Officer, IsActive = true };
            events[EventId] = NewEvent(EventId, participantLimit);
            UnitOfWork = new CountingUnitOfWork();
            Audit = new RecordingAudit();
            Service = new RegistrationReviewService(
                new BatchRegistrations(this),
                new BatchUsers(this),
                new BatchMembers(this),
                new BatchEvents(this),
                UnitOfWork,
                Actor,
                new UnusedCancellation(),
                Audit,
                Clock);
        }

        public FixedClock Clock { get; }
        public MutableActor Actor { get; }
        public CountingUnitOfWork UnitOfWork { get; }
        public RecordingAudit Audit { get; }
        public RegistrationReviewService Service { get; }

        /// <summary>Adds one pending registration, its own member and its own account.</summary>
        public Registration AddPending(int registrationId, DateTime submittedAt, int eventId = EventId)
        {
            var userId = 1000 + registrationId;
            var memberId = 100 + registrationId;
            users[userId] = new User { Id = userId, Email = $"member-{registrationId}@attendta.test", Name = $"Participant {registrationId}", Password = string.Empty, RoleId = RoleCode.Member, IsActive = true };
            members[userId] = new Member { MemberId = memberId, UserId = userId, StudentNumber = $"SN-{registrationId}", MembershipStatusId = MembershipStatusCode.Active };
            if (!events.ContainsKey(eventId)) events[eventId] = NewEvent(eventId, 10);
            var row = new Registration
            {
                RegistrationId = registrationId,
                MemberId = memberId,
                EventId = eventId,
                RegistrationStatusId = RegistrationStatusCode.Pending,
                CourseSnapshot = "BSIT",
                YearLevelSnapshot = 1,
                SectionSnapshot = "A",
                SubmittedAt = submittedAt,
                CreatedBy = ActorUserId,
                CreatedAt = submittedAt
            };
            registrations[registrationId] = row;
            identities[registrationId] = new RegistrationIdentity(userId, memberId, eventId);
            return row;
        }

        public Registration Row(int registrationId) => registrations[registrationId];
        public string NameOf(int registrationId) => users[identities[registrationId].UserId].Name;
        public string StudentNumberOf(int registrationId) => members[identities[registrationId].UserId].StudentNumber;
        public void Decide(int registrationId, RegistrationStatusCode status) => registrations[registrationId].RegistrationStatusId = status;

        public RegistrationIdentity? Identity(int registrationId) => identities.TryGetValue(registrationId, out var identity) ? identity : null;
        public Registration? RowOrNull(int registrationId) => registrations.TryGetValue(registrationId, out var row) ? row : null;
        public User? User(int userId) => users.TryGetValue(userId, out var user) ? user : null;
        public Member? MemberByUserId(int userId) => members.TryGetValue(userId, out var member) ? member : null;
        public Event? EventItem(int eventId) => events.TryGetValue(eventId, out var item) ? item : null;

        public int ConfirmedCount(int eventId) =>
            registrations.Values.Count(row => row.EventId == eventId && row.RegistrationStatusId == RegistrationStatusCode.Confirmed);

        public IReadOnlyList<RegistrationSummary> Summaries(IReadOnlyCollection<int> registrationIds) =>
            registrations.Values
                .Where(row => registrationIds.Contains(row.RegistrationId))
                .Select(row => new RegistrationSummary(row.RegistrationId, row.EventId, events[row.EventId].Title,
                    users[identities[row.RegistrationId].UserId].Name, members[identities[row.RegistrationId].UserId].StudentNumber,
                    row.SubmittedAt, row.RegistrationStatusId))
                .ToList();

        private Event NewEvent(int eventId, int participantLimit) => new()
        {
            EventId = eventId,
            Title = $"Batch event {eventId}",
            TypeId = 1,
            VenueId = 1,
            StartsAt = Start,
            RegistrationDeadline = Start.AddDays(-1),
            ParticipantLimit = participantLimit,
            EventStatusId = EventStatusCode.Published,
            CreatedBy = ActorUserId,
            CreatedAt = Start.AddDays(-2)
        };
    }

    private sealed class BatchRegistrations(BatchHarness harness) : IRegistrationRepository
    {
        public Task<RegistrationIdentity> DiscoverIdentityAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(harness.Identity(id)!);

        public Task<Registration> GetForUpdateAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(harness.RowOrNull(id)!);

        public Task<int> CountConfirmedAsync(int eventId, CancellationToken ct = default) =>
            Task.FromResult(harness.ConfirmedCount(eventId));

        public Task<bool> HasEligibleWaitlistAsync(int eventId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasValidAttendanceAsync(int registrationId, CancellationToken ct = default) => Task.FromResult(false);

        public Task<IReadOnlyList<RegistrationSummary>> GetSummariesAsync(IReadOnlyCollection<int> registrationIds, CancellationToken ct = default) =>
            Task.FromResult(harness.Summaries(registrationIds));

        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsConfirmedAsync(int registrationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Registration registration, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int userId, DateTime nowUtc, int? eventId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountConfirmedLookupAsync(int eventId, string query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(int eventId, string query, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> GetConfirmedStudentNumberAsync(int eventId, int registrationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class BatchUsers(BatchHarness harness) : IUserRepository
    {
        public Task<User> LockUserAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(harness.User(userId)!);

        public Task<User> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class BatchMembers(BatchHarness harness) : IMemberRepository
    {
        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(harness.MemberByUserId(userId)!);

        public IQueryable<MemberRecord> GetMemberRecords() => throw new NotSupportedException();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void EnsureMemberCanRefresh(int memberId) => throw new NotSupportedException();
        public Task<Member> GetMemberForEditAsync(int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class BatchEvents(BatchHarness harness) : IEventRepository
    {
        public Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(harness.EventItem(eventId)!);

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Event> GetEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddEventAsync(Event eventItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateEventAsync(Event eventItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublishedEventListItem>> GetPublishedPageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublishedEventDetail> GetPublishedEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMemberVisibleAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublishedEventListItem>> GetMemberVisiblePageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublishedEventDetail> GetMemberVisibleEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedCancellation : IRegistrationCancellationService
    {
        public Task CancelRegistrationAsync(int registrationId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelAllForEventAsync(int eventId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelUpcomingForUserAsync(int userId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationPreview>> PreviewUpcomingForUserAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
