using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

public interface IAccountRoleService
{
    /// <summary>
    /// Returns the role change form for one account, including the fixed role choices.
    /// </summary>
    Task<ChangeAccountRoleViewModel> GetRoleChangeAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns the requested role, stamps <c>updated_by</c>/<c>updated_at</c>, and appends one
    /// <c>user.role_assigned</c> Activity Log entry in the same transaction. A no-op change
    /// succeeds without an audit row; removing the last active Administrator is refused.
    /// </summary>
    Task ChangeRoleAsync(ChangeAccountRoleViewModel model, CancellationToken cancellationToken = default);
}
