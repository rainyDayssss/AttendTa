using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

// M-12: empty history and standard refusal until the real provider is registered.
public sealed class UnavailableMyCertificatesService : IMyCertificatesService
{
    public Task<PagedResult<MyEventCertificateRowViewModel>> GetPageAsync(int? page, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PagedResult<MyEventCertificateRowViewModel>(Array.Empty<MyEventCertificateRowViewModel>(), 1, 0));
    }

    public Task<CertificateDocument> GetPdfAsync(int certificateId, CancellationToken cancellationToken = default) =>
        throw new DomainException(OutcomeCode.NotFound, "Certificate not found.");
}
