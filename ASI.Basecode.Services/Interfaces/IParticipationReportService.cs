using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Per-event participation report read. The demo provider implements this contract now; 7.1, 7.2
/// and 7.3 implement the real reads behind the same contract and remove the demo rows M-13a,
/// M-13b and M-13c. Reads only: the report never mutates participation records.
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
