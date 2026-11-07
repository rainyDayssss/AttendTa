using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The passes surface's demo registration file (register M-03; register rule 9). A surface adds only
/// its own providers here, so removing the register row means deleting its classes and its line in
/// this file.
/// </summary>
internal static class DemoPassesRegistration
{
    public static void AddDemoPasses(IServiceCollection services)
    {
        services.AddScoped<IPassService, DemoPassService>();
    }
}
