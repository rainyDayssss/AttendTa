using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
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

    public Task ApproveAsync(int id, CancellationToken ct = default) => ExecuteAsync(id, null, 0, ct);
    public Task DeclineAsync(int id, string reason, CancellationToken ct = default) => ExecuteAsync(id, reason, 1, ct);
    public Task CancelAsync(int id, string reason, CancellationToken ct = default) => ExecuteAsync(id, reason, 2, ct);

    public async Task PromoteNextAsync(int eventId, CancellationToken ct = default)
    {
        var actorId = RequireManagement();
        var candidate = await registrations.DiscoverPromotionCandidateAsync(eventId, ct);
        if (candidate == null) throw new DomainException(OutcomeCode.Invalid, "No eligible waitlisted registration to promote.");
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var user = await users.LockUserAsync(candidate.UserId, token);
            var member = await members.GetMemberByUserIdAsync(candidate.UserId, token);
            var eventItem = await events.LockEventAsync(candidate.EventId, token);
            var now = clock.GetUtcNow().UtcDateTime;
            var current = await registrations.DiscoverPromotionCandidateAsync(candidate.EventId, token);
            var row = await registrations.GetForCancellationAsync(candidate.RegistrationId, token);
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

    private async Task ExecuteAsync(int id, string reason, int operation, CancellationToken ct)
    {
        var actorId = RequireManagement();
        var identity = await registrations.DiscoverReviewAsync(id, ct);
        if (identity == null) throw new DomainException(OutcomeCode.NotFound, "Registration not found.");
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var user = await users.LockUserAsync(identity.UserId, token);
            var member = await members.GetMemberByUserIdAsync(identity.UserId, token);
            var eventItem = await events.LockEventAsync(identity.EventId, token);
            var now = clock.GetUtcNow().UtcDateTime;
            var current = await registrations.DiscoverReviewAsync(id, token);
            var row = await registrations.GetForCancellationAsync(id, token);
            if (current != identity || row == null || row.MemberId != identity.MemberId || row.EventId != identity.EventId || user == null || member == null || member.MemberId != identity.MemberId || eventItem == null)
                throw new DomainException(OutcomeCode.Stale, "Registration changed. Refresh and try again.");
            if (operation != 2 && row.RegistrationStatusId != RegistrationStatusCode.Pending || operation == 2 && row.RegistrationStatusId is not (RegistrationStatusCode.Confirmed or RegistrationStatusCode.Waitlisted))
                throw new DomainException(OutcomeCode.Stale, "Registration status changed. Refresh and try again.");
            if (operation != 0 && string.IsNullOrWhiteSpace(reason))
                throw new DomainException(OutcomeCode.Invalid, "A reason is required.");
            if (eventItem.EventStatusId != EventStatusCode.Published)
                throw new DomainException(OutcomeCode.Stale, "Event status changed. Refresh and try again.");
            if (ParticipationFacts.StartCutoffPassed(now).Compile()(eventItem))
                throw new DomainException(OutcomeCode.CutoffPassed, "This event has already started.");
            if (operation == 0)
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
                if (operation == 1) { row.ReviewedBy = actorId; row.ReviewedAt = now; row.ReviewNotes = reason.Trim(); }
                await cancellation.CancelRegistrationAsync(id, operation == 1 ? CancellationCause.Declined : CancellationCause.AdminCancelled, reason.Trim(), token);
                audit.Append(new AuditEntry(operation == 1 ? AuditActions.RegistrationDeclined : AuditActions.RegistrationAdminCancelled,
                    "registrations", id, reason.Trim(), new StatusValues(oldStatus.ToString()), new StatusValues(RegistrationStatusCode.Cancelled.ToString())));
            }
            await unitOfWork.SaveChangesAsync(token);
        }, ct);
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
