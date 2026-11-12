using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Interfaces;

public interface IAccountRequestRepository
{
    Task<AccountRequest> LockAccountRequestAsync(int requestId, CancellationToken cancellationToken = default);

    /// <summary>Counts requests for one status, or every request when <paramref name="status"/> is null.</summary>
    Task<int> CountByStatusAsync(AccountRequestStatusCode? status, string search, CancellationToken cancellationToken = default);

    /// <summary>Pages requests for one status, or every request when <paramref name="status"/> is null.</summary>
    Task<IReadOnlyList<AccountRequestQueueItem>> GetPageByStatusAsync(AccountRequestStatusCode? status, string search, bool oldestFirst, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>Counts requests per status so the queue can label its filters.</summary>
    Task<AccountRequestStatusCounts> GetStatusCountsAsync(string search, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new account request. The caller saves through the unit of work.
    /// </summary>
    Task AddAsync(AccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a pending request already uses the email. Finalized requests are ignored.
    /// </summary>
    Task<bool> PendingEmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a pending request already uses the student number. Finalized requests are ignored.
    /// </summary>
    Task<bool> PendingStudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken = default);
}
