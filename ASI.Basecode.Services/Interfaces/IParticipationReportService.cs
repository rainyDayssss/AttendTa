using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Per-event participation report read. 7.1, 7.2 and 7.3 are all real: the summary, no-shows,
/// attendee distributions and paged detail rows come from canonical SQL, with no demo provider.
/// Reads only: the report never mutates participation records.
/// </summary>
public interface IParticipationReportService
{
    /// <summary>
    /// Returns the report for one event, or the selector plus an empty report when no event is
    /// selected. Summary values and breakdowns cover the full dataset; <paramref name="page"/>
    /// only moves the detail rows.
    /// </summary>
    Task<ParticipationReportViewModel> GetReportAsync(int? eventId, int? page, CancellationToken cancellationToken = default);
}
