using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>
/// Certificate review persistence for the event completion hook. Reads compose the canonical facts;
/// writes stage rows for the caller's unit of work and never save on their own.
/// </summary>
public interface ICertificateRepository
{
    /// <summary>
    /// Registration ids of the event that have Valid Attendance and no certificate row yet, ordered by
    /// registration id. Invalidated attendance and registrations already carrying a review never match.
    /// </summary>
    Task<IReadOnlyList<int>> GetRegistrationsAwaitingReviewAsync(int eventId, CancellationToken ct = default);

    /// <summary>Stages the certificate rows; the caller saves them inside its transaction.</summary>
    Task AddRangeAsync(IReadOnlyList<Certificate> certificates, CancellationToken ct = default);
}
