using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Completion-to-review smoke for 6.4: the real completion hook inserts one pending certificate
/// review per registration with Valid Attendance, inside the caller's completion transaction.
/// </summary>
public sealed class EventCompletionReviewTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CompletionCreatesOnePendingReviewPerValidAttendeeWithOfficerAndCompletionTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, "Completion review event", ct);

        var valid = await SeedConfirmedAttendeeAsync(fixture.ConnectionString, officerId, eventItem.EventId, "Valid attendee", ct);
        var invalidated = await SeedConfirmedAttendeeAsync(fixture.ConnectionString, officerId, eventItem.EventId, "Invalidated attendee", ct);
        var absent = await SeedConfirmedAttendeeAsync(fixture.ConnectionString, officerId, eventItem.EventId, "Absent attendee", ct);
        await SeedAttendanceAsync(fixture.ConnectionString, valid.RegistrationId, officerId, Now.AddHours(-1), null, ct);
        await SeedAttendanceAsync(fixture.ConnectionString, invalidated.RegistrationId, officerId, Now.AddHours(-1), Now.AddMinutes(-30), ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now),
            completionHandlerFactory: unitOfWork => new IEventCompletionHandler[]
            {
                new CertificateReviewCompletionHandler(new CertificateRepository(unitOfWork), unitOfWork)
            });

        await service.CompleteAsync(eventItem.EventId, ct);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var certificate = Assert.Single(await observer.Certificates.AsNoTracking()
            .Where(row => row.RegistrationId == valid.RegistrationId)
            .ToListAsync(ct));
        Assert.Equal(valid.RegistrationId, certificate.RegistrationId);
        Assert.Equal(CertificateStatusCode.PendingApproval, certificate.CertificateStatusId);
        Assert.Equal(officerId, certificate.CreatedBy);
        Assert.Equal(Now, certificate.CreatedAt);
        Assert.False(await observer.Certificates.AsNoTracking()
            .AnyAsync(row => row.RegistrationId == invalidated.RegistrationId || row.RegistrationId == absent.RegistrationId, ct));

        var log = Assert.Single(await observer.ActivityLogs.AsNoTracking()
            .Where(row => row.EntityType == "events" && row.EntityId == eventItem.EventId)
            .ToListAsync(ct));
        Assert.Equal(AuditActions.EventCompleted, log.Action);
        Assert.Equal(officerId, log.ActorUserId);
    }

    [Fact]
    public async Task CompletionCreatesNoReviewForACancelledOrWaitlistedRegistrationWithAttendance()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, "Seat filter review event", ct);

        var confirmed = await SeedConfirmedAttendeeAsync(fixture.ConnectionString, officerId, eventItem.EventId, "Confirmed attendee", ct);
        var waitlisted = await SeedConfirmedAttendeeAsync(
            fixture.ConnectionString, officerId, eventItem.EventId, "Waitlisted attendee", ct, RegistrationStatusCode.Waitlisted);
        var cancelled = await SeedConfirmedAttendeeAsync(
            fixture.ConnectionString, officerId, eventItem.EventId, "Cancelled attendee", ct, RegistrationStatusCode.Cancelled);
        await SeedAttendanceAsync(fixture.ConnectionString, confirmed.RegistrationId, officerId, Now.AddHours(-1), null, ct);
        await SeedAttendanceAsync(fixture.ConnectionString, waitlisted.RegistrationId, officerId, Now.AddHours(-1), null, ct);
        await SeedAttendanceAsync(fixture.ConnectionString, cancelled.RegistrationId, officerId, Now.AddHours(-1), null, ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now),
            completionHandlerFactory: unitOfWork => new IEventCompletionHandler[]
            {
                new CertificateReviewCompletionHandler(new CertificateRepository(unitOfWork), unitOfWork)
            });

        await service.CompleteAsync(eventItem.EventId, ct);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.True(await observer.Certificates.AsNoTracking()
            .AnyAsync(row => row.RegistrationId == confirmed.RegistrationId, ct));
        Assert.False(await observer.Certificates.AsNoTracking()
            .AnyAsync(row => row.RegistrationId == waitlisted.RegistrationId || row.RegistrationId == cancelled.RegistrationId, ct));
    }

    [Fact]
    public async Task LaterHandlerFailureRollsBackTheInsertedReviewsAndKeepsTheEventPublished()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, "Rollback review event", ct);
        var valid = await SeedConfirmedAttendeeAsync(fixture.ConnectionString, officerId, eventItem.EventId, "Rollback attendee", ct);
        await SeedAttendanceAsync(fixture.ConnectionString, valid.RegistrationId, officerId, Now.AddHours(-1), null, ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var throwing = new RecordingThrowingHandler(context, valid.RegistrationId);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now),
            completionHandlerFactory: unitOfWork => new IEventCompletionHandler[]
            {
                new CertificateReviewCompletionHandler(new CertificateRepository(unitOfWork), unitOfWork),
                throwing
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(eventItem.EventId, ct));

        // The throwing handler ran after the certificate handler and saw the uncommitted row.
        Assert.Equal(1, throwing.CertificatesSeen);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.False(await observer.Certificates.AsNoTracking()
            .AnyAsync(row => row.RegistrationId == valid.RegistrationId, ct));
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Published, persisted.EventStatusId);
        Assert.Null(persisted.CompletedBy);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(
            row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct));
    }

    [Fact]
    public async Task ExistingCertificateRowIsLeftUnchangedAndNoDuplicateIsInserted()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, "Existing review event", ct);
        var valid = await SeedConfirmedAttendeeAsync(fixture.ConnectionString, officerId, eventItem.EventId, "Already reviewed attendee", ct);
        await SeedAttendanceAsync(fixture.ConnectionString, valid.RegistrationId, officerId, Now.AddHours(-1), null, ct);

        var existingCreatedAt = Now.AddDays(-1);
        await using (var seed = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            seed.Certificates.Add(new Certificate
            {
                RegistrationId = valid.RegistrationId,
                CertificateStatusId = CertificateStatusCode.NotEligible,
                ReviewNotes = "Existing decision.",
                CreatedBy = officerId,
                CreatedAt = existingCreatedAt
            });
            await seed.SaveChangesAsync(ct);
        }

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now),
            completionHandlerFactory: unitOfWork => new IEventCompletionHandler[]
            {
                new CertificateReviewCompletionHandler(new CertificateRepository(unitOfWork), unitOfWork)
            });

        await service.CompleteAsync(eventItem.EventId, ct);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var certificate = Assert.Single(await observer.Certificates.AsNoTracking()
            .Where(row => row.RegistrationId == valid.RegistrationId)
            .ToListAsync(ct));
        Assert.Equal(valid.RegistrationId, certificate.RegistrationId);
        Assert.Equal(CertificateStatusCode.NotEligible, certificate.CertificateStatusId);
        Assert.Equal("Existing decision.", certificate.ReviewNotes);
        Assert.Equal(existingCreatedAt, certificate.CreatedAt);
    }

    [Fact]
    public void ApplicationRegistersTheCertificateReviewCompletionHandler()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventCompletionHandler>();
        Assert.Contains(handlers, handler => handler is CertificateReviewCompletionHandler);
    }

    private static async Task<Registration> SeedConfirmedAttendeeAsync(
        string connectionString,
        int officerId,
        int eventId,
        string name,
        CancellationToken cancellationToken,
        RegistrationStatusCode status = RegistrationStatusCode.Confirmed)
    {
        var user = await MemberEventTestSupport.SeedUserAsync(
            connectionString, $"{Guid.NewGuid():N}@attendta.test", MemberEventTestSupport.NewPassword(), RoleCode.Member, cancellationToken);
        await using (var context = MemberEventTestSupport.Context(connectionString))
        {
            (await context.Users.SingleAsync(row => row.Id == user.Id, cancellationToken)).Name = name;
            await context.SaveChangesAsync(cancellationToken);
        }

        var member = await MemberEventTestSupport.SeedMemberAsync(
            connectionString, user.Id, MembershipStatusCode.Active, cancellationToken);
        return await MemberEventTestSupport.SeedRegistrationAsync(
            connectionString, eventId, member.MemberId, status, officerId, cancellationToken);
    }

    private static async Task SeedAttendanceAsync(
        string connectionString,
        int registrationId,
        int recordedBy,
        DateTime recordedAt,
        DateTime? invalidatedAt,
        CancellationToken cancellationToken)
    {
        await using var context = MemberEventTestSupport.Context(connectionString);
        context.AttendanceRecords.Add(new Attendance
        {
            RegistrationId = registrationId,
            CheckInMethodId = CheckInMethodCode.QrCode,
            RecordedBy = recordedBy,
            RecordedAt = recordedAt,
            InvalidatedBy = invalidatedAt.HasValue ? recordedBy : null,
            InvalidatedAt = invalidatedAt,
            InvalidationReason = invalidatedAt.HasValue ? "Attendance corrected." : null
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed class RecordingThrowingHandler(AsiBasecodeDBContext db, int registrationId) : IEventCompletionHandler
    {
        public int CertificatesSeen { get; private set; }

        public async Task HandleAsync(EventCompletionContext context, CancellationToken cancellationToken = default)
        {
            CertificatesSeen = await db.Certificates.AsNoTracking()
                .CountAsync(row => row.RegistrationId == registrationId, cancellationToken);
            throw new InvalidOperationException("simulated later handler failure");
        }
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}
