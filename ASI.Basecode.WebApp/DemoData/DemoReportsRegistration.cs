using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The reports surface's demo registration file (register M-13b and M-13c; register rule 9). The
/// demo provider overrides the real participation report service on the demo host and overlays the
/// still-open fields; a surface adds only its own providers here, so removing a register row means
/// deleting its classes and its line in this file.
/// </summary>
internal static class DemoReportsRegistration
{
    public static void AddDemoReports(IServiceCollection services)
    {
        services.AddScoped<IParticipationReportService, DemoParticipationReportService>();
    }
}
