using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ASI.Basecode.WebApp.DemoData;

// M-10b/M-11a/M-11b/M-12: the real registrations take precedence; the demo provider only overlays
// the still-demo issued state and documents, and never replaces a real provider with a fallback.
internal static class DemoCertificatesRegistration
{
    public static void AddCertificates(IServiceCollection services, bool enabled)
    {
        if (enabled)
        {
            services.TryAddScoped<DemoCertificateDocuments>();
            services.Replace(ServiceDescriptor.Scoped<ICertificateLedgerService, DemoCertificateLedgerService>());
            services.TryAddScoped<IMyCertificatesService, DemoMyCertificatesService>();
        }
        else
        {
            services.TryAddScoped<IMyCertificatesService, UnavailableMyCertificatesService>();
        }
    }
}
