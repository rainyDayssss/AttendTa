using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>
/// Attendance persistence for the check-in module (AD-2). Writes stage rows for the caller's unit of
/// work and never save or open a transaction; reads compose the canonical facts.
/// </summary>
public interface IAttendanceRepository
{
    /// <summary>
    /// Takes the attendance row lock by primary key (AD-4) and returns the fresh tracked row, or
    /// <c>null</c> when the row no longer exists. A transaction is required.
    /// </summary>
    Task<Attendance> LockAttendanceAsync(int attendanceId, CancellationToken cancellationToken = default);

    /// <summary>Reads the registration's attendance row, or <c>null</c> when it has none.</summary>
    Task<Attendance> GetByRegistrationIdAsync(int registrationId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new attendance row; the caller saves it inside its transaction.</summary>
    Task AddAsync(Attendance attendance, CancellationToken cancellationToken = default);

    /// <summary>
    /// The event's most recent attendance rows for the scanner log, newest first, capped at 25 and
    /// ordered by <c>recorded_at DESC, attendance_id DESC</c>. Each row carries whether the
    /// registration's certificate is already issued so the log can hide the invalidation action.
    /// </summary>
    Task<IReadOnlyList<AttendanceLogRow>> GetRecentForEventAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registration ids of the event with Valid Attendance, ordered by registration id. Invalidated
    /// rows never match.
    /// </summary>
    Task<IReadOnlyList<int>> GetValidAttendedRegistrationIdsAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the actor's attendance rows across events, resolving ownership through
    /// <c>registrations.member_id</c> to <c>members.user_id</c>.
    /// </summary>
    Task<int> CountMyAttendanceAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one owner-scoped page of the actor's attendance records across events, newest recorded
    /// first, ordered by <c>recorded_at DESC, attendance_id DESC</c>, with the event title, the
    /// check-in method and the invalidation state the member history needs. Another member's row
    /// never matches.
    /// </summary>
    Task<IReadOnlyList<MyAttendanceRow>> GetMyAttendanceAsync(int userId, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>One attendance row joined to its participant and certificate state for the scanner log.</summary>
public sealed record AttendanceLogRow(
    int AttendanceId,
    int RegistrationId,
    string MemberName,
    string StudentNumber,
    DateTime RecordedAt,
    DateTime? InvalidatedAt,
    string InvalidationReason,
    bool IsCertificateIssued)
{
    public bool IsValid => InvalidatedAt is null;
}

/// <summary>One attendance row joined to its event for the member Participation History.</summary>
public sealed record MyAttendanceRow(
    int AttendanceId,
    int EventId,
    string EventTitle,
    DateTime RecordedAt,
    CheckInMethodCode CheckInMethod,
    DateTime? InvalidatedAt,
    string InvalidationReason)
{
    public bool IsValid => InvalidatedAt is null;
}
