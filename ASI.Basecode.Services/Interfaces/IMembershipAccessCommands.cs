using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Membership access commands for the Management members list. Both are real transactional commands
/// (5.6 deactivation, 5.7 reactivation) and raise the same
/// <see cref="ASI.Basecode.Resources.DomainException"/> outcomes as the screen expects. Membership
/// status is separate from account access.
/// </summary>
public interface IMembershipAccessCommands
{
    /// <summary>Deactivates a membership. A reason is required.</summary>
    Task DeactivateAsync(int memberId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Reactivates a membership. A reason is required.</summary>
    Task ReactivateAsync(int memberId, string reason, CancellationToken cancellationToken = default);
}
