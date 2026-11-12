using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Composes the real account or member read, the real 5.3 registration preview and the
/// last-administrator count into the access dialog models, so controllers never touch repositories.
/// </summary>
public interface IAccessDialogService
{
    /// <summary>Builds the account access dialog for one account.</summary>
    /// <exception cref="ASI.Basecode.Resources.DomainException">The account does not exist.</exception>
    Task<AccountAccessViewModel> GetAccountAccessAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the read-only deactivation impact preview for one account. It returns the same
    /// authoritative identity as the access dialog but always reads the upcoming registrations,
    /// whatever the account's active state, and never takes the last-administrator count.
    /// </summary>
    /// <exception cref="ASI.Basecode.Resources.DomainException">The account does not exist.</exception>
    Task<AccountAccessViewModel> GetDeactivationPreviewAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>Builds the membership access dialog for one member.</summary>
    /// <exception cref="ASI.Basecode.Resources.DomainException">The member does not exist.</exception>
    Task<MembershipAccessViewModel> GetMembershipAccessAsync(int memberId, CancellationToken cancellationToken = default);
}
