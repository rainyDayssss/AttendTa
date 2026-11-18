using System;
using System.Collections.Generic;
using System.IO;
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

/// <summary>
/// The request-shape guards and the per-record isolation of the certificate bulk approval. The batch
/// must refuse an empty or over-bound selection before it reads anything, decide a repeated id once,
/// and let one refusal leave the other records committed.
/// </summary>
public sealed class CertificateLedgerBulkApprovalTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EmptyBatchIsRejectedBeforeAnyRead()
    {
        var service = UnusedService(new Actor(true, RoleCodes.Officer));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.ApproveManyAsync(Array.Empty<int>(), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(BulkApproval.EmptySelectionMessageFor("certificate review"), exception.Message);
        Assert.Equal("Select at least one certificate review to approve.", exception.Message);
    }

    [Fact]
    public async Task OverBoundBatchIsRejectedBeforeAnyRead()
    {
        var service = UnusedService(new Actor(true, RoleCodes.Officer));
        var ids = Enumerable.Range(1, BulkApproval.MaxBatchSize + 1).ToArray();

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.ApproveManyAsync(ids, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(BulkApproval.OverBoundMessageFor("certificate review"), exception.Message);
        Assert.Equal($"You can approve up to {BulkApproval.MaxBatchSize} certificate reviews at a time.", exception.Message);
    }

    [Theory]
    [InlineData(false, RoleCodes.Officer)]
    [InlineData(true, RoleCodes.Member)]
    public async Task NonManagementBatchIsRejectedBeforeAnyRead(bool authenticated, string role)
    {
        var service = UnusedService(new Actor(authenticated, role));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.ApproveManyAsync(new[] { 7 }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
    }

    [Fact]
    public async Task DuplicateIdIsDecidedOnceAndReportedOnce()
    {
        var harness = new BatchHarness();
        harness.AddPending(7);
        harness.AddPending(8);

        var outcomes = await harness.Service.ApproveManyAsync(
            new[] { 7, 7, 8 },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, outcomes.Count);
        Assert.Equal(new[] { 7, 8 }, outcomes.Select(outcome => outcome.RegistrationId).ToArray());
        Assert.All(outcomes, outcome => Assert.True(outcome.Succeeded));
        Assert.Equal("Participant 7", outcomes[0].MemberName);
        Assert.Equal("SN-7", outcomes[0].StudentNumber);
        Assert.Equal(CertificateStatusCode.Eligible, harness.Certificate(7).CertificateStatusId);
        Assert.Equal(CertificateStatusCode.Eligible, harness.Certificate(8).CertificateStatusId);
        Assert.Equal(2, harness.Audit.Entries.Count);
        // One transaction per distinct id: the decision path owns its own transaction.
        Assert.Equal(2, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task RefusedReviewIsReportedAndLeavesTheOtherApprovalsCommitted()
    {
        var harness = new BatchHarness();
        harness.AddPending(7);
        harness.AddPending(8);
        harness.Certificate(8).CertificateStatusId = CertificateStatusCode.NotEligible;

        var outcomes = await harness.Service.ApproveManyAsync(
            new[] { 7, 8 },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, outcomes.Count);
        var approved = outcomes.Single(outcome => outcome.RegistrationId == 7);
        Assert.True(approved.Succeeded);
        Assert.Equal("Approved.", approved.Message);
        var refused = outcomes.Single(outcome => outcome.RegistrationId == 8);
        Assert.False(refused.Succeeded);
        Assert.Equal("Participant 8", refused.MemberName);
        Assert.Equal("This review has already been decided.", refused.Message);

        Assert.Equal(CertificateStatusCode.Eligible, harness.Certificate(7).CertificateStatusId);
        Assert.Equal(CertificateStatusCode.NotEligible, harness.Certificate(8).CertificateStatusId);
        Assert.Null(harness.Certificate(8).ReviewedBy);
        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Equal(AuditActions.CertificateApproved, audit.Action);
        Assert.Equal(7, harness.RegistrationIdOf(audit.EntityId));
        // The refused record still opens and rolls back only its own transaction.
        Assert.Equal(2, harness.UnitOfWork.TransactionCount);
    }

    private static CertificateLedgerService UnusedService(ICurrentActor actor) =>
        new(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            actor,
            null!,
            new FixedClock(),
            new UnusedPdfRenderer(),
            new UnusedPrivateFileStore());

    private sealed record Actor(bool IsAuthenticated, string Role, int UserId = BatchHarness.ActorUserId) : ICurrentActor;

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
    }

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

    private sealed class RecordingAudit : IActivityLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class UnusedPdfRenderer : ICertificatePdfRenderer
    {
        public Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("A bulk approval must never render a PDF.");
    }

    private sealed class UnusedPrivateFileStore : IPrivateFileStore
    {
        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();
        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Delete(string relativePath) => throw new NotSupportedException();
    }

    /// <summary>
    /// One completed event with N pending reviews, each owned by its own confirmed member with valid
    /// attendance. The certificate rows are the same objects the service mutates, so a decision is
    /// observable without a database.
    /// </summary>
    private sealed class BatchHarness
    {
        public const int ActorUserId = 5;
        public const int EventId = 3;

        private readonly Dictionary<int, RegistrationIdentity> identities = [];
        private readonly Dictionary<int, Registration> registrations = [];
        private readonly Dictionary<int, Certificate> certificates = [];
        private readonly Dictionary<int, Attendance> attendances = [];
        private readonly Dictionary<int, User> users = [];
        private readonly Dictionary<int, Member> members = [];

        public BatchHarness()
        {
            Actor = new Actor(true, RoleCodes.Officer);
            users[ActorUserId] = new User { Id = ActorUserId, Email = "officer@attendta.test", Name = "Officer", Password = string.Empty, RoleId = RoleCode.Officer, IsActive = true };
            EventItem = new Event
            {
                EventId = EventId,
                Title = "Certificate batch",
                TypeId = 1,
                VenueId = 1,
                StartsAt = Now.AddDays(-2),
                RegistrationDeadline = Now.AddDays(-3),
                ParticipantLimit = 50,
                EventStatusId = EventStatusCode.Completed,
                CreatedBy = ActorUserId,
                CreatedAt = Now.AddDays(-4)
            };
            UnitOfWork = new CountingUnitOfWork();
            Audit = new RecordingAudit();
            Service = new CertificateLedgerService(
                new BatchEvents(this),
                new BatchCertificates(this),
                new BatchRegistrations(this),
                new BatchAttendance(this),
                new BatchUsers(this),
                UnitOfWork,
                Actor,
                Audit,
                new FixedClock(),
                new UnusedPdfRenderer(),
                new UnusedPrivateFileStore());
        }

        public Actor Actor { get; }
        public Event EventItem { get; }
        public CountingUnitOfWork UnitOfWork { get; }
        public RecordingAudit Audit { get; }
        public CertificateLedgerService Service { get; }

        /// <summary>Adds one pending review with its own member, account, seat and attendance.</summary>
        public void AddPending(int registrationId)
        {
            var userId = 1000 + registrationId;
            var memberId = 100 + registrationId;
            users[userId] = new User { Id = userId, Email = $"member-{registrationId}@attendta.test", Name = $"Participant {registrationId}", Password = string.Empty, RoleId = RoleCode.Member, IsActive = true };
            members[userId] = new Member { MemberId = memberId, UserId = userId, StudentNumber = $"SN-{registrationId}", MembershipStatusId = MembershipStatusCode.Active };
            registrations[registrationId] = new Registration
            {
                RegistrationId = registrationId,
                MemberId = memberId,
                EventId = EventId,
                RegistrationStatusId = RegistrationStatusCode.Confirmed,
                CourseSnapshot = "BSIT",
                YearLevelSnapshot = 1,
                SectionSnapshot = "A",
                SubmittedAt = Now.AddDays(-3),
                QrToken = new string('a', 64),
                CreatedBy = ActorUserId,
                CreatedAt = Now.AddDays(-3)
            };
            attendances[registrationId] = new Attendance
            {
                AttendanceId = registrationId,
                RegistrationId = registrationId,
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = ActorUserId,
                RecordedAt = Now.AddDays(-2)
            };
            certificates[registrationId] = new Certificate
            {
                CertificateId = 500 + registrationId,
                RegistrationId = registrationId,
                CertificateStatusId = CertificateStatusCode.PendingApproval,
                CreatedBy = ActorUserId,
                CreatedAt = Now.AddDays(-2)
            };
            identities[registrationId] = new RegistrationIdentity(userId, memberId, EventId);
        }

        public Certificate Certificate(int registrationId) => certificates[registrationId];
        public RegistrationIdentity? Identity(int registrationId) => identities.TryGetValue(registrationId, out var identity) ? identity : null;
        public Registration? RegistrationOrNull(int registrationId) => registrations.TryGetValue(registrationId, out var row) ? row : null;
        public Certificate? CertificateOrNull(int registrationId) => certificates.TryGetValue(registrationId, out var row) ? row : null;
        public Attendance? AttendanceOrNull(int registrationId) => attendances.TryGetValue(registrationId, out var row) ? row : null;
        public User? User(int userId) => users.TryGetValue(userId, out var user) ? user : null;
        public Event? EventItemOrNull(int eventId) => eventId == EventId ? EventItem : null;

        public int RegistrationIdOf(int certificateId) =>
            certificates.Values.Single(row => row.CertificateId == certificateId).RegistrationId;

        public IReadOnlyList<CertificateReviewSummary> Summaries(IReadOnlyCollection<int> registrationIds) =>
            registrations.Values
                .Where(row => registrationIds.Contains(row.RegistrationId))
                .Select(row => new CertificateReviewSummary
                {
                    RegistrationId = row.RegistrationId,
                    MemberName = users[identities[row.RegistrationId].UserId].Name,
                    StudentNumber = members[identities[row.RegistrationId].UserId].StudentNumber
                })
                .ToList();
    }

    private sealed class BatchEvents(BatchHarness harness) : IEventRepository
    {
        public Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(harness.EventItemOrNull(eventId)!);

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

    private sealed class BatchCertificates(BatchHarness harness) : ICertificateRepository
    {
        public Task<Certificate> GetByRegistrationIdAsync(int registrationId, CancellationToken ct = default) =>
            Task.FromResult(harness.CertificateOrNull(registrationId)!);

        public Task<IReadOnlyList<CertificateReviewSummary>> GetDecisionSummariesAsync(IReadOnlyCollection<int> registrationIds, CancellationToken ct = default) =>
            Task.FromResult(harness.Summaries(registrationIds));

        public Task<IReadOnlyList<int>> GetRegistrationsAwaitingReviewAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Certificate> GetByRegistrationIdNoTrackingAsync(int registrationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CertificateReviewQueueRow>> GetReviewQueueAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IReadOnlyList<Certificate> certificates, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class BatchRegistrations(BatchHarness harness) : IRegistrationRepository
    {
        public Task<RegistrationIdentity> DiscoverIdentityAsync(int registrationId, CancellationToken ct = default) =>
            Task.FromResult(harness.Identity(registrationId)!);

        public Task<Registration> GetForUpdateAsync(int registrationId, CancellationToken ct = default) =>
            Task.FromResult(harness.RegistrationOrNull(registrationId)!);

        public Task<bool> IsConfirmedAsync(int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountConfirmedLookupAsync(int eventId, string query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(int eventId, string query, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetConfirmedStudentNumberAsync(int eventId, int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Registration registration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int userId, DateTime nowUtc, int? eventId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasEligibleWaitlistAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceAsync(int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RegistrationSummary>> GetSummariesAsync(IReadOnlyCollection<int> registrationIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class BatchAttendance(BatchHarness harness) : IAttendanceRepository
    {
        public Task<Attendance> GetByRegistrationIdAsync(int registrationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(harness.AttendanceOrNull(registrationId)!);

        public Task<Attendance> LockAttendanceAsync(int attendanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Attendance attendance, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AttendanceLogRow>> GetRecentForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMyAttendanceAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyAttendanceRow>> GetMyAttendanceAsync(int userId, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class BatchUsers(BatchHarness harness) : IUserRepository
    {
        public Task<User> LockUserAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(harness.User(userId)!);

        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
