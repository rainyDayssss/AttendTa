using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

// M-10b/M-11a/M-11b: switch-off behavior until real queries and commands are integrated.
public sealed class UnavailableCertificateLedgerService : ICertificateLedgerService
{
    public Task<CertificateLedgerViewModel> GetPageAsync(int? eventId, CertificateLedgerTab tab, int? page, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CertificateLedgerViewModel { ActiveTab = tab });

    public Task DecideAsync(int registrationId, bool approve, string reason, CancellationToken cancellationToken = default) => Task.FromException(Unavailable());
    public Task IssueAsync(int registrationId, CancellationToken cancellationToken = default) => Task.FromException(Unavailable());
    public Task<CertificateViewerViewModel> GetViewerAsync(int registrationId, CancellationToken cancellationToken = default) => Task.FromException<CertificateViewerViewModel>(NotFound());
    public Task<CertificateDocument> GetPdfAsync(int registrationId, CancellationToken cancellationToken = default) => Task.FromException<CertificateDocument>(NotFound());
    private static DomainException Unavailable() => new(OutcomeCode.Invalid, "Certificate management is currently unavailable. Please try again later.");
    private static DomainException NotFound() => new(OutcomeCode.NotFound, "Certificate not found.");
}
