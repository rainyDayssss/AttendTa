using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Membership access commands for the Management members list. The demo provider implements this
/// contract now; 5.6 (deactivation) and 5.7 (reactivation) implement the real one behind the same
/// contract and raise the same <see cref="ASI.Basecode.Resources.DomainException"/> outcomes.
/// Membership status is separate from account access.
/// </summary>
public interface IMembershipAccessCommands
{
    /// <summary>Deactivates a membership. A reason is required.</summary>
    Task DeactivateAsync(int memberId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Reactivates a membership. A reason is required.</summary>
    Task ReactivateAsync(int memberId, string reason, CancellationToken cancellationToken = default);
}
