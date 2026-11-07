using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>
/// Certificate review persistence for the event completion and attendance invalidation hooks. Reads
/// compose the canonical facts; writes stage or mutate rows for the caller's unit of work and never
/// save on their own.
/// </summary>
public interface ICertificateRepository
{
    /// <summary>
    /// Registration ids of the event that have Valid Attendance and no certificate row yet, ordered by
    /// registration id. Invalidated attendance and registrations already carrying a review never match.
    /// </summary>
    Task<IReadOnlyList<int>> GetRegistrationsAwaitingReviewAsync(int eventId, CancellationToken ct = default);

    /// <summary>
    /// The registration's certificate row, tracked by the caller's unit of work, or <c>null</c> when
    /// the registration has none. Callers mutate the returned row inside their own transaction and
    /// save through their unit of work; this read never saves and never detaches.
    /// </summary>
    Task<Certificate> GetByRegistrationIdAsync(int registrationId, CancellationToken ct = default);

    /// <summary>Stages the certificate rows; the caller saves them inside its transaction.</summary>
    Task AddRangeAsync(IReadOnlyList<Certificate> certificates, CancellationToken ct = default);
}
