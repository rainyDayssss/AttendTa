using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>
/// Read-only participation report facts for one event. Counts and the rate are derived from the
/// canonical <see cref="ASI.Basecode.Data.Queries.ParticipationFacts"/>; the report never mutates
/// participation records.
/// </summary>
public interface IParticipationReportRepository
{
    /// <summary>
    /// Returns the full event registration status counts, the canonical Confirmed and Valid attendee
    /// counts and the unrounded Valid-over-Confirmed rate. The rate is null when Confirmed is zero.
    /// </summary>
    Task<ParticipationReportSummaryFacts> GetSummaryFactsAsync(int eventId, CancellationToken cancellationToken = default);
}

/// <summary>The canonical summary values for one event, before any presentation rounding.</summary>
public sealed record ParticipationReportSummaryFacts(
    int PendingCount,
    int ConfirmedCount,
    int WaitlistedCount,
    int CancelledCount,
    int ValidAttendeeCount,
    decimal? AttendanceRate);
