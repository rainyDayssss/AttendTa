using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

public sealed class RegistrationCancellationService(IRegistrationRepository registrations, IEventRepository events,
    IUnitOfWork unitOfWork, ICurrentActor actor, IActivityLogWriter audit, TimeProvider clock) : IRegistrationCancellationService
{
    public async Task CancelRegistrationAsync(int registrationId, CancellationCause cause, string reason, CancellationToken cancellationToken = default)
    {
        var actorId = Validate(cause, reason);
        var row = await registrations.GetForCancellationAsync(registrationId, cancellationToken);
        if (row != null) Cancel(row, cause, reason, actorId, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAllForEventAsync(int eventId, CancellationCause cause, string reason, CancellationToken cancellationToken = default)
    {
        var actorId = Validate(cause, reason);
        var rows = await registrations.GetOpenForEventAsync(eventId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var row in rows) Cancel(row, cause, reason, actorId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelUpcomingForUserAsync(int userId, CancellationCause cause, string reason, CancellationToken cancellationToken = default)
    {
        var actorId = Validate(cause, reason);
        var ids = await registrations.DiscoverOpenEventIdsForUserAsync(userId, cancellationToken);
        foreach (var eventId in ids.Distinct().OrderBy(id => id))
        {
            if (await events.LockEventAsync(eventId, cancellationToken) == null) continue;
            var now = clock.GetUtcNow().UtcDateTime;
            var rows = await registrations.GetUpcomingForUserAsync(userId, now, eventId, cancellationToken);
            foreach (var candidate in rows)
            {
                var row = await registrations.GetForCancellationAsync(candidate.RegistrationId, cancellationToken);
                if (row != null) Cancel(row, cause, reason, actorId, now);
            }
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UpcomingRegistrationPreview>> PreviewUpcomingForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await registrations.GetUpcomingForUserAsync(userId, clock.GetUtcNow().UtcDateTime, null, cancellationToken);
        return rows.Select(row => new UpcomingRegistrationPreview(row.RegistrationId, row.EventId, row.EventTitle, row.StartsAt)).ToList();
    }

    private int Validate(CancellationCause cause, string reason)
    {
        if (unitOfWork.Database.Database.CurrentTransaction == null) throw new InvalidOperationException("Cancellation requires an active transaction.");
        if (!actor.IsAuthenticated || actor.UserId <= 0) throw new InvalidOperationException("Cancellation requires an authenticated user.");
        if (!Enum.IsDefined(cause)) throw new DomainException(OutcomeCode.Invalid, "Invalid cancellation cause.");
        if (cause != CancellationCause.MemberRequested && string.IsNullOrWhiteSpace(reason)) throw new DomainException(OutcomeCode.Invalid, "A cancellation reason is required.");
        return actor.UserId;
    }

    private void Cancel(Registration row, CancellationCause cause, string reason, int actorId, DateTime now)
    {
        if (row.RegistrationStatusId is not (RegistrationStatusCode.Pending or RegistrationStatusCode.Confirmed or RegistrationStatusCode.Waitlisted)) return;
        var oldStatus = row.RegistrationStatusId;
        var storedReason = string.IsNullOrWhiteSpace(reason) ? "Member requested cancellation" : reason.Trim();
        row.RegistrationStatusId = RegistrationStatusCode.Cancelled;
        row.QrToken = null;
        row.CancelledBy = actorId;
        row.CancelledAt = now;
        row.CancellationReason = storedReason;
        row.UpdatedBy = actorId;
        row.UpdatedAt = now;
        audit.Append(new AuditEntry(AuditActions.RegistrationCancelled, "registrations", row.RegistrationId, storedReason,
            new StatusValues(oldStatus.ToString(), cause.ToString()), new StatusValues(RegistrationStatusCode.Cancelled.ToString(), cause.ToString())));
    }

    private sealed record StatusValues(string Status, string Cause) : IAuditValues;
}
