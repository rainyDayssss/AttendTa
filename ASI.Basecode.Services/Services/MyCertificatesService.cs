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
/// document only when it is Issued and its retained PDF is readable. Missing documents do not change
/// the recorded issuance state. Personal PDF reads check ownership before delegating to the ledger.
/// </summary>
public sealed class MyCertificatesService(
    IRegistrationRepository registrations,
    IEventRepository events,
    ICertificateRepository certificates,
    ICertificateLedgerService ledger,
    ICurrentActor actor, IPrivateFileStore files, ListReadOptions readOptions = null) : IMyCertificatesService
{
    public async Task<PagedResult<MyEventCertificateRowViewModel>> GetPageAsync(
        int? page,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireActor();

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
            var hasDocument = state == MyCertificateState.Issued
                && await CertificateFileAvailability.IsAvailableAsync(files, review?.FilePath, cancellationToken);

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
            .Where(row => string.IsNullOrWhiteSpace(readOptions?.Search) || row.EventTitle.Contains(readOptions.Search, StringComparison.OrdinalIgnoreCase))
            .Where(row => string.IsNullOrWhiteSpace(readOptions?.Filter) || string.Equals(row.State.ToString(), readOptions.Filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(row => row.StartsAt)
            .ThenByDescending(row => row.CertificateId)
            .ToList();
        var request = new PageRequest(page, readOptions?.PageSize);
        var normalized = request.NormalizePageNumber(ordered.Count);
        return new PagedResult<MyEventCertificateRowViewModel>(
            ordered.Skip(request.GetOffset(normalized)).Take(PageRequest.NormalizeSize(readOptions?.PageSize)).ToList(),
            normalized,
            ordered.Count, PageRequest.NormalizeSize(readOptions?.PageSize));
    }

    /// <summary>
    /// Check ownership independently before calling the management-capable ledger.
    /// </summary>
    public async Task<CertificateDocument> GetPdfAsync(int certificateId, CancellationToken cancellationToken = default)
    {
        var userId = RequireActor();
        var identity = certificateId > 0 ? await registrations.DiscoverIdentityAsync(certificateId, cancellationToken) : null;
        if (identity is null || identity.UserId != userId)
            throw new DomainException(OutcomeCode.NotFound, "Certificate not found.");
        return await ledger.GetPdfAsync(certificateId, cancellationToken);
    }

    private int RequireActor()
    {
        if (!actor.IsAuthenticated)
        {
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to view these certificates.");
        }

        int userId;
        try
        {
            userId = actor.UserId;
        }
        catch (InvalidOperationException)
        {
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to view these certificates.");
        }

        if (userId <= 0)
        {
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to view these certificates.");
        }

        return userId;
    }
}
