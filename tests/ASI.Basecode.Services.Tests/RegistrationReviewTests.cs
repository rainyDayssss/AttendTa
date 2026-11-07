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
        var error = await Assert.ThrowsAsync<DomainException>(() => service.PromoteNextAsync(1, TestContext.Current.CancellationToken));
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
            await harness.Service.PromoteNextAsync(ReviewHarness.EventId, TestContext.Current.CancellationToken);

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
                harness.Service.PromoteNextAsync(ReviewHarness.EventId, TestContext.Current.CancellationToken));

            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
            Assert.Equal(RegistrationStatusCode.Waitlisted, harness.Registration.RegistrationStatusId);
            Assert.Null(harness.Registration.ConfirmedBy);
            Assert.Null(harness.Registration.ConfirmedAt);
            Assert.Null(harness.Registration.QrToken);
            Assert.Empty(harness.Audit.Entries);
        }
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
            User = new User { Id = ActorUserId, UserId = "officer@attendta.test", Name = "Officer", Password = string.Empty, RoleId = RoleCode.Officer, IsActive = true };
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
        public Task<RegistrationReviewIdentity> DiscoverReviewAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(id == ReviewHarness.RegistrationId
                ? new RegistrationReviewIdentity(ReviewHarness.ActorUserId, ReviewHarness.MemberId, ReviewHarness.EventId)
                : null!);

        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) =>
            Task.FromResult(eventId == ReviewHarness.EventId
                ? new RegistrationPromotionCandidate(ReviewHarness.RegistrationId, ReviewHarness.ActorUserId, ReviewHarness.MemberId, ReviewHarness.EventId, harness.Registration.ReviewedAt)
                : null!);

        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<Registration> GetForCancellationAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(id == ReviewHarness.RegistrationId ? harness.Registration : null!);

        public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) => Task.FromResult(0);
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
    }

    private sealed class FakeUsers(ReviewHarness harness) : IUserRepository
    {
        public Task<User> LockUserAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == ReviewHarness.ActorUserId ? harness.User : null!);

        public Task<User> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IQueryable<User> GetUsers() => throw new NotSupportedException();
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
        public MemberRecord GetMemberRecord(int memberId) => throw new NotSupportedException();
        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Member GetMember(int memberId) => throw new NotSupportedException();
        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public bool UserHasMemberProfile(int userId) => throw new NotSupportedException();
        public bool StudentNumberExists(string studentNumber, int? excludeId = null) => throw new NotSupportedException();
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
}
