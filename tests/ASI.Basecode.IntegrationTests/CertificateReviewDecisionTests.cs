using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Focused SQL coverage for 6.6: the real review queue, both decisions, the single audit entry, every
/// refusal in the ticket matrix, and in-memory paging at 25. The DI check also asserts that the real
/// ledger and the real member history resolve from the container.
/// </summary>
public sealed class CertificateReviewDecisionTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ApproveMarksEligibleWithReviewerAndTimeAndWritesOneAuditEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        await service.DecideAsync(registrationId, true, null, ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.Eligible, certificate.CertificateStatusId);
        Assert.Equal(setup.OfficerId, certificate.ReviewedBy);
        Assert.Equal(Now, certificate.ReviewedAt);
        Assert.Null(certificate.ReviewNotes);

        var log = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(row => row.EntityType == "certificates" && row.EntityId == certificateId, ct);
        Assert.Equal(AuditActions.CertificateApproved, log.Action);
        Assert.Equal(setup.OfficerId, log.ActorUserId);
        Assert.Null(log.Reason);
        Assert.DoesNotContain("token", log.NewValues!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeclineMarksNotEligibleWithTheTrimmedReasonAndWritesOneAuditEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        await service.DecideAsync(registrationId, false, "  Participation requirements not met.  ", ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.NotEligible, certificate.CertificateStatusId);
        Assert.Equal("Participation requirements not met.", certificate.ReviewNotes);
        Assert.Equal(setup.OfficerId, certificate.ReviewedBy);
        Assert.Equal(Now, certificate.ReviewedAt);

        var log = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(row => row.EntityType == "certificates" && row.EntityId == certificateId, ct);
        Assert.Equal(AuditActions.CertificateDeclined, log.Action);
        Assert.Equal("Participation requirements not met.", log.Reason);
    }

    [Fact]
    public async Task BlankReasonLeavesTheReviewUnchangedAndNamesTheReasonField()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, false, "   ", ct));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal("reason", exception.Field);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Theory]
    [InlineData(CertificateStatusCode.Eligible)]
    [InlineData(CertificateStatusCode.NotEligible)]
    [InlineData(CertificateStatusCode.Issued)]
    public async Task AlreadyDecidedReviewIsStaleAndUnchanged(CertificateStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(reviewStatus: status);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, true, null, ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        await AssertUnchangedAsync(certificateId, status);
    }

    [Fact]
    public async Task DecidedReviewWithBlankDeclineReasonIsStaleNotInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(reviewStatus: CertificateStatusCode.Eligible);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, false, "   ", ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.Eligible);
    }

    [Fact]
    public async Task InvalidatedAttendanceIsStaleAndUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(invalidateAttendance: true);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, false, "Not eligible.", ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Fact]
    public async Task InvalidatedAttendanceWithBlankDeclineReasonIsStaleNotInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(invalidateAttendance: true);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        // The certificate rule runs before the decline reason, so an undecidable review reports the
        // stale fact instead of asking for a reason the officer could never submit.
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, false, "   ", ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        Assert.Null(exception.Field);
        Assert.Contains("attendance is no longer valid", exception.Message, StringComparison.Ordinal);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Fact]
    public async Task LostSeatIsStaleWithTheChangedMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(cancelRegistration: true);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        // Approving a review whose seat was cancelled refuses as stale, and the message names the lost
        // seat rather than the attendance, which is still valid.
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, true, string.Empty, ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        Assert.Contains("details changed", exception.Message, StringComparison.Ordinal);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Fact]
    public async Task UnknownReviewIsNotFoundAndWritesNoAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(seedReview: false);
        var registrationId = setup.RegistrationIds[0];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);
        var before = await CertificateAuditCountAsync(ct);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, true, null, ct));

        Assert.Equal(OutcomeCode.NotFound, exception.Outcome);
        Assert.Equal(before, await CertificateAuditCountAsync(ct));
    }

    [Fact]
    public async Task MemberCallerIsForbiddenAndWritesNoAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Member);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, true, null, ct));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Fact]
    public async Task QueueShowsTwentyFiveRowsAndClampsAPagePastTheEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(confirmedCount: 28);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var first = await service.GetPageAsync(setup.EventId, CertificateLedgerTab.Eligibility, 1, ct);
        Assert.Equal(28, first.EligibilityCount);
        Assert.Equal(0, first.IssuedCount);
        Assert.Equal(25, first.Rows.Count);
        Assert.Equal(1, first.Page.PageNumber);
        Assert.Equal(28, first.Page.TotalCount);
        Assert.True(first.Page.HasNextPage);
        Assert.Equal(setup.EventId, first.Selector.SelectedEventId);

        var last = await service.GetPageAsync(setup.EventId, CertificateLedgerTab.Eligibility, int.MaxValue, ct);
        Assert.Equal(3, last.Rows.Count);
        Assert.Equal(2, last.Page.PageNumber);
        Assert.False(last.Page.HasNextPage);

        var issued = await service.GetPageAsync(setup.EventId, CertificateLedgerTab.Issued, 1, ct);
        Assert.Empty(issued.Rows);
        Assert.Equal(0, issued.IssuedCount);
        Assert.Equal(28, issued.EligibilityCount);
    }

    [Fact]
    public void ApplicationRegistersTheRealLedgerAndMemberHistory()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using (var scope = factory.Services.CreateScope())
        {
            Assert.IsType<CertificateLedgerService>(
                scope.ServiceProvider.GetRequiredService<ICertificateLedgerService>());
            Assert.IsType<MyCertificatesService>(
                scope.ServiceProvider.GetRequiredService<IMyCertificatesService>());
        }
    }

    private static CertificateLedgerService Service(
        AsiBasecodeDBContext context,
        int actorId,
        string role)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorId, role);
        var clock = new FixedClock(Now);
        return new CertificateLedgerService(
            new EventRepository(unitOfWork),
            new CertificateRepository(unitOfWork),
            new RegistrationRepository(unitOfWork),
            new AttendanceRepository(unitOfWork),
            new UserRepository(unitOfWork),
            unitOfWork,
            actor,
            new ActivityLogWriter(context, actor, clock),
            clock,
            new UnusedPdfRenderer(),
            new UnusedPrivateFileStore());
    }

    private async Task<Setup> SeedAsync(
        int confirmedCount = 1,
        CertificateStatusCode? reviewStatus = CertificateStatusCode.PendingApproval,
        bool invalidateAttendance = false,
        bool seedReview = true,
        bool cancelRegistration = false)
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString,
            officerId,
            typeId,
            venueId,
            EventStatusCode.Completed,
            Now.AddDays(-2),
            Now.AddDays(-3),
            50,
            null,
            $"Certificate review {Guid.NewGuid():N}",
            ct);

        var registrationIds = new List<int>();
        var certificateIds = new Dictionary<int, int>();
        for (var index = 0; index < confirmedCount; index++)
        {
            var user = await MemberEventTestSupport.SeedUserAsync(
                fixture.ConnectionString,
                $"review-{Guid.NewGuid():N}@attendta.test",
                MemberEventTestSupport.NewPassword(),
                RoleCode.Member,
                ct);
            var member = await MemberEventTestSupport.SeedMemberAsync(
                fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
            var registration = await MemberEventTestSupport.SeedRegistrationAsync(
                fixture.ConnectionString,
                eventItem.EventId,
                member.MemberId,
                RegistrationStatusCode.Confirmed,
                officerId,
                ct);
            registrationIds.Add(registration.RegistrationId);

            if (cancelRegistration)
            {
                // A seat lost after the review exists: the reviewer cancelled the registration, so the
                // review can no longer be decided.
                await using var canceller = EventDraftTestSupport.Context(fixture.ConnectionString);
                var row = await canceller.Registrations
                    .SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
                row.RegistrationStatusId = RegistrationStatusCode.Cancelled;
                await canceller.SaveChangesAsync(ct);
            }

            await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
            writer.AttendanceRecords.Add(new Attendance
            {
                RegistrationId = registration.RegistrationId,
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = officerId,
                RecordedAt = Now.AddDays(-2),
                InvalidatedBy = invalidateAttendance ? officerId : null,
                InvalidatedAt = invalidateAttendance ? Now.AddHours(-1) : null,
                InvalidationReason = invalidateAttendance ? "Correction." : null
            });
            if (seedReview)
            {
                var certificate = new Certificate
                {
                    RegistrationId = registration.RegistrationId,
                    CertificateStatusId = reviewStatus!.Value,
                    CreatedBy = officerId,
                    CreatedAt = Now.AddDays(-2)
                };
                writer.Certificates.Add(certificate);
                await writer.SaveChangesAsync(ct);
                certificateIds[registration.RegistrationId] = certificate.CertificateId;
            }
            else
            {
                await writer.SaveChangesAsync(ct);
            }
        }

        return new Setup(officerId, eventItem.EventId, registrationIds, certificateIds);
    }

    private async Task<int> CertificateAuditCountAsync(CancellationToken cancellationToken)
    {
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        return await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.Action == AuditActions.CertificateApproved
                               || row.Action == AuditActions.CertificateDeclined, cancellationToken);
    }

    private async Task AssertUnchangedAsync(int certificateId, CertificateStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(expected, certificate.CertificateStatusId);
        Assert.Null(certificate.ReviewedBy);
        Assert.Null(certificate.ReviewedAt);
        Assert.Equal(0, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "certificates" && row.EntityId == certificateId, ct));
    }

    private sealed record Setup(
        int OfficerId,
        int EventId,
        IReadOnlyList<int> RegistrationIds,
        IReadOnlyDictionary<int, int> CertificateIds);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }

    private sealed class Actor(int userId, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => userId;
        public string Role => role;
    }

    private sealed class UnusedPdfRenderer : ICertificatePdfRenderer
    {
        public Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedPrivateFileStore : IPrivateFileStore
    {
        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();
        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Delete(string relativePath) => throw new NotSupportedException();
    }
}
