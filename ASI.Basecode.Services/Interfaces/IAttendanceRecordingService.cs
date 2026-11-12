using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// The one shared check-in recording engine (AD-2, AD-4). The door scanner and, from 4.5, the manual
/// adapter call the same validation and write path: it rechecks the event, registration, eligibility
/// and attendance inside a caller-owned transaction and writes at most one attendance row plus one
/// <c>attendance.recorded</c> audit entry. Refusals are returned, not thrown; only a missing
/// Management role throws <c>Forbidden</c>. A refusal result never carries a name, student number or
/// recorded time.
/// </summary>
public interface IAttendanceRecordingService
{
    Task<AttendanceRecordingResult> RecordAsync(
        int eventId,
        int registrationId,
        CheckInMethodCode method,
        CancellationToken cancellationToken = default);
}

public enum AttendanceRecordingOutcome
{
    Recorded,
    Repeat,
    Invalid,
    Closed
}

/// <summary>
/// The engine's outcome. Identity and time are populated only for <see cref="AttendanceRecordingOutcome.Recorded"/>
/// and <see cref="AttendanceRecordingOutcome.Repeat"/>.
/// </summary>
public sealed record AttendanceRecordingResult(
    AttendanceRecordingOutcome Outcome,
    string Message,
    int RegistrationId = 0,
    string MemberName = null,
    string StudentNumber = null,
    DateTime? RecordedAt = null)
{
    public bool IsRecorded => Outcome is AttendanceRecordingOutcome.Recorded or AttendanceRecordingOutcome.Repeat;

    public static AttendanceRecordingResult Invalid(string message) =>
        new(AttendanceRecordingOutcome.Invalid, message);

    public static AttendanceRecordingResult Closed(string message) =>
        new(AttendanceRecordingOutcome.Closed, message);
}
