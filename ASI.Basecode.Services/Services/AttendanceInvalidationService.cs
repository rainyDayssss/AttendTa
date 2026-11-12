using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The real attendance invalidation command (R25, AD-2, AD-4). It discovers the registration, then
/// runs one transaction that locks the participant's <c>users</c> row, the event and the attendance
/// row in that order, validates the trimmed reason, refuses an already-invalidated row, invokes
/// every registered <see cref="IAttendanceInvalidationHandler"/> before the write, stamps
/// <c>invalidated_at</c>, <c>invalidated_by</c> and <c>invalidation_reason</c> on the retained row
/// and appends exactly one <c>attendance.invalidated</c> audit entry. The registration status never
/// changes, so a later check-in is refused by the recording engine's invalidated rule.
/// </summary>
public sealed class AttendanceInvalidationService(
    IRegistrationRepository registrations,
    IUserRepository users,
    IEventRepository events,
    IAttendanceRepository attendance,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    IActivityLogWriter audit,
    IEnumerable<IAttendanceInvalidationHandler> handlers,
    TimeProvider clock) : IAttendanceInvalidationService
{
    private const int MaxReasonLength = 500;
    private const string ReasonRequiredMessage = "An invalidation reason is required.";
    private const string ReasonTooLongMessage = "Enter an invalidation reason of up to 500 characters.";
    private const string NotFoundMessage = "Attendance not found for this event.";
    private const string AlreadyInvalidatedMessage = "This attendance has already been invalidated.";

    public async Task InvalidateAsync(
        int eventId,
        int registrationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = ManagementActorGuard.RequireUserId(actor);

        // Discovery outside the transaction; every fact is re-read after the locks are taken.
        var identity = await registrations.DiscoverIdentityAsync(registrationId, cancellationToken);
        if (identity is null || identity.EventId != eventId)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            token => InvalidateInsideTransactionAsync(eventId, registrationId, reason, actorUserId, identity, token),
            cancellationToken);
    }

    private async Task InvalidateInsideTransactionAsync(
        int eventId,
        int registrationId,
        string reason,
        int actorUserId,
        RegistrationIdentity identity,
        CancellationToken cancellationToken)
    {
        // AD-4 lock order: the participant's users row, then the event, then the attendance row.
        await users.LockUserAsync(identity.UserId, cancellationToken);
        await events.LockEventAsync(eventId, cancellationToken);

        var current = await attendance.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (current is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        var row = await attendance.LockAttendanceAsync(current.AttendanceId, cancellationToken);
        if (row is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        // The reason is validated inside the transaction, so no state is committed without it.
        var trimmedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new DomainException(OutcomeCode.Invalid, ReasonRequiredMessage, "reason");
        }

        if (trimmedReason.Length > MaxReasonLength)
        {
            throw new DomainException(OutcomeCode.Invalid, ReasonTooLongMessage, "reason");
        }

        // A repeat writes nothing, so the first reason and its audit entry survive.
        if (ParticipationFacts.IsInvalidated(row.InvalidatedAt))
        {
            throw new DomainException(OutcomeCode.Invalid, AlreadyInvalidatedMessage);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var context = new AttendanceInvalidationContext(
            eventId, registrationId, row.AttendanceId, trimmedReason, actorUserId, now);

        // Hooks run after the row is locked and before the write, so a Forbidden throw leaves both
        // the stamp and the audit unwritten (AD-2, AD-3).
        foreach (var handler in handlers ?? Array.Empty<IAttendanceInvalidationHandler>())
        {
            await handler.HandleAsync(context, cancellationToken);
        }

        row.InvalidatedBy = actorUserId;
        row.InvalidatedAt = now;
        row.InvalidationReason = trimmedReason;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        audit.Append(new AuditEntry(
            AuditActions.AttendanceInvalidated,
            "attendance",
            row.AttendanceId,
            Reason: trimmedReason,
            NewValues: new AttendanceInvalidationAuditValues(
                eventId, registrationId, actorUserId, now, trimmedReason)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private sealed record AttendanceInvalidationAuditValues(
        int EventId,
        int RegistrationId,
        int InvalidatedBy,
        DateTime InvalidatedAt,
        string InvalidationReason) : IAuditValues;
}
