using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// Administrator-only role assignment. Every change runs in one transaction that takes the
/// <c>attendta:last-admin</c> application lock before the <c>users</c> row lock whenever the
/// change could remove an active Administrator.
/// </summary>
public sealed class AccountRoleService : IAccountRoleService
{
    private readonly IAccountAdministrationRepository _accountAdministrationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentActor _currentActor;
    private readonly IActivityLogWriter _activityLogWriter;

    public AccountRoleService(
        IAccountAdministrationRepository accountAdministrationRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ICurrentActor currentActor,
        IActivityLogWriter activityLogWriter)
    {
        _accountAdministrationRepository = accountAdministrationRepository ?? throw new ArgumentNullException(nameof(accountAdministrationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _currentActor = currentActor ?? throw new ArgumentNullException(nameof(currentActor));
        _activityLogWriter = activityLogWriter ?? throw new ArgumentNullException(nameof(activityLogWriter));
    }

    /// <inheritdoc />
    public async Task<ChangeAccountRoleViewModel> GetRoleChangeAsync(int accountId, CancellationToken cancellationToken = default)
    {
        AdministrationCommandGuards.RequireAdministrator(_currentActor);

        var account = await _accountAdministrationRepository.GetAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new DomainException(OutcomeCode.NotFound, "Account not found.");
        }

        var roles = await _accountAdministrationRepository.GetRolesAsync(cancellationToken);

        return new ChangeAccountRoleViewModel
        {
            AccountId = account.AccountId,
            FullName = account.FullName,
            Email = account.Email,
            CurrentRoleCode = account.RoleCode,
            CurrentRoleLabel = account.RoleLabel,
            RoleCode = account.RoleCode,
            Roles = roles
        };
    }

    /// <inheritdoc />
    public async Task ChangeRoleAsync(ChangeAccountRoleViewModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var actorUserId = AdministrationCommandGuards.RequireAdministrator(_currentActor);
        var requestedRole = RequiredRole(model.RoleCode);
        var reason = AdministrationCommandGuards.RequiredReason(model.Reason, nameof(ChangeAccountRoleViewModel.Reason));

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var snapshot = await _accountAdministrationRepository.GetAccountAsync(model.AccountId, ct);
            if (snapshot is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Account not found.");
            }

            // The pre-lock read only decides whether the application lock is needed. The
            // authoritative decision is made after the users row lock is held.
            var preLockDemotion = IsActiveAdministratorDemotion(
                snapshot.IsActive,
                string.Equals(snapshot.RoleCode, RoleCodes.Admin, StringComparison.Ordinal),
                requestedRole);

            if (preLockDemotion)
            {
                await _accountAdministrationRepository.AcquireLastAdministratorLockAsync(ct);
            }

            var user = await _userRepository.LockUserAsync(model.AccountId, ct);
            if (user is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Account not found.");
            }

            var postLockDemotion = IsActiveAdministratorDemotion(user.IsActive, user.RoleId == RoleCode.Admin, requestedRole);
            if (postLockDemotion && !preLockDemotion)
            {
                // The row changed between the pre-lock read and the row lock, so taking the
                // application lock now would break the documented lock order.
                throw new DomainException(OutcomeCode.Stale, "This account changed while the role was being assigned. Review it and try again.");
            }

            if (user.RoleId == requestedRole)
            {
                // A no-op succeeds without a duplicate change log.
                return;
            }

            if (postLockDemotion)
            {
                var activeAdministrators = await _accountAdministrationRepository.CountActiveAdministratorsAsync(ct);
                if (activeAdministrators <= 1)
                {
                    throw new DomainException(OutcomeCode.Stale, "The last active Administrator cannot be demoted.");
                }
            }

            // A missing role lookup is a data-integrity fault, not a user error: fail loudly rather
            // than writing a numeric fallback into the audit row.
            var oldRoleCode = await _userRepository.GetRoleCodeAsync(user.RoleId, ct)
                ?? throw new InvalidOperationException($"Role lookup {user.RoleId} is missing.");
            var newRoleCode = await _userRepository.GetRoleCodeAsync(requestedRole, ct)
                ?? throw new InvalidOperationException($"Role lookup {requestedRole} is missing.");

            user.RoleId = requestedRole;
            user.UpdatedById = actorUserId;
            user.UpdatedTime = _timeProvider.GetUtcNow().UtcDateTime;
            await _unitOfWork.SaveChangesAsync(ct);

            _activityLogWriter.Append(new AuditEntry(
                AuditActions.UserRoleAssigned,
                "users",
                user.Id,
                Reason: reason,
                OldValues: new RoleAuditValues(oldRoleCode),
                NewValues: new RoleAuditValues(newRoleCode)));

            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    private static bool IsActiveAdministratorDemotion(bool isActive, bool isAdministrator, RoleCode requestedRole) =>
        isActive && isAdministrator && requestedRole != RoleCode.Admin;

    private static RoleCode RequiredRole(string roleCode) =>
        roleCode?.Trim().ToLowerInvariant() switch
        {
            RoleCodes.Member => RoleCode.Member,
            RoleCodes.Officer => RoleCode.Officer,
            RoleCodes.Admin => RoleCode.Admin,
            _ => throw new DomainException(OutcomeCode.Invalid, "Select a valid role.", nameof(ChangeAccountRoleViewModel.RoleCode))
        };

    private sealed record RoleAuditValues(string RoleCode) : IAuditValues;
}
