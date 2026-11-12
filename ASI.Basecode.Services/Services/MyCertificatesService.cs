using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// The real member certificate history (R2, 6.9). The page is owner-scoped: the actor's own
/// non-cancelled event ids, each event's own confirmed registration, and that registration's
/// certificate row decide the state, so another member's record never appears. A row carries a
/// document only when it is Issued and the row has a retained <c>file_path</c>; the seeded issued row
/// has none and stays a controlled not-found. The PDF read adds the member guard and then delegates to
/// the ledger's authorized retained-file retrieval (6.8), which rechecks ownership on every request.
/// </summary>
public sealed class MyCertificatesService(
    IRegistrationRepository registrations,
    IEventRepository events,
    ICertificateRepository certificates,
    ICertificateLedgerService ledger,
    ICurrentActor actor) : IMyCertificatesService
{
    public async Task<PagedResult<MyEventCertificateRowViewModel>> GetPageAsync(
        int? page,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireMember();

        var eventIds = await registrations.DiscoverOpenEventIdsForUserAsync(userId, cancellationToken);
        var rows = new List<MyEventCertificateRowViewModel>();
        foreach (var eventId in eventIds)
        {
            // One event read per owned event keeps the page bounded by the member's registrations
            // instead of loading the whole event table.
            var eventItem = await events.GetEventAsync(eventId, cancellationToken);
            if (eventItem is null || eventItem.EventStatusId == EventStatusCode.Cancelled)
            {
                continue;
            }

            var registration = await registrations.GetMemberRegistrationAsync(eventId, userId, cancellationToken);
            if (registration is null || !ParticipationFacts.HoldsSeat(registration.Status))
            {
                continue;
            }

            var review = await certificates.GetByRegistrationIdNoTrackingAsync(registration.RegistrationId, cancellationToken);
            var state = review?.CertificateStatusId switch
            {
                CertificateStatusCode.PendingApproval => MyCertificateState.Pending,
                CertificateStatusCode.Eligible => MyCertificateState.Eligible,
                CertificateStatusCode.NotEligible => MyCertificateState.Declined,
                CertificateStatusCode.Issued => MyCertificateState.Issued,
                _ => MyCertificateState.Unavailable
            };
            var hasDocument = state == MyCertificateState.Issued && !string.IsNullOrWhiteSpace(review?.FilePath);
            if (state == MyCertificateState.Issued && !hasDocument)
            {
                state = MyCertificateState.Unavailable;
            }

            rows.Add(new MyEventCertificateRowViewModel
            {
                // The row's CertificateId is the registration id, not certificates.certificate_id:
                // it is the route value the PDF read expects, and the ledger resolves the row from it.
                CertificateId = registration.RegistrationId,
                EventId = eventItem.EventId,
                EventTitle = review?.EventTitleSnapshot ?? eventItem.Title,
                StartsAt = review?.EventStartsAtSnapshot ?? eventItem.StartsAt,
                State = state,
                Reason = review?.ReviewNotes,
                HasDocument = hasDocument,
                CertificateNumber = review?.CertificateNumber,
                IssuedAt = review?.IssuedAt
            });
        }

        var ordered = rows
            .OrderByDescending(row => row.StartsAt)
            .ThenByDescending(row => row.CertificateId)
            .ToList();
        var request = new PageRequest(page);
        var normalized = request.NormalizePageNumber(ordered.Count);
        return new PagedResult<MyEventCertificateRowViewModel>(
            ordered.Skip(request.GetOffset(normalized)).Take(PageRequest.FixedPageSize).ToList(),
            normalized,
            ordered.Count);
    }

    /// <summary>
    /// The member guard is the only addition here: the ledger re-resolves the registration's owner and
    /// requires an issued row with a retained file, so another member's id and an unknown id both
    /// surface the identical not-found.
    /// </summary>
    public async Task<CertificateDocument> GetPdfAsync(int certificateId, CancellationToken cancellationToken = default)
    {
        RequireMember();
        return await ledger.GetPdfAsync(certificateId, cancellationToken);
    }

    private int RequireMember()
    {
        if (!actor.IsAuthenticated)
        {
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to view these certificates.");
        }

        int userId;
        string role;
        try
        {
            userId = actor.UserId;
            role = actor.Role;
        }
        catch (InvalidOperationException)
        {
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to view these certificates.");
        }

        if (userId <= 0 || !string.Equals(role, RoleCodes.Member, StringComparison.Ordinal))
        {
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to view these certificates.");
        }

        return userId;
    }
}
