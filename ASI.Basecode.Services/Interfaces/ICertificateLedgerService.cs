using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

public interface ICertificateLedgerService
{
    Task<CertificateLedgerViewModel> GetPageAsync(int? eventId, CertificateLedgerTab tab, int? page, CancellationToken cancellationToken = default);
    Task DecideAsync(int registrationId, bool approve, string reason, CancellationToken cancellationToken = default);
    Task IssueAsync(int registrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the selected pending reviews through the single-record decision path and returns one
    /// outcome per distinct id in first-seen order. Each record keeps its own transaction, lock order
    /// and audit entry, so a refusal rolls back only that record.
    /// </summary>
    Task<IReadOnlyList<CertificateApprovalOutcome>> ApproveManyAsync(
        IReadOnlyCollection<int> registrationIds,
        CancellationToken cancellationToken = default);

    Task<CertificateViewerViewModel> GetViewerAsync(int registrationId, CancellationToken cancellationToken = default);
    Task<CertificateDocument> GetPdfAsync(int registrationId, CancellationToken cancellationToken = default);
}
