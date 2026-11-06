using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The My Events surface's demo registration file (register M-02; register rule 9). Removing the
/// row means deleting its classes and its line in this file.
/// </summary>
internal static class DemoMyEventsRegistration
{
    public static void AddDemoMyEvents(IServiceCollection services)
    {
        services.AddScoped<IMyEventsService, DemoMyEventsService>();
    }
}
