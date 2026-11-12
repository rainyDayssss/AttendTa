using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces;

public interface IAccountAdministrationRepository
{
    /// <summary>
    /// Counts accounts whose full name or email contains the trimmed search term.
    /// </summary>
    Task<int> CountAsync(string search, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of safe account summaries ordered by full name, then account ID.
    /// </summary>
    Task<IReadOnlyList<AccountListItem>> GetPageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one safe account summary with its role display fields, or <c>null</c> when it does not exist.
    /// </summary>
    Task<AccountListItem> GetAccountAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the fixed role lookup ordered by role id.
    /// </summary>
    Task<IReadOnlyList<RoleOption>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts active accounts whose role is Administrator (<c>role_id = 1 AND is_active = 1</c>).
    /// </summary>
    Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the transaction-scoped <c>attendta:last-admin</c> application lock. A negative
    /// <c>sp_getapplock</c> result is surfaced as <see cref="ASI.Basecode.Resources.DomainException"/>
    /// with <see cref="ASI.Basecode.Resources.OutcomeCode.Stale"/>.
    /// </summary>
    Task AcquireLastAdministratorLockAsync(CancellationToken cancellationToken = default);
}
