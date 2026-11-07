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
/// Management membership access commands. Deactivation runs in one transaction that locks the
/// member's <c>users</c> row, marks <c>members.membership_status_id</c> inactive, cancels the
/// member's upcoming registrations through the shared cancellation service and records one
/// <c>member.membership_deactivated</c> audit row. Reactivation runs in one transaction that locks
/// the <c>users</c> row, restores eligibility only and records one
/// <c>member.membership_reactivated</c> audit row. Neither takes the last-administrator application
/// lock or changes <c>users.is_active</c>: membership eligibility is separate from login access.
/// </summary>
public sealed class MembershipAccessService : IMembershipAccessCommands
{
    private const int ReasonMaxLength = 1000;

    private readonly IMemberRepository _memberRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRegistrationCancellationService _registrationCancellationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentActor _currentActor;
    private readonly IActivityLogWriter _activityLogWriter;

    public MembershipAccessService(
        IMemberRepository memberRepository,
        IUserRepository userRepository,
        IRegistrationCancellationService registrationCancellationService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ICurrentActor currentActor,
        IActivityLogWriter activityLogWriter)
    {
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _registrationCancellationService = registrationCancellationService ?? throw new ArgumentNullException(nameof(registrationCancellationService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _currentActor = currentActor ?? throw new ArgumentNullException(nameof(currentActor));
        _activityLogWriter = activityLogWriter ?? throw new ArgumentNullException(nameof(activityLogWriter));
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(int memberId, string reason, CancellationToken cancellationToken = default)
    {
        var actorUserId = RequireManagementActor();
        var normalizedReason = RequiredReason(reason);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var memberRecord = _memberRepository.GetMemberRecord(memberId);
            if (memberRecord is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Member not found.");
            }

            // AD-4 makes the users row the serialization point that also protects the member row.
            var user = await _userRepository.LockUserAsync(memberRecord.UserId, ct);
            if (user is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Member not found.");
            }

            var member = _memberRepository.GetMember(memberId);
            if (member is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Member not found.");
            }

            if (member.MembershipStatusId == MembershipStatusCode.Inactive)
            {
                throw new DomainException(OutcomeCode.Invalid, "This membership is already inactive.");
            }

            await _registrationCancellationService.CancelUpcomingForUserAsync(
                member.UserId, CancellationCause.MembershipDeactivated, normalizedReason, ct);

            var previousStatus = member.MembershipStatusId;
            member.MembershipStatusId = MembershipStatusCode.Inactive;
            member.UpdatedBy = actorUserId;
            member.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _memberRepository.UpdateMemberAsync(member, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            _activityLogWriter.Append(new AuditEntry(
                AuditActions.MemberMembershipDeactivated,
                "members",
                member.MemberId,
                Reason: normalizedReason,
                OldValues: new MembershipStatusAuditValues(previousStatus.ToString()),
                NewValues: new MembershipStatusAuditValues(MembershipStatusCode.Inactive.ToString())));

            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReactivateAsync(int memberId, string reason, CancellationToken cancellationToken = default)
    {
        var actorUserId = RequireManagementActor();
        var normalizedReason = RequiredReason(reason);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var memberRecord = _memberRepository.GetMemberRecord(memberId);
            if (memberRecord is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Member not found.");
            }

            // AD-4 makes the users row the serialization point that also protects the member row.
            var user = await _userRepository.LockUserAsync(memberRecord.UserId, ct);
            if (user is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Member not found.");
            }

            var member = _memberRepository.GetMember(memberId);
            if (member is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Member not found.");
            }

            if (member.MembershipStatusId == MembershipStatusCode.Active)
            {
                throw new DomainException(OutcomeCode.Invalid, "This membership is already active.");
            }

            var previousStatus = member.MembershipStatusId;
            member.MembershipStatusId = MembershipStatusCode.Active;
            member.UpdatedBy = actorUserId;
            member.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _memberRepository.UpdateMemberAsync(member, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            _activityLogWriter.Append(new AuditEntry(
                AuditActions.MemberMembershipReactivated,
                "members",
                member.MemberId,
                Reason: normalizedReason,
                OldValues: new MembershipStatusAuditValues(previousStatus.ToString()),
                NewValues: new MembershipStatusAuditValues(MembershipStatusCode.Active.ToString())));

            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    private static string RequiredReason(string reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException(OutcomeCode.Invalid, "A reason is required.", "reason");
        }

        if (normalized.Length > ReasonMaxLength)
        {
            throw new DomainException(OutcomeCode.Invalid, "The reason must be 1000 characters or fewer.", "reason");
        }

        return normalized;
    }

    private int RequireManagementActor()
    {
        if (!_currentActor.IsAuthenticated)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        int userId;
        try
        {
            userId = _currentActor.UserId;
        }
        catch (InvalidOperationException)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        if (userId <= 0
            || (!string.Equals(_currentActor.Role, RoleCodes.Admin, StringComparison.Ordinal)
                && !string.Equals(_currentActor.Role, RoleCodes.Officer, StringComparison.Ordinal)))
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        return userId;
    }

    private sealed record MembershipStatusAuditValues(string Status) : IAuditValues;
}
