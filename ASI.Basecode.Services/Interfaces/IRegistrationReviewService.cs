using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

public interface IRegistrationReviewService
{
    Task<PagedResult<RegistrationReviewRow>> GetPageAsync(string status, int? eventId, int? page, CancellationToken ct = default);
    Task ApproveAsync(int id, CancellationToken ct = default);
    Task DeclineAsync(int id, string reason, CancellationToken ct = default);
    Task CancelAsync(int id, string reason, CancellationToken ct = default);

    /// <summary>Confirms the earliest eligible waitlisted registration for the event, in queue order.</summary>
    Task PromoteNextAsync(int eventId, CancellationToken ct = default);
}
