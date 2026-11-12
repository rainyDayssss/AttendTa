using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// Builds the two access dialogs from the real reads. The account dialog adds the
/// last-active-Administrator flag and the real upcoming-registration preview; the membership dialog
/// previews through the member's linked user id. Nothing here writes.
/// </summary>
public sealed class AccessDialogService : IAccessDialogService
{
    private readonly IAccountAdministrationRepository _accountAdministrationRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IRegistrationCancellationService _registrationCancellationService;

    public AccessDialogService(
        IAccountAdministrationRepository accountAdministrationRepository,
        IMemberRepository memberRepository,
        IRegistrationCancellationService registrationCancellationService)
    {
        _accountAdministrationRepository = accountAdministrationRepository ?? throw new ArgumentNullException(nameof(accountAdministrationRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _registrationCancellationService = registrationCancellationService ?? throw new ArgumentNullException(nameof(registrationCancellationService));
    }

    public async Task<AccountAccessViewModel> GetAccountAccessAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accountAdministrationRepository.GetAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new DomainException(OutcomeCode.NotFound, "Account not found.");
        }

        var isAdministrator = string.Equals(account.RoleCode, RoleCodes.Admin, StringComparison.Ordinal);
        // Best-effort display read: the count is not taken under the last-administrator lock, so it
        // can be stale. The write path re-checks it under the lock before a demotion or deactivation.
        var isLastActiveAdministrator = account.IsActive
            && isAdministrator
            && await _accountAdministrationRepository.CountActiveAdministratorsAsync(cancellationToken) <= 1;

        var affected = account.IsActive
            ? await _registrationCancellationService.PreviewUpcomingForUserAsync(account.AccountId, cancellationToken)
            : Array.Empty<UpcomingRegistrationPreview>();

        return new AccountAccessViewModel
        {
            AccountId = account.AccountId,
            UserId = account.AccountId,
            FullName = account.FullName,
            Email = account.Email,
            RoleCode = account.RoleCode,
            RoleLabel = account.RoleLabel,
            IsActive = account.IsActive,
            IsLastActiveAdministrator = isLastActiveAdministrator,
            AffectedRegistrations = affected
        };
    }

    public async Task<AccountAccessViewModel> GetDeactivationPreviewAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accountAdministrationRepository.GetAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new DomainException(OutcomeCode.NotFound, "Account not found.");
        }

        // The preview is read-only, so it always lists the upcoming registrations a deactivation
        // would cancel and never takes the last-administrator count.
        var affected = await _registrationCancellationService.PreviewUpcomingForUserAsync(account.AccountId, cancellationToken);

        return new AccountAccessViewModel
        {
            AccountId = account.AccountId,
            UserId = account.AccountId,
            FullName = account.FullName,
            Email = account.Email,
            RoleCode = account.RoleCode,
            RoleLabel = account.RoleLabel,
            IsActive = account.IsActive,
            AffectedRegistrations = affected
        };
    }

    public async Task<MembershipAccessViewModel> GetMembershipAccessAsync(int memberId, CancellationToken cancellationToken = default)
    {
        var member = await _memberRepository.GetMemberRecordAsync(memberId, cancellationToken);
        if (member is null)
        {
            throw new DomainException(OutcomeCode.NotFound, "Member not found.");
        }

        var isActive = string.Equals(member.Status, "active", StringComparison.OrdinalIgnoreCase);
        var affected = isActive
            ? await _registrationCancellationService.PreviewUpcomingForUserAsync(member.UserId, cancellationToken)
            : Array.Empty<UpcomingRegistrationPreview>();

        return new MembershipAccessViewModel
        {
            MemberId = member.MemberId,
            UserId = member.UserId,
            FullName = member.FullName,
            StudentNumber = member.StudentNumber,
            IsActive = isActive,
            AffectedRegistrations = affected
        };
    }
}
