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
/// Focused smoke over real SQL for the 4.6 invalidation command: the retained stamp, the single
/// <c>attendance.invalidated</c> audit entry, the hook placement, every refusal and the re-check-in
/// rule that 4.3 already enforces. The true parallel race belongs to 8.5.
/// </summary>
public sealed class AttendanceInvalidationFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    private const string Reason = "Pass could not be verified at the door.";

    [Fact]
    public async Task ValidInvalidationStampsTheRowCallsTheHandlerAndWritesOneAuditEntryWithTheReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var handler = new RecordingHandler();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer, [handler]);

        await service.InvalidateAsync(setup.EventId, setup.RegistrationId, $"  {Reason}  ", ct);

        await using (var observer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await observer.AttendanceRecords.AsNoTracking()
                .SingleAsync(item => item.AttendanceId == setup.AttendanceId, ct);
            Assert.Equal(Now, row.InvalidatedAt);
            Assert.Equal(setup.OfficerId, row.InvalidatedBy);
            Assert.Equal(Reason, row.InvalidationReason);

            var log = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(item => item.EntityType == "attendance" && item.EntityId == setup.AttendanceId, ct);
            Assert.Equal(AuditActions.AttendanceInvalidated, log.Action);
            Assert.Equal(setup.OfficerId, log.ActorUserId);
            Assert.Equal(Reason, log.Reason);
            Assert.DoesNotContain("token", log.NewValues!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("qr", log.NewValues!, StringComparison.OrdinalIgnoreCase);

            var registration = await observer.Registrations.AsNoTracking()
                .SingleAsync(item => item.RegistrationId == setup.RegistrationId, ct);
            Assert.Equal(RegistrationStatusCode.Confirmed, registration.RegistrationStatusId);
        }

        var observed = Assert.Single(handler.Contexts);
        Assert.Equal(setup.EventId, observed.EventId);
        Assert.Equal(setup.RegistrationId, observed.RegistrationId);
        Assert.Equal(setup.AttendanceId, observed.AttendanceId);
        Assert.Equal(Reason, observed.Reason);
        Assert.Equal(setup.OfficerId, observed.InvalidatedBy);
        Assert.Equal(Now, observed.InvalidatedAt);
    }

    [Fact]
    public async Task NoRegisteredHandlerStillCommits()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer, Array.Empty<IAttendanceInvalidationHandler>());

        await service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var row = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(item => item.AttendanceId == setup.AttendanceId, ct);
        Assert.Equal(Now, row.InvalidatedAt);
        Assert.Equal(1, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(item => item.EntityType == "attendance" && item.EntityId == setup.AttendanceId, ct));
    }

    [Fact]
    public async Task HandlerForbiddenLeavesTheRowValidAndWritesNoAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer, [new ForbiddenHandler()]);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var row = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(item => item.AttendanceId == setup.AttendanceId, ct);
        Assert.Null(row.InvalidatedAt);
        Assert.Null(row.InvalidatedBy);
        Assert.Null(row.InvalidationReason);
        Assert.Equal(0, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(item => item.EntityType == "attendance" && item.EntityId == setup.AttendanceId, ct));
    }

    [Fact]
    public async Task ReCheckInAfterInvalidationIsRefusedAndAddsNoRowOrAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var service = Service(context, setup.OfficerId, RoleCodes.Officer);
            await service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct);
        }

        var before = await AuditCountAsync();
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var unitOfWork = new UnitOfWork(context);
            var actor = new Actor(setup.OfficerId, RoleCodes.Officer);
            var clock = new FixedClock(Now);
            var engine = new AttendanceRecordingService(
                new RegistrationRepository(unitOfWork),
                new UserRepository(unitOfWork),
                new MemberRepository(unitOfWork),
                new EventRepository(unitOfWork),
                new AttendanceRepository(unitOfWork),
                unitOfWork,
                actor,
                new ActivityLogWriter(context, actor, clock),
                clock);

            var outcome = await engine.RecordAsync(
                setup.EventId, setup.RegistrationId, CheckInMethodCode.QrCode, ct);

            Assert.Equal(AttendanceRecordingOutcome.Invalid, outcome.Outcome);
            Assert.Null(outcome.MemberName);
            Assert.Null(outcome.RecordedAt);
        }

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(1, await observer.AttendanceRecords.AsNoTracking()
            .CountAsync(item => item.RegistrationId == setup.RegistrationId, ct));
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task BlankReasonIsInvalidAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.InvalidateAsync(setup.EventId, setup.RegistrationId, "   ", ct));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal("reason", exception.Field);
        await AssertUntouchedAsync(setup, 0);
    }

    [Fact]
    public async Task OverLongReasonIsInvalidAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.InvalidateAsync(setup.EventId, setup.RegistrationId, $"  {new string('a', 501)}  ", ct));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal("reason", exception.Field);
        await AssertUntouchedAsync(setup, 0);
    }

    [Fact]
    public async Task RepeatInvalidationIsRefusedAndKeepsTheFirstReasonAndAuditEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var service = Service(context, setup.OfficerId, RoleCodes.Officer);
            await service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct);
        }

        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var service = Service(context, setup.OfficerId, RoleCodes.Officer);
            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.InvalidateAsync(setup.EventId, setup.RegistrationId, "A different reason.", ct));
            Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        }

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var row = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(item => item.AttendanceId == setup.AttendanceId, ct);
        Assert.Equal(Reason, row.InvalidationReason);
        var log = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(item => item.EntityType == "attendance" && item.EntityId == setup.AttendanceId, ct);
        Assert.Equal(Reason, log.Reason);
    }

    [Fact]
    public async Task UnknownRegistrationAndWrongEventAreNotFoundAndWriteNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await SeedAsync();
        var second = await SeedAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, first.OfficerId, RoleCodes.Officer);

        var unknown = await Assert.ThrowsAsync<DomainException>(
            () => service.InvalidateAsync(first.EventId, 999_999, Reason, ct));
        var wrongEvent = await Assert.ThrowsAsync<DomainException>(
            () => service.InvalidateAsync(first.EventId, second.RegistrationId, Reason, ct));

        Assert.Equal(OutcomeCode.NotFound, unknown.Outcome);
        Assert.Equal(OutcomeCode.NotFound, wrongEvent.Outcome);
        await AssertUntouchedAsync(first, 0);
        await AssertUntouchedAsync(second, 0);
    }

    [Fact]
    public async Task NonManagementIsForbiddenBeforeAnyWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Member);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
        await AssertUntouchedAsync(setup, 0);
    }

    [Fact]
    public async Task PendingReviewBecomesNotEligibleWithTheCorrectionReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var certificateId = await SeedCertificateAsync(setup, CertificateStatusCode.PendingApproval);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer, handlerFactory: CertificateHandlerFactory);

        await service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.NotEligible, certificate.CertificateStatusId);
        Assert.Equal(Reason, certificate.ReviewNotes);
        Assert.Equal(setup.OfficerId, certificate.UpdatedBy);
        Assert.Equal(Now, certificate.UpdatedAt);

        var attendance = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(row => row.AttendanceId == setup.AttendanceId, ct);
        Assert.Equal(Now, attendance.InvalidatedAt);
        Assert.Equal(setup.OfficerId, attendance.InvalidatedBy);
        Assert.Equal(Reason, attendance.InvalidationReason);
        Assert.Equal(1, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "attendance" && row.EntityId == setup.AttendanceId
                && row.Action == AuditActions.AttendanceInvalidated, ct));
    }

    [Fact]
    public async Task EligibleReviewBecomesNotEligibleWithTheCorrectionReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var originalReviewedAt = Now.AddHours(-2);
        var certificateId = await SeedCertificateAsync(
            setup, CertificateStatusCode.Eligible, reviewedBy: setup.OfficerId, reviewedAt: originalReviewedAt);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer, handlerFactory: CertificateHandlerFactory);

        await service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.NotEligible, certificate.CertificateStatusId);
        Assert.Equal(Reason, certificate.ReviewNotes);
        Assert.Equal(setup.OfficerId, certificate.ReviewedBy);
        Assert.Equal(originalReviewedAt, certificate.ReviewedAt);
        Assert.Equal(setup.OfficerId, certificate.UpdatedBy);
        Assert.Equal(Now, certificate.UpdatedAt);

        var attendance = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(row => row.AttendanceId == setup.AttendanceId, ct);
        Assert.Equal(Now, attendance.InvalidatedAt);
        Assert.Equal(Reason, attendance.InvalidationReason);
        Assert.Equal(1, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "attendance" && row.EntityId == setup.AttendanceId
                && row.Action == AuditActions.AttendanceInvalidated, ct));
    }

    [Fact]
    public async Task IssuedCertificateRefusesTheInvalidationAndLeavesTheAttendanceValid()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var certificateId = await SeedCertificateAsync(setup, CertificateStatusCode.Issued, "Issued.");
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer, handlerFactory: CertificateHandlerFactory);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
        Assert.Equal("A certificate has already been issued for this attendance.", exception.Message);
        await AssertUntouchedAsync(setup, 0);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.Issued, certificate.CertificateStatusId);
        Assert.Equal("Issued.", certificate.ReviewNotes);
    }

    [Fact]
    public async Task MissingReviewRowCommitsWithoutCreatingOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer, handlerFactory: CertificateHandlerFactory);

        await service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var attendance = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(row => row.AttendanceId == setup.AttendanceId, ct);
        Assert.Equal(Now, attendance.InvalidatedAt);
        Assert.Equal(Reason, attendance.InvalidationReason);
        Assert.Equal(0, await observer.Certificates.AsNoTracking()
            .CountAsync(row => row.RegistrationId == setup.RegistrationId, ct));
        Assert.Equal(1, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "attendance" && row.EntityId == setup.AttendanceId
                && row.Action == AuditActions.AttendanceInvalidated, ct));
    }

    [Fact]
    public async Task AlreadyNotEligibleReviewKeepsItsOriginalReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        const string originalReason = "Participation requirements not met.";
        var certificateId = await SeedCertificateAsync(setup, CertificateStatusCode.NotEligible, originalReason);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer, handlerFactory: CertificateHandlerFactory);

        await service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.NotEligible, certificate.CertificateStatusId);
        Assert.Equal(originalReason, certificate.ReviewNotes);
        Assert.Null(certificate.UpdatedBy);
        Assert.Null(certificate.UpdatedAt);

        var attendance = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(row => row.AttendanceId == setup.AttendanceId, ct);
        Assert.Equal(Now, attendance.InvalidatedAt);
        Assert.Equal(1, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "attendance" && row.EntityId == setup.AttendanceId
                && row.Action == AuditActions.AttendanceInvalidated, ct));
    }

    [Fact]
    public async Task LaterHandlerFailureRollsBackTheReviewChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var certificateId = await SeedCertificateAsync(setup, CertificateStatusCode.PendingApproval);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer,
            handlerFactory: unitOfWork => new IAttendanceInvalidationHandler[]
            {
                new CertificateReviewInvalidationHandler(new CertificateRepository(unitOfWork), unitOfWork),
                new ThrowingHandler()
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InvalidateAsync(setup.EventId, setup.RegistrationId, Reason, ct));

        await AssertUntouchedAsync(setup, 0);
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.PendingApproval, certificate.CertificateStatusId);
        Assert.Null(certificate.ReviewNotes);
        Assert.Null(certificate.UpdatedBy);
        Assert.Null(certificate.UpdatedAt);
    }

    [Fact]
    public void ApplicationRegistersTheCertificateReviewInvalidationHandler()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IAttendanceInvalidationHandler>();
        Assert.Contains(handlers, handler => handler is CertificateReviewInvalidationHandler);
    }

    private static AttendanceInvalidationService Service(
        AsiBasecodeDBContext context,
        int actorId,
        string role,
        IEnumerable<IAttendanceInvalidationHandler>? handlers = null,
        Func<IUnitOfWork, IEnumerable<IAttendanceInvalidationHandler>>? handlerFactory = null)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorId, role);
        var clock = new FixedClock(Now);
        return new AttendanceInvalidationService(
            new RegistrationRepository(unitOfWork),
            new UserRepository(unitOfWork),
            new EventRepository(unitOfWork),
            new AttendanceRepository(unitOfWork),
            unitOfWork,
            actor,
            new ActivityLogWriter(context, actor, clock),
            handlerFactory?.Invoke(unitOfWork) ?? handlers ?? Array.Empty<IAttendanceInvalidationHandler>(),
            clock);
    }

    private static Func<IUnitOfWork, IEnumerable<IAttendanceInvalidationHandler>> CertificateHandlerFactory =>
        unitOfWork => new IAttendanceInvalidationHandler[]
        {
            new CertificateReviewInvalidationHandler(new CertificateRepository(unitOfWork), unitOfWork)
        };

    private async Task<InvalidationSetup> SeedAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(1), Now.AddMinutes(-30), 20, null, $"Invalidation flow {Guid.NewGuid():N}", ct);

        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Events.SingleAsync(item => item.EventId == eventItem.EventId, ct);
            row.IsCheckInOpen = true;
            row.CheckInOpenedBy = officerId;
            row.CheckInOpenedAt = Now;
            await writer.SaveChangesAsync(ct);
        }

        var memberUser = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"invalidation-{Guid.NewGuid():N}@attendta.test",
            MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(
            fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId,
            RegistrationStatusCode.Confirmed, officerId, ct);

        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var attendance = new Attendance
            {
                RegistrationId = registration.RegistrationId,
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = officerId,
                RecordedAt = Now.AddMinutes(-5)
            };
            writer.AttendanceRecords.Add(attendance);
            await writer.SaveChangesAsync(ct);

            return new InvalidationSetup(
                officerId, eventItem.EventId, registration.RegistrationId, attendance.AttendanceId);
        }
    }

    private async Task<int> SeedCertificateAsync(
        InvalidationSetup setup,
        CertificateStatusCode status,
        string? reviewNotes = null,
        int? reviewedBy = null,
        DateTime? reviewedAt = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = new Certificate
        {
            RegistrationId = setup.RegistrationId,
            CertificateStatusId = status,
            ReviewNotes = reviewNotes,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            CreatedBy = setup.OfficerId,
            CreatedAt = Now.AddHours(-1)
        };
        writer.Certificates.Add(certificate);
        await writer.SaveChangesAsync(ct);
        return certificate.CertificateId;
    }

    private async Task AssertUntouchedAsync(InvalidationSetup setup, int expectedAuditCount)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var row = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(item => item.AttendanceId == setup.AttendanceId, ct);
        Assert.Null(row.InvalidatedAt);
        Assert.Null(row.InvalidatedBy);
        Assert.Null(row.InvalidationReason);
        Assert.Equal(expectedAuditCount, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(item => item.EntityType == "attendance" && item.EntityId == setup.AttendanceId, ct));
    }

    private async Task<int> AuditCountAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        return await context.ActivityLogs.AsNoTracking().CountAsync(ct);
    }

    private sealed record InvalidationSetup(int OfficerId, int EventId, int RegistrationId, int AttendanceId);

    private sealed class RecordingHandler : IAttendanceInvalidationHandler
    {
        public List<AttendanceInvalidationContext> Contexts { get; } = [];

        public Task HandleAsync(AttendanceInvalidationContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return Task.CompletedTask;
        }
    }

    private sealed class ForbiddenHandler : IAttendanceInvalidationHandler
    {
        public Task HandleAsync(AttendanceInvalidationContext context, CancellationToken cancellationToken = default) =>
            throw new DomainException(OutcomeCode.Forbidden, "A certificate has already been issued for this attendance.");
    }

    private sealed class ThrowingHandler : IAttendanceInvalidationHandler
    {
        public Task HandleAsync(AttendanceInvalidationContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated later handler failure");
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }

    private sealed class Actor(int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => role;
    }
}
