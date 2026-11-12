using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Account access commands for the Administrator accounts list. Both deactivation and reactivation
/// are real transactional commands and raise the same <see cref="ASI.Basecode.Resources.DomainException"/>
/// outcomes as the screen expects. Account access is separate from membership eligibility.
/// </summary>
public interface IAccountAccessCommands
{
    /// <summary>Deactivates an account. A reason is required; the last active Administrator is refused.</summary>
    Task DeactivateAsync(int accountId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Reactivates an account. A reason is required.</summary>
    Task ReactivateAsync(int accountId, string reason, CancellationToken cancellationToken = default);
}
