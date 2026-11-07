using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ASI.Basecode.WebApp.DemoData;

// M-10a/M-10b/M-11a/M-11b/M-12: real registrations take precedence; never replace or fall back from one.
internal static class DemoCertificatesRegistration
{
    public static void AddCertificates(IServiceCollection services, bool enabled)
    {
        if (enabled)
        {
            services.TryAddScoped<DemoCertificateDocuments>();
            services.TryAddScoped<ICertificateLedgerService, DemoCertificateLedgerService>();
            services.TryAddScoped<IMyCertificatesService, DemoMyCertificatesService>();
        }
        else
        {
            services.TryAddScoped<ICertificateLedgerService, UnavailableCertificateLedgerService>();
            services.TryAddScoped<IMyCertificatesService, UnavailableMyCertificatesService>();
        }
    }
}
