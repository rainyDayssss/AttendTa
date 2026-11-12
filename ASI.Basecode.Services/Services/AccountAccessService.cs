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
/// Administrator-only account access commands. Deactivation runs in one transaction that takes the
/// <c>attendta:last-admin</c> application lock before the <c>users</c> row lock whenever the target
/// is an active Administrator, then cancels the account's upcoming registrations and records one
/// <c>user.deactivated</c> audit row. Reactivation runs in one transaction that locks the
/// <c>users</c> row, restores login access only, and records one <c>user.reactivated</c> audit row.
/// </summary>
public sealed class AccountAccessService : IAccountAccessCommands
{
    private readonly IAccountAdministrationRepository _accountAdministrationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRegistrationCancellationService _registrationCancellationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentActor _currentActor;
    private readonly IActivityLogWriter _activityLogWriter;

    public AccountAccessService(
        IAccountAdministrationRepository accountAdministrationRepository,
        IUserRepository userRepository,
        IRegistrationCancellationService registrationCancellationService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ICurrentActor currentActor,
        IActivityLogWriter activityLogWriter)
    {
        _accountAdministrationRepository = accountAdministrationRepository ?? throw new ArgumentNullException(nameof(accountAdministrationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _registrationCancellationService = registrationCancellationService ?? throw new ArgumentNullException(nameof(registrationCancellationService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _currentActor = currentActor ?? throw new ArgumentNullException(nameof(currentActor));
        _activityLogWriter = activityLogWriter ?? throw new ArgumentNullException(nameof(activityLogWriter));
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(int accountId, string reason, CancellationToken cancellationToken = default)
    {
        var actorUserId = AdministrationCommandGuards.RequireAdministrator(_currentActor);
        var normalizedReason = AdministrationCommandGuards.RequiredReason(reason, "reason");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var snapshot = await _accountAdministrationRepository.GetAccountAsync(accountId, ct);
            if (snapshot is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Account not found.");
            }

            if (!snapshot.IsActive)
            {
                throw new DomainException(OutcomeCode.Invalid, "This account is already inactive.");
            }

            // The pre-lock read only decides whether the application lock is needed. The
            // authoritative decision is made after the users row lock is held.
            var preLockAdministrator = string.Equals(snapshot.RoleCode, RoleCodes.Admin, StringComparison.Ordinal);
            if (preLockAdministrator)
            {
                await _accountAdministrationRepository.AcquireLastAdministratorLockAsync(ct);
            }

            var user = await _userRepository.LockUserAsync(accountId, ct);
            if (user is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Account not found.");
            }

            if (!user.IsActive)
            {
                throw new DomainException(OutcomeCode.Invalid, "This account is already inactive.");
            }

            var postLockAdministrator = user.RoleId == RoleCode.Admin;
            if (postLockAdministrator && !preLockAdministrator)
            {
                // The row changed between the pre-lock read and the row lock, so taking the
                // application lock now would break the documented lock order.
                throw new DomainException(OutcomeCode.Stale, "This account changed while it was being deactivated. Review it and try again.");
            }

            if (postLockAdministrator)
            {
                var activeAdministrators = await _accountAdministrationRepository.CountActiveAdministratorsAsync(ct);
                if (activeAdministrators <= 1)
                {
                    throw new DomainException(OutcomeCode.Stale, "The last active Administrator cannot be deactivated.");
                }
            }

            await _registrationCancellationService.CancelUpcomingForUserAsync(
                accountId, CancellationCause.AccountDeactivated, normalizedReason, ct);

            user.IsActive = false;
            user.UpdatedById = actorUserId;
            user.UpdatedTime = _timeProvider.GetUtcNow().UtcDateTime;
            await _unitOfWork.SaveChangesAsync(ct);

            _activityLogWriter.Append(new AuditEntry(
                AuditActions.UserDeactivated,
                "users",
                user.Id,
                Reason: normalizedReason,
                OldValues: new ActivationAuditValues(true),
                NewValues: new ActivationAuditValues(false)));

            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReactivateAsync(int accountId, string reason, CancellationToken cancellationToken = default)
    {
        var actorUserId = AdministrationCommandGuards.RequireAdministrator(_currentActor);
        var normalizedReason = AdministrationCommandGuards.RequiredReason(reason, "reason");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await _userRepository.LockUserAsync(accountId, ct);
            if (user is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Account not found.");
            }

            if (user.IsActive)
            {
                throw new DomainException(OutcomeCode.Invalid, "This account is already active.");
            }

            user.IsActive = true;
            user.UpdatedById = actorUserId;
            user.UpdatedTime = _timeProvider.GetUtcNow().UtcDateTime;
            await _unitOfWork.SaveChangesAsync(ct);

            _activityLogWriter.Append(new AuditEntry(
                AuditActions.UserReactivated,
                "users",
                user.Id,
                Reason: normalizedReason,
                OldValues: new ActivationAuditValues(false),
                NewValues: new ActivationAuditValues(true)));

            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    private sealed record ActivationAuditValues(bool IsActive) : IAuditValues;
}
