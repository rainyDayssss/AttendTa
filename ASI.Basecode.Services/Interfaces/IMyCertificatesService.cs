using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>Personal certificate history and documents, scoped to the current actor on every read.</summary>
public interface IMyCertificatesService
{
    Task<PagedResult<MyEventCertificateRowViewModel>> GetPageAsync(int? page, CancellationToken cancellationToken = default);
    Task<CertificateDocument> GetPdfAsync(int certificateId, CancellationToken cancellationToken = default);
}
