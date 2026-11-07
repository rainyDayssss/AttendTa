using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>
/// Read-only participation report facts for one event. Counts, no-shows, distributions and the rate
/// are derived from the canonical <see cref="ASI.Basecode.Data.Queries.ParticipationFacts"/>; the
/// paged detail rows are the event's registrations joined to their member and attendance. The
/// report never mutates participation records.
/// </summary>
public interface IParticipationReportRepository
{
    /// <summary>
    /// Returns the full event registration status counts, the canonical Confirmed and Valid attendee
    /// counts and the unrounded Valid-over-Confirmed rate. The rate is null when Confirmed is zero.
    /// </summary>
    Task<ParticipationReportSummaryFacts> GetSummaryFactsAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the canonical No-show count for one event: Confirmed registrations that have no valid
    /// attendance. The fact is Completed-only, so any other event status returns zero.
    /// </summary>
    Task<int> GetNoShowCountAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the valid attendees of one event grouped by the course and year level captured at
    /// signup. It never joins users or members, so inactive history stays visible.
    /// </summary>
    Task<ParticipationReportDistributionFacts> GetAttendeeDistributionAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the number of detail rows for one event: every registration in every status, so the
    /// pager's total covers the full dataset rather than the requested page.
    /// </summary>
    Task<int> CountDetailRowsAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of detail rows for one event, ordered by participant name and then
    /// <c>registration_id</c> so page boundaries are stable. A null <c>CheckedInAt</c> means the
    /// registration has no attendance row.
    /// </summary>
    Task<IReadOnlyList<ParticipationReportDetailRow>> GetDetailPageAsync(int eventId, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>The canonical summary values for one event, before any presentation rounding.</summary>
public sealed record ParticipationReportSummaryFacts(
    int PendingCount,
    int ConfirmedCount,
    int WaitlistedCount,
    int CancelledCount,
    int ValidAttendeeCount,
    decimal? AttendanceRate);

/// <summary>The valid-attendee distribution for one event, grouped by signup snapshot.</summary>
public sealed record ParticipationReportDistributionFacts(
    IReadOnlyList<CourseDistributionRow> Courses,
    IReadOnlyList<YearDistributionRow> Years);

/// <summary>One course snapshot group, before presentation labels, ordering and percentages.</summary>
public sealed record CourseDistributionRow(string Course, int Count);

/// <summary>One year-level snapshot group, before presentation labels, ordering and percentages.</summary>
public sealed record YearDistributionRow(int YearLevel, int Count);

/// <summary>
/// One participant detail row, before presentation labels. <see cref="CheckedInAt"/> is null when the
/// registration has no attendance row; <see cref="IsAttendanceInvalidated"/> separates an invalidated
/// check-in from a valid one.
/// </summary>
public sealed record ParticipationReportDetailRow(
    int RegistrationId,
    string MemberName,
    string StudentNumber,
    string Course,
    int YearLevel,
    RegistrationStatusCode Status,
    DateTime? CheckedInAt,
    bool IsAttendanceInvalidated);
