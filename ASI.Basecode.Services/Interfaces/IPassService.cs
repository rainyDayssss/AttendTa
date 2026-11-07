using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// The member's door passes. This is the seam 4.1 replaces with the real owner-scoped query and
/// server-rendered QR; until then the demo provider composes the existing reads behind it.
/// (Register M-03.)
/// </summary>
public interface IPassService
{
    /// <summary>
    /// Returns only the actor's own confirmed registrations on published events, split into
    /// upcoming and past passes. Another member's registration is never returned.
    /// </summary>
    Task<PassesViewModel> GetMyPassesAsync(CancellationToken cancellationToken = default);
}
