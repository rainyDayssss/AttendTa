using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

// M-12: replaced by 6.9's owner query. Reviews are the real SQL rows; the store only overlays the
// still-demo issued document (M-11a/M-11b).
public sealed class DemoMyCertificatesService(
    IRegistrationRepository registrations, IEventRepository events, ICertificateRepository certificates,
    DemoStore store, DemoCertificateDocuments documents, ICurrentActor actor) : IMyCertificatesService
{
    public async Task<PagedResult<MyEventCertificateRowViewModel>> GetPageAsync(int? page, CancellationToken cancellationToken = default)
    {
        RequireMember();
        var eventIds = await registrations.DiscoverOpenEventIdsForUserAsync(actor.UserId, cancellationToken);
        var rows = new List<MyEventCertificateRowViewModel>();
        if (eventIds.Count > 0)
        {
            var count = await events.CountAsync(cancellationToken);
            var eventRows = await events.GetPageAsync(0, count, cancellationToken);
            foreach (var eventRow in eventRows.Where(row => eventIds.Contains(row.EventId)))
            {
                var registration = await registrations.GetMemberRegistrationAsync(eventRow.EventId, actor.UserId, cancellationToken);
                if (registration == null || registration.Status != RegistrationStatusCode.Confirmed) continue;
                var review = await certificates.GetByRegistrationIdAsync(registration.RegistrationId, cancellationToken);
                store.TryGetIssuedCertificate(registration.RegistrationId, out var issue);
                var state = review?.CertificateStatusId switch
                {
                    CertificateStatusCode.PendingApproval => MyCertificateState.Pending,
                    CertificateStatusCode.Eligible => MyCertificateState.Eligible,
                    CertificateStatusCode.NotEligible => MyCertificateState.Declined,
                    CertificateStatusCode.Issued => MyCertificateState.Issued,
                    _ => MyCertificateState.Unavailable
                };
                var hasDocument = state == MyCertificateState.Issued && issue?.Snapshot != null;
                if (state == MyCertificateState.Issued && !hasDocument) state = MyCertificateState.Unavailable;
                if (issue?.Snapshot != null)
                {
                    state = MyCertificateState.Issued;
                    hasDocument = true;
                }

                rows.Add(new MyEventCertificateRowViewModel
                {
                    CertificateId = registration.RegistrationId,
                    EventId = eventRow.EventId,
                    EventTitle = issue?.Snapshot?.EventTitle ?? eventRow.Title,
                    StartsAt = issue?.Snapshot?.EventStartsAt ?? eventRow.StartsAt,
                    State = state,
                    Reason = review?.ReviewNotes,
                    HasDocument = hasDocument,
                    CertificateNumber = issue?.CertificateNumber,
                    IssuedAt = issue?.IssuedAt
                });
            }
        }
        var ordered = rows.OrderByDescending(row => row.StartsAt).ThenByDescending(row => row.CertificateId).ToList();
        var request = new PageRequest(page);
        var normalized = request.NormalizePageNumber(ordered.Count);
        return new PagedResult<MyEventCertificateRowViewModel>(
            ordered.Skip(request.GetOffset(normalized)).Take(PageRequest.FixedPageSize).ToList(), normalized, ordered.Count);
    }

    public async Task<CertificateDocument> GetPdfAsync(int certificateId, CancellationToken cancellationToken = default)
    {
        RequireMember();
        // Reload the real owner for each request; ids and hidden/disabled links grant no access.
        var identity = await registrations.DiscoverReviewAsync(certificateId, cancellationToken);
        if (identity == null || identity.UserId != actor.UserId
            || !store.TryGetIssuedCertificate(certificateId, out var issue)
            || issue.EventId != identity.EventId || issue.Snapshot == null)
            throw new DomainException(OutcomeCode.NotFound, "Certificate not found.");
        return await documents.GetPdfAsync(certificateId, cancellationToken);
    }

    private void RequireMember()
    {
        if (!actor.IsAuthenticated || actor.UserId <= 0 || actor.Role != RoleCodes.Member)
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to view these certificates.");
    }
}
