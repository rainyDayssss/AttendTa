using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>
/// Read-only participation report facts for one event. Counts, no-shows, distributions and the rate
/// are derived from the canonical <see cref="ASI.Basecode.Data.Queries.ParticipationFacts"/>; the
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
