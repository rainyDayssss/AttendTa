using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The one shared check-in recording engine (AD-2, AD-4). It discovers the registration, then runs
/// one transaction that locks the participant's <c>users</c> row, the event and the attendance row in
/// that order, rechecks every rule against the locked rows, and writes exactly one attendance row plus
/// one <c>attendance.recorded</c> audit entry. A repeat returns the original time and writes nothing;
/// an invalidated attendance, a cancelled or unconfirmed registration, an ineligible participant, a
/// closed window or an unpublished event is refused before any repeat decision. The unique
/// <c>registration_id</c> index is the final duplicate guard.
/// </summary>
public sealed class AttendanceRecordingService(
    IRegistrationRepository registrations,
    IUserRepository users,
    IMemberRepository members,
    IEventRepository events,
    IAttendanceRepository attendance,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    IActivityLogWriter audit,
    TimeProvider clock) : IAttendanceRecordingService
{
    private const string InvalidPassMessage = "This pass is not valid for this event.";
    private const string EventClosedMessage = "Check-in is not available for this event.";
    private const string WindowClosedMessage = "Check-in is closed for this event. Open it before scanning.";
    private const string NotConfirmedMessage = "This registration is not confirmed for this event.";
    private const string IneligibleMessage = "This participant is not eligible to check in.";
    private const string InvalidatedMessage = "This attendance was invalidated and cannot be checked in again.";
    private const string RecordedMessage = "Check-in recorded.";
    private const string RepeatMessage = "Already checked in. This is the original time.";

    public async Task<AttendanceRecordingResult> RecordAsync(
        int eventId,
        int registrationId,
        CheckInMethodCode method,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = ManagementActorGuard.RequireUserId(actor);

        // Discovery outside the transaction; every fact is rechecked after the locks are taken.
        var identity = await registrations.DiscoverIdentityAsync(registrationId, cancellationToken);
        if (identity is null || identity.EventId != eventId)
        {
            return AttendanceRecordingResult.Invalid(InvalidPassMessage);
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => RecordInsideTransactionAsync(eventId, registrationId, method, actorUserId, identity, token),
                cancellationToken);
        }
        catch (DomainException exception) when (exception.Outcome == OutcomeCode.Duplicate)
        {
            // Another scan committed the row first. Answer from the committed attendance instead of
            // failing the operator; the unique registration index is the final guard. A registration
            // that is no longer confirmed or eligible is still refused, so the refusal wins.
            var current = await attendance.GetByRegistrationIdAsync(registrationId, cancellationToken);
            if (current is null || !ParticipationFacts.IsValidAttendance(current)
                || !await StillEligibleForRepeatAsync(eventId, registrationId, identity, cancellationToken))
            {
                return AttendanceRecordingResult.Invalid(InvalidPassMessage);
            }

            var (memberName, studentNumber) = await ReadIdentityAsync(identity.UserId, cancellationToken);
            return new AttendanceRecordingResult(
                AttendanceRecordingOutcome.Repeat,
                RepeatMessage,
                registrationId,
                memberName,
                studentNumber,
                AsUtc(current.RecordedAt));
        }
    }

    // SQL datetime2 materializes as Unspecified. The stored value is UTC, and the JSON outcome must
    // carry the UTC marker so the browser renders the same time for the first scan and the repeat.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private async Task<bool> StillEligibleForRepeatAsync(
        int eventId,
        int registrationId,
        RegistrationIdentity identity,
        CancellationToken cancellationToken)
    {
        var registration = await registrations.GetForUpdateAsync(registrationId, cancellationToken);
        var eventItem = await events.GetEventAsync(eventId, cancellationToken);
        var user = await users.GetByIdAsync(identity.UserId, cancellationToken);
        var member = await members.GetMemberByUserIdAsync(identity.UserId, cancellationToken);

        return registration is not null
               && registration.EventId == eventId
               && registration.MemberId == identity.MemberId
               && ParticipationFacts.HoldsSeat(registration)
               && eventItem is not null
               && eventItem.EventStatusId == EventStatusCode.Published
               && eventItem.IsCheckInOpen
               && user is not null
               && member is not null
               && member.MemberId == identity.MemberId
               && ParticipationFacts.EligibleParticipant.Compile()(user, member);
    }

    private async Task<AttendanceRecordingResult> RecordInsideTransactionAsync(
        int eventId,
        int registrationId,
        CheckInMethodCode method,
        int actorUserId,
        RegistrationIdentity identity,
        CancellationToken cancellationToken)
    {
        // AD-4 lock order: the participant's users row, then the event, then the attendance row.
        var user = await users.LockUserAsync(identity.UserId, cancellationToken);
        var eventItem = await events.LockEventAsync(eventId, cancellationToken);

        // Re-read after the event lock: a concurrent scan for this event may have inserted first.
        var current = await attendance.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (current is not null)
        {
            current = await attendance.LockAttendanceAsync(current.AttendanceId, cancellationToken);
        }

        var registration = await registrations.GetForUpdateAsync(registrationId, cancellationToken);
        var member = await members.GetMemberByUserIdAsync(identity.UserId, cancellationToken);

        if (registration is null
            || registration.EventId != eventId
            || registration.MemberId != identity.MemberId
            || member is null
            || member.MemberId != identity.MemberId)
        {
            return AttendanceRecordingResult.Invalid(InvalidPassMessage);
        }

        if (eventItem is null || eventItem.EventStatusId != EventStatusCode.Published)
        {
            return AttendanceRecordingResult.Closed(EventClosedMessage);
        }

        if (!eventItem.IsCheckInOpen)
        {
            return AttendanceRecordingResult.Closed(WindowClosedMessage);
        }

        if (!ParticipationFacts.HoldsSeat(registration))
        {
            return AttendanceRecordingResult.Invalid(NotConfirmedMessage);
        }

        if (user is null || !ParticipationFacts.EligibleParticipant.Compile()(user, member))
        {
            return AttendanceRecordingResult.Invalid(IneligibleMessage);
        }

        if (current is not null)
        {
            // The refusal checks above win over the repeat: an invalidated row is never a repeat.
            if (ParticipationFacts.IsInvalidated(current.InvalidatedAt))
            {
                return AttendanceRecordingResult.Invalid(InvalidatedMessage);
            }

            return new AttendanceRecordingResult(
                AttendanceRecordingOutcome.Repeat,
                RepeatMessage,
                registrationId,
                user.Name,
                member.StudentNumber,
                AsUtc(current.RecordedAt));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var row = new Attendance
        {
            RegistrationId = registrationId,
            CheckInMethodId = method,
            RecordedBy = actorUserId,
            RecordedAt = now
        };
        await attendance.AddAsync(row, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        audit.Append(new AuditEntry(
            AuditActions.AttendanceRecorded,
            "attendance",
            row.AttendanceId,
            NewValues: new AttendanceAuditValues(eventId, registrationId, (int)method, now)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AttendanceRecordingResult(
            AttendanceRecordingOutcome.Recorded,
            RecordedMessage,
            registrationId,
            user.Name,
            member.StudentNumber,
            now);
    }

    private async Task<(string MemberName, string StudentNumber)> ReadIdentityAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        var member = await members.GetMemberByUserIdAsync(userId, cancellationToken);
        return (user?.Name, member?.StudentNumber);
    }

    private sealed record AttendanceAuditValues(int EventId, int RegistrationId, int CheckInMethodId, DateTime RecordedAt) : IAuditValues;
}
