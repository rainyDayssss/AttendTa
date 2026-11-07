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
using ASI.Basecode.Services.Services;

namespace ASI.Basecode.WebApp.DemoData;

// M-10b removed by 6.6: the queue and both decisions are the real CertificateLedgerService. This
// provider only overlays the still-demo issued state (M-11a) and keeps issuance/viewer (M-11a/M-11b).
public sealed class DemoCertificateLedgerService(
    CertificateLedgerService inner,
    IEventRepository events,
    IRegistrationRepository registrations,
    IAttendanceRepository attendance,
    ICertificateRepository certificates,
    IUnitOfWork unitOfWork,
    DemoStore store,
    DemoCertificateDocuments documents,
    ICurrentActor actor,
    TimeProvider clock) : ICertificateLedgerService
{
    public async Task<CertificateLedgerViewModel> GetPageAsync(
        int? eventId,
        CertificateLedgerTab tab,
        int? page,
        CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(eventId, cancellationToken);
        var rows = OverlayIssues(read.Rows);
        return CertificateLedgerService.BuildPage(new CertificateLedgerRead(read.Selector, rows), tab, page);
    }

    public Task DecideAsync(
        int registrationId,
        bool approve,
        string reason,
        CancellationToken cancellationToken = default) =>
        inner.DecideAsync(registrationId, approve, reason, cancellationToken);

    public async Task IssueAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        var current = await RequireCurrentParticipationAsync(registrationId, cancellationToken);
        if (store.IsCertificateIssued(registrationId))
            throw new DomainException(OutcomeCode.Duplicate, "A certificate has already been issued.");
        if (current.Status != CertificateStatusCode.Eligible)
            throw new DomainException(OutcomeCode.Invalid, "Approve this review before issuing a certificate.");

        var issuedAt = clock.GetUtcNow().UtcDateTime;
        var snapshot = new CertificatePdfSnapshot(
            current.MemberName,
            current.Event.Title,
            current.Event.StartsAt,
            issuedAt,
            store.GetCertificateNumber(registrationId, issuedAt));
        var bytes = await documents.RenderAsync(snapshot, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // The review and attendance may have changed while the PDF rendered; recheck before publish.
        var recheck = await RequireCurrentParticipationAsync(registrationId, cancellationToken);
        if (recheck.Status != CertificateStatusCode.Eligible)
            throw new DomainException(OutcomeCode.Invalid, "Approve this review before issuing a certificate.");
        store.PublishCertificate(registrationId, current.Event.EventId, snapshot, bytes, actor.UserId);

        // The real invalidation handler refuses an issued certificate, so the SQL review must carry
        // the issued state too; the store keeps only the still-demo document.
        var certificate = await certificates.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (certificate is not null)
        {
            certificate.CertificateStatusId = CertificateStatusCode.Issued;
            certificate.CertificateNumber = snapshot.CertificateNumber;
            certificate.RecipientNameSnapshot = snapshot.RecipientName;
            certificate.EventTitleSnapshot = snapshot.EventTitle;
            certificate.EventStartsAtSnapshot = snapshot.EventStartsAt;
            certificate.IssuedBy = actor.UserId;
            certificate.IssuedAt = issuedAt;
            certificate.UpdatedBy = actor.UserId;
            certificate.UpdatedAt = issuedAt;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
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

    private IReadOnlyList<CertificateLedgerRowViewModel> OverlayIssues(
        IReadOnlyList<CertificateLedgerRowViewModel> rows)
    {
        var overlaid = new List<CertificateLedgerRowViewModel>(rows.Count);
        foreach (var row in rows)
        {
            if (!store.TryGetIssuedCertificate(row.RegistrationId, out var issue) || issue.Snapshot is null)
            {
                overlaid.Add(row);
                continue;
            }

            overlaid.Add(new CertificateLedgerRowViewModel
            {
                RegistrationId = row.RegistrationId,
                MemberId = row.MemberId,
                MemberName = issue.Snapshot.RecipientName ?? row.MemberName,
                StudentNumber = row.StudentNumber,
                EventTitle = issue.Snapshot.EventTitle ?? row.EventTitle,
                AttendedAt = row.AttendedAt,
                Status = CertificateStatusCode.Issued,
                StatusLabel = "Issued",
                StatusBadgeClass = "badge-completed",
                CertificateNumber = issue.CertificateNumber,
                IssuedAt = issue.IssuedAt,
                Reason = null
            });
        }

        return overlaid;
    }

    private DemoCertificateIssue RequireIssue(int registrationId)
    {
        if (!store.TryGetIssuedCertificate(registrationId, out var issue) || issue.Snapshot == null)
            throw new DomainException(OutcomeCode.NotFound, "Certificate not found.");
        return issue;
    }

    private async Task<IssueContext> RequireCurrentParticipationAsync(
        int registrationId,
        CancellationToken cancellationToken)
    {
        var identity = await registrations.DiscoverReviewAsync(registrationId, cancellationToken);
        if (identity == null || !await registrations.IsConfirmedAsync(registrationId, cancellationToken))
            throw new DomainException(OutcomeCode.Invalid, "A confirmed registration is required for a certificate.");

        var eventItem = await events.GetEventAsync(identity.EventId, cancellationToken);
        if (eventItem == null || eventItem.EventStatusId != EventStatusCode.Completed)
            throw new DomainException(OutcomeCode.Invalid, "Certificates are available only for completed events.");

        var attendanceRow = await attendance.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (attendanceRow == null || attendanceRow.InvalidatedAt is not null)
            throw new DomainException(OutcomeCode.Invalid, "Valid attendance is required for a certificate.");

        // The queue read is untracked, so the second check after rendering sees a decision made in
        // another request rather than the cached entity.
        var queue = await certificates.GetReviewQueueAsync(identity.EventId, cancellationToken);
        var row = queue.FirstOrDefault(item => item.RegistrationId == registrationId);
        if (row?.Status is null)
            throw new DomainException(OutcomeCode.NotFound, "Certificate review not found.");

        return new IssueContext(eventItem, row.Status.Value, row.MemberName);
    }

    private void RequireManagement()
    {
        if (!actor.IsAuthenticated || actor.UserId <= 0 || actor.Role is not (RoleCodes.Admin or RoleCodes.Officer))
            throw new DomainException(OutcomeCode.Forbidden, "You do not have permission to manage certificates.");
    }

    private sealed record IssueContext(Event Event, CertificateStatusCode Status, string MemberName);
}
