using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The one demo registration method (register M-01; register rule 9). Called from
/// <c>Startup.DI.cs</c> behind <c>DemoData:Enabled</c>; it calls one registration file per surface.
/// With the switch off it registers nothing beyond the certificate providers, so the normal screens
/// are untouched. Check-in by scan, the manual lookup, attendance invalidation, both account
/// commands and both membership commands are the real services registered by DI in both switch
/// states.
/// </summary>
internal static class DemoDataRegistration
{
    public static void AddDemoData(this IServiceCollection services, IConfiguration configuration)
    {
        DemoCertificatesRegistration.AddCertificates(services, configuration.GetValue<bool>(DemoDataOptions.SectionName + ":Enabled"));
        if (!configuration.GetValue<bool>(DemoDataOptions.SectionName + ":Enabled"))
        {
            return;
        }

        services.Configure<DemoDataOptions>(configuration.GetSection(DemoDataOptions.SectionName));
        services.AddSingleton<DemoStore>();
        services.AddHostedService<DemoSeedService>();

        DemoReportsRegistration.AddDemoReports(services);
    }
}
