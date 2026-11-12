using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

public sealed class RegistrationReviewService(IRegistrationRepository registrations, IUserRepository users,
    IMemberRepository members, IEventRepository events, IUnitOfWork unitOfWork, ICurrentActor actor,
    IRegistrationCancellationService cancellation, IActivityLogWriter audit, TimeProvider clock) : IRegistrationReviewService
{
    public async Task<PagedResult<RegistrationReviewRow>> GetPageAsync(string status, int? eventId, int? page, CancellationToken ct = default)
    {
        RequireManagement();
        var parsed = ParseStatus(status);
        var count = await registrations.CountReviewQueueAsync(parsed, eventId, ct);
        var request = new PageRequest(page);
        var normalized = request.NormalizePageNumber(count);
        var rows = await registrations.GetReviewQueueAsync(parsed, eventId, request.GetOffset(normalized), PageRequest.FixedPageSize, ct);
        return new PagedResult<RegistrationReviewRow>(rows, normalized, count);
    }

    public Task ApproveAsync(int id, CancellationToken ct = default) => ExecuteAsync(id, null, ReviewOperation.Approve, ct);
    public Task DeclineAsync(int id, string reason, CancellationToken ct = default) => ExecuteAsync(id, reason, ReviewOperation.Decline, ct);
    public Task CancelAsync(int id, string reason, CancellationToken ct = default) => ExecuteAsync(id, reason, ReviewOperation.Cancel, ct);

    public async Task PromoteNextAsync(int eventId, CancellationToken ct = default)
    {
        var actorId = RequireManagement();
        var candidate = await registrations.DiscoverPromotionCandidateAsync(eventId, ct);
        if (candidate == null) throw new DomainException(OutcomeCode.Invalid, "No eligible waitlisted registration to promote.");
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var (user, member, eventItem) = await LockOwnerAndEventAsync(candidate.UserId, candidate.EventId, token);
            var now = clock.GetUtcNow().UtcDateTime;
            var current = await registrations.DiscoverPromotionCandidateAsync(candidate.EventId, token);
            var row = await registrations.GetForUpdateAsync(candidate.RegistrationId, token);
            if (current != candidate || row == null || row.MemberId != candidate.MemberId || row.EventId != candidate.EventId
                || user == null || member == null || member.MemberId != candidate.MemberId || eventItem == null)
                throw new DomainException(OutcomeCode.Stale, "The waitlist changed. Refresh and try again.");
            if (row.RegistrationStatusId != RegistrationStatusCode.Waitlisted)
                throw new DomainException(OutcomeCode.Stale, "Registration status changed. Refresh and try again.");
            if (eventItem.EventStatusId != EventStatusCode.Published)
                throw new DomainException(OutcomeCode.Stale, "Event status changed. Refresh and try again.");
            if (ParticipationFacts.StartCutoffPassed(now).Compile()(eventItem))
                throw new DomainException(OutcomeCode.CutoffPassed, "This event has already started.");
            if (!ParticipationFacts.EligibleParticipant.Compile()(user, member))
                throw new DomainException(OutcomeCode.Stale, "Member eligibility changed. Refresh and try again.");
            if (await registrations.CountConfirmedAsync(candidate.EventId, token) >= eventItem.ParticipantLimit)
                throw new DomainException(OutcomeCode.CapacityUnavailable, "No seat is available to promote.");
            row.RegistrationStatusId = RegistrationStatusCode.Confirmed;
            row.ConfirmedBy = actorId;
            row.ConfirmedAt = now;
            row.QrToken = NewQrToken();
            row.UpdatedBy = actorId;
            row.UpdatedAt = now;
            audit.Append(new AuditEntry(AuditActions.RegistrationPromoted, "registrations", candidate.RegistrationId, null,
                new StatusValues(RegistrationStatusCode.Waitlisted.ToString()), new StatusValues(RegistrationStatusCode.Confirmed.ToString())));
            await unitOfWork.SaveChangesAsync(token);
        }, ct);
    }

    private async Task ExecuteAsync(int id, string reason, ReviewOperation operation, CancellationToken ct)
    {
        var actorId = RequireManagement();
        var identity = await registrations.DiscoverIdentityAsync(id, ct);
        if (identity == null) throw new DomainException(OutcomeCode.NotFound, "Registration not found.");
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var (user, member, eventItem) = await LockOwnerAndEventAsync(identity.UserId, identity.EventId, token);
            var now = clock.GetUtcNow().UtcDateTime;
            var current = await registrations.DiscoverIdentityAsync(id, token);
            var row = await registrations.GetForUpdateAsync(id, token);
            if (current != identity || row == null || row.MemberId != identity.MemberId || row.EventId != identity.EventId || user == null || member == null || member.MemberId != identity.MemberId || eventItem == null)
                throw new DomainException(OutcomeCode.Stale, "Registration changed. Refresh and try again.");
            if ((operation != ReviewOperation.Cancel && row.RegistrationStatusId != RegistrationStatusCode.Pending)
                || (operation == ReviewOperation.Cancel && row.RegistrationStatusId is not (RegistrationStatusCode.Confirmed or RegistrationStatusCode.Waitlisted)))
                throw new DomainException(OutcomeCode.Stale, "Registration status changed. Refresh and try again.");
            if (operation != ReviewOperation.Approve && string.IsNullOrWhiteSpace(reason))
                throw new DomainException(OutcomeCode.Invalid, "A reason is required.");
            if (eventItem.EventStatusId != EventStatusCode.Published)
                throw new DomainException(OutcomeCode.Stale, "Event status changed. Refresh and try again.");
            if (ParticipationFacts.StartCutoffPassed(now).Compile()(eventItem))
                throw new DomainException(OutcomeCode.CutoffPassed, "This event has already started.");
            if (operation == ReviewOperation.Approve)
            {
                if (!ParticipationFacts.EligibleParticipant.Compile()(user, member))
                    throw new DomainException(OutcomeCode.Stale, "Member eligibility changed. Refresh and try again.");
                var seatAvailable = await registrations.CountConfirmedAsync(identity.EventId, token) < eventItem.ParticipantLimit;
                var waitlistHasPriority = await registrations.HasEligibleWaitlistAsync(identity.EventId, token);
                row.RegistrationStatusId = seatAvailable && !waitlistHasPriority ? RegistrationStatusCode.Confirmed : RegistrationStatusCode.Waitlisted;
                row.ReviewedBy = actorId;
                row.ReviewedAt = now;
                row.UpdatedBy = actorId;
                row.UpdatedAt = now;
                if (row.RegistrationStatusId == RegistrationStatusCode.Confirmed)
                {
                    row.ConfirmedBy = actorId;
                    row.ConfirmedAt = now;
                    row.QrToken = NewQrToken();
                }
                audit.Append(new AuditEntry(AuditActions.RegistrationApproved, "registrations", id, null,
                    new StatusValues(RegistrationStatusCode.Pending.ToString()), new StatusValues(row.RegistrationStatusId.ToString())));
            }
            else
            {
                if (await registrations.HasValidAttendanceAsync(id, token))
                    throw new DomainException(OutcomeCode.Stale, "A registration with valid attendance cannot be cancelled.");
                var oldStatus = row.RegistrationStatusId;
                if (operation == ReviewOperation.Decline) { row.ReviewedBy = actorId; row.ReviewedAt = now; row.ReviewNotes = reason.Trim(); }
                await cancellation.CancelRegistrationAsync(id, operation == ReviewOperation.Decline ? CancellationCause.Declined : CancellationCause.AdminCancelled, reason.Trim(), token);
                audit.Append(new AuditEntry(operation == ReviewOperation.Decline ? AuditActions.RegistrationDeclined : AuditActions.RegistrationAdminCancelled,
                    "registrations", id, reason.Trim(), new StatusValues(oldStatus.ToString()), new StatusValues(RegistrationStatusCode.Cancelled.ToString())));
            }
            await unitOfWork.SaveChangesAsync(token);
        }, ct);
    }

    /// <summary>
    /// Locks the owner's users row, then the event row — AD-4 order, user before event — so
    /// seat allocation and cancellation for that owner serialize against competing reviews.
    /// The locked rows may be missing; the caller rechecks and reports <c>Stale</c>.
    /// </summary>
    private async Task<(User User, Member Member, Event Event)> LockOwnerAndEventAsync(int userId, int eventId, CancellationToken ct)
    {
        var user = await users.LockUserAsync(userId, ct);
        var member = await members.GetMemberByUserIdAsync(userId, ct);
        var eventItem = await events.LockEventAsync(eventId, ct);
        return (user, member, eventItem);
    }

    private enum ReviewOperation
    {
        Approve,
        Decline,
        Cancel
    }

    private int RequireManagement()
    {
        if (!actor.IsAuthenticated || (actor.Role != RoleCodes.Admin && actor.Role != RoleCodes.Officer) || actor.UserId <= 0)
            throw new DomainException(OutcomeCode.Forbidden, "Management access is required.");
        return actor.UserId;
    }

    /// <summary>32 random bytes as 64 lowercase hex characters (AD-4 data shape).</summary>
    private static string NewQrToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public static RegistrationStatusCode ParseStatus(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "confirmed" => RegistrationStatusCode.Confirmed,
        "waitlisted" => RegistrationStatusCode.Waitlisted,
        "cancelled" => RegistrationStatusCode.Cancelled,
        _ => RegistrationStatusCode.Pending
    };

    private sealed record StatusValues(string Status) : IAuditValues;
}
