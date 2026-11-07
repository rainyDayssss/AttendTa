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

namespace ASI.Basecode.WebApp.DemoData;

// M-10a/M-10b/M-11a/M-11b: certificate surface adapters removed by 6.4/6.6/6.7/6.8 respectively.
public sealed class DemoCertificateLedgerService(
    IEventRepository events, IRegistrationRepository registrations, DemoStore store,
    DemoCertificateDocuments documents, ICurrentActor actor, TimeProvider clock) : ICertificateLedgerService
{
    public async Task<CertificateLedgerViewModel> GetPageAsync(int? eventId, CertificateLedgerTab tab, int? page,
        CancellationToken cancellationToken = default)
    {
        RequireManagement();
        var count = await events.CountAsync(cancellationToken);
        var allEvents = await events.GetPageAsync(0, count, cancellationToken);
        var completed = allEvents.Where(row => IsCompleted(row.EventId, row.EventStatusId))
            .OrderByDescending(row => row.StartsAt).ThenBy(row => row.EventId).ToList();
        var selected = eventId.HasValue ? completed.FirstOrDefault(row => row.EventId == eventId) : completed.FirstOrDefault();
        var rows = new List<CertificateLedgerRowViewModel>();
        if (selected != null)
        {
            store.EnsureCertificateReviews(selected.EventId, clock.GetUtcNow().UtcDateTime);
            var confirmed = await ReadRegistrationsAsync(selected.EventId, cancellationToken);
            foreach (var row in confirmed)
            {
                if (!store.TryGetReview(row.RegistrationId, out var review) || review.EventId != selected.EventId) continue;
                store.TryGetAttendance(row.RegistrationId, out var attendance);
                store.TryGetIssuedCertificate(row.RegistrationId, out var issue);
                var (label, badge) = review.Status switch
                {
                    CertificateStatusCode.PendingApproval => ("Pending review", "badge-pending"),
                    CertificateStatusCode.Eligible => ("Approved", "badge-approved"),
                    CertificateStatusCode.NotEligible => ("Not eligible", "badge-rejected"),
                    _ => ("Issued", "badge-completed")
                };
                rows.Add(new CertificateLedgerRowViewModel
                {
                    RegistrationId = row.RegistrationId,
                    MemberName = issue?.Snapshot?.RecipientName ?? row.MemberName,
                    StudentNumber = row.StudentNumber,
                    EventTitle = issue?.Snapshot?.EventTitle ?? selected.Title,
                    AttendedAt = attendance?.RecordedAt,
                    Status = review.Status,
                    StatusLabel = label,
                    StatusBadgeClass = badge,
                    Reason = review.Reason,
                    CertificateNumber = issue?.CertificateNumber,
                    IssuedAt = issue?.IssuedAt
                });
            }
        }
        var issuedCount = rows.Count(row => row.Status == CertificateStatusCode.Issued);
        var filtered = rows.Where(row => (row.Status == CertificateStatusCode.Issued) == (tab == CertificateLedgerTab.Issued))
            .OrderBy(row => row.MemberName, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.RegistrationId).ToList();
        var metadata = new PagedResult<CertificateLedgerRowViewModel>(Array.Empty<CertificateLedgerRowViewModel>(), page ?? 1, filtered.Count);
        var items = filtered.Skip((metadata.PageNumber - 1) * PageRequest.FixedPageSize).Take(PageRequest.FixedPageSize).ToList();
        return new CertificateLedgerViewModel
        {
            ActiveTab = tab,
            EligibilityCount = rows.Count - issuedCount,
            IssuedCount = issuedCount,
            Rows = items,
            Page = new PagedResult<CertificateLedgerRowViewModel>(items, metadata.PageNumber, filtered.Count),
            Selector = new EventSelectorViewModel
            {
                Label = "Completed event",
                SelectedEventId = selected?.EventId,
                Options = completed.Select(row => new EventSelectorOption(row.EventId, row.Title)).ToList(),
                HiddenValues = new Dictionary<string, string> { ["tab"] = tab == CertificateLedgerTab.Issued ? "issued" : "eligibility" }
            }
        };
    }

    public async Task DecideAsync(int registrationId, bool approve, string reason, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        await RequireCurrentParticipationAsync(registrationId, cancellationToken);
        store.DecideCertificate(registrationId, approve, reason, actor.UserId, clock.GetUtcNow().UtcDateTime);
    }

    public async Task IssueAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        var eventItem = await RequireCurrentParticipationAsync(registrationId, cancellationToken);
        if (store.IsCertificateIssued(registrationId))
            throw new DomainException(OutcomeCode.Duplicate, "A certificate has already been issued.");
        if (!store.TryGetReview(registrationId, out var review) || review.Status != CertificateStatusCode.Eligible)
            throw new DomainException(OutcomeCode.Invalid, "Approve this review before issuing a certificate.");
        var rows = await ReadRegistrationsAsync(eventItem.EventId, cancellationToken);
        var registration = rows.FirstOrDefault(row => row.RegistrationId == registrationId)
            ?? throw new DomainException(OutcomeCode.NotFound, "Certificate review not found.");
        var issuedAt = clock.GetUtcNow().UtcDateTime;
        var snapshot = new CertificatePdfSnapshot(registration.MemberName, eventItem.Title, eventItem.StartsAt,
            issuedAt, store.GetCertificateNumber(registrationId, issuedAt));
        var bytes = await documents.RenderAsync(snapshot, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await RequireCurrentParticipationAsync(registrationId, cancellationToken);
        store.PublishCertificate(registrationId, snapshot, bytes, actor.UserId);
    }

    public Task<CertificateViewerViewModel> GetViewerAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        cancellationToken.ThrowIfCancellationRequested();
        var issue = RequireIssue(registrationId);
        return Task.FromResult(new CertificateViewerViewModel
        {
            CertificateId = registrationId,
            EventId = issue.EventId,
            MemberName = issue.Snapshot.RecipientName,
            EventTitle = issue.Snapshot.EventTitle,
            CertificateNumber = issue.CertificateNumber
        });
    }

    public Task<CertificateDocument> GetPdfAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        return documents.GetPdfAsync(registrationId, cancellationToken);
    }

    private DemoCertificateIssue RequireIssue(int registrationId)
    {
        if (!store.TryGetIssuedCertificate(registrationId, out var issue) || issue.Snapshot == null)
            throw new DomainException(OutcomeCode.NotFound, "Certificate not found.");
        return issue;
    }

    private async Task<Event> RequireCurrentParticipationAsync(int registrationId, CancellationToken cancellationToken)
    {
        var identity = await registrations.DiscoverReviewAsync(registrationId, cancellationToken);
        if (identity == null || !await registrations.IsConfirmedAsync(registrationId, cancellationToken))
            throw new DomainException(OutcomeCode.Invalid, "A confirmed registration is required for a certificate.");
        var eventItem = await events.GetEventAsync(identity.EventId, cancellationToken);
        if (eventItem == null || !IsCompleted(eventItem.EventId, eventItem.EventStatusId))
            throw new DomainException(OutcomeCode.Invalid, "Certificates are available only for completed events.");
        if (!store.TryGetAttendance(registrationId, out var attendance) || !attendance.IsValid || attendance.EventId != eventItem.EventId)
            throw new DomainException(OutcomeCode.Invalid, "Valid attendance is required for a certificate.");
        if (!store.TryGetReview(registrationId, out var review) || review.EventId != eventItem.EventId)
            throw new DomainException(OutcomeCode.NotFound, "Certificate review not found.");
        return eventItem;
    }

    private bool IsCompleted(int eventId, EventStatusCode status) => status == EventStatusCode.Completed
        || (status == EventStatusCode.Published && store.IsCompleted(eventId));

    private async Task<IReadOnlyList<RegistrationReviewRow>> ReadRegistrationsAsync(int eventId, CancellationToken cancellationToken)
    {
        var count = await registrations.CountReviewQueueAsync(RegistrationStatusCode.Confirmed, eventId, cancellationToken);
        return await registrations.GetReviewQueueAsync(RegistrationStatusCode.Confirmed, eventId, 0, count, cancellationToken);
    }

    private void RequireManagement()
    {
        if (!actor.IsAuthenticated || actor.UserId <= 0 || actor.Role is not (RoleCodes.Admin or RoleCodes.Officer))
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to manage certificates.");
    }
}
