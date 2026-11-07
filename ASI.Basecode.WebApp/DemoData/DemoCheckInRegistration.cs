using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The check-in surface's demo registration file (register M-07; register rule 9). A surface adds
/// only its own providers here, so removing a register row means deleting its classes and its line
/// in this file. Check-in by scan (M-05) and the manual lookup (M-06) are the real services
/// registered by DI.
/// </summary>
internal static class DemoCheckInRegistration
{
    public static void AddDemoCheckIn(IServiceCollection services)
    {
        services.AddScoped<IAttendanceInvalidationService, DemoAttendanceInvalidationService>();
    }
}
