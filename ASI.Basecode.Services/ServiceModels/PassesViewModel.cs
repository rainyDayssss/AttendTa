using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Door Passes screen host. Active holds confirmed registrations whose event has not started;
/// Past holds confirmed registrations whose event start has passed. A cancelled, waitlisted or
/// pending registration never appears in either list.
/// </summary>
public sealed class PassesViewModel
{
    public IReadOnlyList<PassViewModel> Active { get; init; } = Array.Empty<PassViewModel>();
    public IReadOnlyList<PassViewModel> Past { get; init; } = Array.Empty<PassViewModel>();

    public static PassesViewModel Empty() => new();
}
