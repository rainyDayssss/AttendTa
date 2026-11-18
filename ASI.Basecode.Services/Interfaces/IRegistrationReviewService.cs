using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

public interface IRegistrationReviewService
{
    Task<IReadOnlyList<EventSelectorOption>> GetEventOptionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<EventSelectorOption>>(Array.Empty<EventSelectorOption>());
    Task<PagedResult<RegistrationReviewRow>> GetPageAsync(string status, int? eventId, int? page, CancellationToken ct = default);

    /// <summary>
    /// Counts one review status for an event (or the whole queue) without materializing a page.
    /// </summary>
    Task<int> GetCountAsync(string status, int? eventId, CancellationToken ct = default);
    Task ApproveAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Approves each distinct id through the single-record approval path, in the review queue's own
    /// order, and reports one outcome per id. A refusal never rolls back another record.
    /// </summary>
    /// <param name="registrationIds">The posted ids; duplicates are decided once.</param>
    /// <param name="eventId">The dossier's event scope, or <c>null</c> for the whole review queue.</param>
    Task<IReadOnlyList<RegistrationApprovalOutcome>> ApproveManyAsync(IReadOnlyCollection<int> registrationIds, int? eventId, CancellationToken ct = default);

    Task DeclineAsync(int id, string reason, CancellationToken ct = default);
    Task CancelAsync(int id, string reason, CancellationToken ct = default);

    /// <summary>Confirms the earliest eligible waitlisted registration for the event, in queue order.</summary>
    Task<RegistrationSummary> GetPromotionCandidateAsync(int eventId, CancellationToken ct = default);
    Task PromoteNextAsync(int eventId, int expectedRegistrationId, CancellationToken ct = default);
}
