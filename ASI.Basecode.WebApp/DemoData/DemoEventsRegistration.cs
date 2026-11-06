using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The events surface's demo registration file (register M-04 and M-09; register rule 9). A surface
/// adds only its own providers here, so removing a register row means deleting its classes and its
/// line in this file.
/// </summary>
internal static class DemoEventsRegistration
{
    public static void AddDemoEvents(IServiceCollection services)
    {
        services.AddScoped<IEventStateCommands, DemoEventStateCommands>();
        services.AddScoped<IEventStateOverlay, DemoEventStateOverlay>();
    }
}
