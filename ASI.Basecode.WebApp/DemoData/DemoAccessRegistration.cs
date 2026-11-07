using ASI.Basecode.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The account and membership access surface's demo registration file (register M-14b, M-15a,
/// M-15b; register rule 9). It adds only this surface's providers, so removing a register row means
/// deleting its classes and its line here. The account wrapper is registered last so its real
/// deactivation delegation and demo reactivation win over the real command registered by DI.
/// </summary>
internal static class DemoAccessRegistration
{
    public static void AddDemoAccess(IServiceCollection services)
    {
        services.AddScoped<IAccountAccessCommands, DemoAccountAccessCommands>();
        services.AddScoped<IMembershipAccessCommands, DemoMembershipAccessCommands>();
        services.AddScoped<IAccessStateOverlay, DemoAccessStateOverlay>();
    }
}
