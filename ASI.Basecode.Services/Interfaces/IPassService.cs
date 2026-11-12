using ASI.Basecode.Resources;
using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// The member's door passes: the owner-scoped list and the authorized download of one pass PNG.
/// Only the actor's own Confirmed registration on a Published event with a stored token yields a
/// pass; every other request receives one indistinguishable refusal.
/// </summary>
public interface IPassService
{
    /// <summary>
    /// Returns only the actor's own confirmed registrations on published events, split into
    /// upcoming and past passes. Another member's registration is never returned.
    /// </summary>
    Task<PassesViewModel> GetMyPassesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Authorizes one pass request and returns only its token and file name. Unknown, foreign,
    /// cancelled, unconfirmed, unpublished and token-less registrations all throw the same
    /// <see cref="DomainException"/> with <see cref="OutcomeCode.Forbidden"/>.
    /// </summary>
    Task<PassDownload> GetPassDownloadAsync(int registrationId, CancellationToken cancellationToken = default);
}
