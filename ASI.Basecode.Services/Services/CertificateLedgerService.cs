using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

/// <summary>
/// The real certificate ledger (6.6, 6.7, R13, R27, AD-3, AD-4, AD-7, AD-13). It reads one completed
/// event's confirmed registrations with their attendance and certificate state, pages the review
/// queue in memory, and runs the approve and decline decisions in one caller-owned transaction that
/// locks the owner's <c>users</c> row and then the event. A decision rechecks the pending state and
/// Valid Attendance under those locks, mutates the review, and appends exactly one
/// <c>certificate.approved</c> or <c>certificate.declined</c> audit entry before the commit.
/// Issuance renders and promotes the PDF outside every lock, then rechecks every fact under the same
/// locks and commits the issued row with one <c>certificate.issued</c> audit entry; a failed commit
/// deletes the file it created. Document retrieval (6.8) keeps the viewer Management-only and lets
/// the PDF route admit Management or the registration's owner; both require an issued row with a
/// retained <c>certificates/</c> file and stream those bytes without regenerating them.
/// </summary>
public sealed class CertificateLedgerService(
    IEventRepository events,
    ICertificateRepository certificates,
    IRegistrationRepository registrations,
    IAttendanceRepository attendance,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    IActivityLogWriter audit,
    TimeProvider clock,
    ICertificatePdfRenderer renderer,
    IPrivateFileStore files) : ICertificateLedgerService
{
    private const int MaxReasonLength = 1000;
    private const string NotFoundMessage = "Certificate review not found.";
    private const string ReasonRequiredMessage = "Enter a decline reason before declining this review.";
    private const string ReasonTooLongMessage = "Enter a decline reason of up to 1,000 characters.";
    private const string AlreadyDecidedMessage = "This review has already been decided.";
    private const string AttendanceInvalidMessage = "This review's attendance is no longer valid.";
    private const string AlreadyIssuedMessage = "A certificate has already been issued.";
    private const string NotApprovedMessage = "Approve this review before issuing a certificate.";
    private const string DeclinedMessage = "This review was declined and cannot be issued.";
    private const string NotConfirmedMessage = "A confirmed registration is required for a certificate.";
    private const string EventNotCompletedMessage = "Certificates are available only for completed events.";
    private const string ChangedMessage = "This review's certificate details changed. Reload the ledger and try again.";
    private const string RenderFailedMessage = "The certificate could not be rendered. Please try again.";

    // A stored certificate key must stay inside the certificates area. The store's segment and
    // descendant checks are the real containment barrier; this prefix is an area hint that refuses
    // a corrupt file_path before the store is asked.
    private const string CertificateKeyPrefix = "certificates/";

    public async Task<CertificateLedgerViewModel> GetPageAsync(
        int? eventId,
        CertificateLedgerTab tab,
        int? page,
        CancellationToken cancellationToken = default)
    {
        var read = await ReadAsync(eventId, cancellationToken);
        return BuildPage(read, tab, page);
    }

    /// <summary>
    /// Reads the selected completed event's full review set: every registration that already carries a
    /// certificate row, mapped for the issued and eligibility tabs. <see cref="BuildPage"/> pages this
    /// read, so tab counts and paging are derived from the same rows.
    /// </summary>
    private async Task<CertificateLedgerRead> ReadAsync(int? eventId, CancellationToken cancellationToken = default)
    {
        RequireManagement();

        var count = await events.CountAsync(cancellationToken);
        var allEvents = count == 0
            ? Array.Empty<EventListItem>()
            : await events.GetPageAsync(0, count, cancellationToken);
        var completed = allEvents
            .Where(row => row.EventStatusId == EventStatusCode.Completed)
            .OrderByDescending(row => row.StartsAt)
            .ThenBy(row => row.EventId)
            .ToList();
        var selected = eventId.HasValue
            ? completed.FirstOrDefault(row => row.EventId == eventId.Value)
            : completed.FirstOrDefault();

        var rows = new List<CertificateLedgerRowViewModel>();
        if (selected is not null)
        {
            var queue = await certificates.GetReviewQueueAsync(selected.EventId, cancellationToken);
            foreach (var row in queue.Where(row => row.CertificateId.HasValue))
            {
                rows.Add(Map(row, selected.Title));
            }
        }

        return new CertificateLedgerRead(
            new EventSelectorViewModel
            {
                Label = "Completed event",
                SelectedEventId = selected?.EventId,
                Options = completed.Select(row => new EventSelectorOption(row.EventId, row.Title)).ToList()
            },
            rows);
    }

    /// <summary>Builds one tab's page from a full read.</summary>
    private static CertificateLedgerViewModel BuildPage(
        CertificateLedgerRead read,
        CertificateLedgerTab tab,
        int? page)
    {
        ArgumentNullException.ThrowIfNull(read);

        var issuedCount = read.Rows.Count(row => row.Status == CertificateStatusCode.Issued);
        var filtered = read.Rows
            .Where(row => (row.Status == CertificateStatusCode.Issued) == (tab == CertificateLedgerTab.Issued))
            .OrderBy(row => row.MemberName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.RegistrationId)
            .ToList();
        var request = new PageRequest(page);
        var normalized = request.NormalizePageNumber(filtered.Count);
        var items = filtered
            .Skip(request.GetOffset(normalized))
            .Take(PageRequest.FixedPageSize)
            .ToList();

        return new CertificateLedgerViewModel
        {
            ActiveTab = tab,
            EligibilityCount = read.Rows.Count - issuedCount,
            IssuedCount = issuedCount,
            Rows = items,
            Page = new PagedResult<CertificateLedgerRowViewModel>(items, normalized, filtered.Count),
            Selector = new EventSelectorViewModel
            {
                Label = read.Selector.Label,
                SelectedEventId = read.Selector.SelectedEventId,
                Options = read.Selector.Options,
                HiddenValues = new Dictionary<string, string>
                {
                    ["tab"] = tab == CertificateLedgerTab.Issued ? "issued" : "eligibility"
                }
            }
        };
    }

    public async Task DecideAsync(
        int registrationId,
        bool approve,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = RequireManagement();

        // Discovery outside the transaction; every fact is re-read after the locks are taken.
        var identity = await registrations.DiscoverIdentityAsync(registrationId, cancellationToken);
        if (identity is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            token => DecideInsideTransactionAsync(registrationId, approve, reason, actorUserId, identity, token),
            cancellationToken);
    }

    public async Task IssueAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        var actorUserId = RequireManagement();

        // Discovery and every pre-check run outside the locks. Rendering and file I/O must not hold a
        // transaction (AD-4), so each fact is re-read after the locks are taken.
        var identity = await registrations.DiscoverIdentityAsync(registrationId, cancellationToken);
        if (identity is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        if (!await registrations.IsConfirmedAsync(registrationId, cancellationToken))
        {
            throw new DomainException(OutcomeCode.Invalid, NotConfirmedMessage);
        }

        var eventItem = await events.GetEventAsync(identity.EventId, cancellationToken);
        if (eventItem is null || eventItem.EventStatusId != EventStatusCode.Completed)
        {
            throw new DomainException(OutcomeCode.Invalid, EventNotCompletedMessage);
        }

        // The queue read is untracked, so the tracked re-read inside the transaction sees a decision
        // made in another request rather than a cached entity.
        var review = await FindReviewAsync(registrationId, identity.EventId, cancellationToken);
        if (review is null || !review.CertificateId.HasValue)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        if (review.Status == CertificateStatusCode.Issued)
        {
            throw new DomainException(OutcomeCode.Duplicate, AlreadyIssuedMessage);
        }

        if (review.Status == CertificateStatusCode.NotEligible)
        {
            throw new DomainException(OutcomeCode.Invalid, DeclinedMessage);
        }

        if (review.Status != CertificateStatusCode.Eligible)
        {
            throw new DomainException(OutcomeCode.Invalid, NotApprovedMessage);
        }

        if (!await registrations.HasValidAttendanceAsync(registrationId, cancellationToken))
        {
            throw new DomainException(OutcomeCode.Stale, AttendanceInvalidMessage);
        }

        var recipient = await users.GetByIdAsync(identity.UserId, cancellationToken);
        var issuer = await users.GetByIdAsync(actorUserId, cancellationToken);
        if (recipient is null || issuer is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        var issuedAt = clock.GetUtcNow().UtcDateTime;
        var snapshot = new CertificatePdfSnapshot(
            recipient.Name,
            eventItem.Title,
            eventItem.StartsAt,
            issuedAt,
            $"AT-{issuedAt:yyyy}-{registrationId:D6}");

        // Render and promote before the transaction so no lock is held during Chromium or disk I/O.
        var filePath = await RenderAndPromoteAsync(snapshot, cancellationToken);
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                token => IssueInsideTransactionAsync(
                    registrationId, identity, snapshot, issuer.Name, filePath, actorUserId, token),
                cancellationToken);
        }
        catch
        {
            // Only the file this attempt created is deleted; an issued row keeps its own path.
            DeleteBestEffort(filePath);
            throw;
        }
    }

    public async Task<CertificateViewerViewModel> GetViewerAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        var (certificate, identity) = await RequireIssuedDocumentAsync(registrationId, cancellationToken);

        return new CertificateViewerViewModel
        {
            CertificateId = registrationId,
            EventId = identity.EventId,
            MemberName = certificate.RecipientNameSnapshot,
            EventTitle = certificate.EventTitleSnapshot,
            CertificateNumber = certificate.CertificateNumber
        };
    }

    public async Task<CertificateDocument> GetPdfAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        var actorUserId = RequireAuthenticated();
        var (certificate, identity) = await RequireIssuedDocumentAsync(registrationId, cancellationToken);

        // Management may read any issued row; a member may read only their own. A non-owner and an
        // unknown id both surface the identical not-found.
        if (!IsManagement() && identity.UserId != actorUserId)
        {
            throw NotFound();
        }

        return await ReadRetainedAsync(certificate, cancellationToken);
    }

    private async Task DecideInsideTransactionAsync(
        int registrationId,
        bool approve,
        string reason,
        int actorUserId,
        RegistrationIdentity identity,
        CancellationToken cancellationToken)
    {
        // AD-4 lock order: the participant's users row, then the event. The certificate row has no
        // lock method and is written only while the event lock is held.
        await users.LockUserAsync(identity.UserId, cancellationToken);
        var eventItem = await events.LockEventAsync(identity.EventId, cancellationToken);

        var certificate = await certificates.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (certificate is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        // One certificate rule: a Confirmed seat with valid attendance on a completed event, whose
        // review is still pending. The refusal names the fact that actually failed.
        var registration = await registrations.GetForUpdateAsync(registrationId, cancellationToken);
        var attendanceRow = await attendance.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (eventItem is null
            || !ParticipationFacts.IsCertificateEligible(
                registration, attendanceRow, certificate, CertificateStatusCode.PendingApproval, eventItem))
        {
            var message = certificate.CertificateStatusId != CertificateStatusCode.PendingApproval
                ? AlreadyDecidedMessage
                : !ParticipationFacts.HoldsSeat(registration)
                    ? ChangedMessage
                    : AttendanceInvalidMessage;
            throw new DomainException(OutcomeCode.Stale, message);
        }

        // The reason is validated inside the transaction, so no state is committed without it.
        var trimmedReason = reason?.Trim();
        if (!approve)
        {
            if (string.IsNullOrWhiteSpace(trimmedReason))
            {
                throw new DomainException(OutcomeCode.Invalid, ReasonRequiredMessage, "reason");
            }

            if (trimmedReason.Length > MaxReasonLength)
            {
                throw new DomainException(OutcomeCode.Invalid, ReasonTooLongMessage, "reason");
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        certificate.CertificateStatusId = approve
            ? CertificateStatusCode.Eligible
            : CertificateStatusCode.NotEligible;
        certificate.ReviewNotes = approve ? null : trimmedReason;
        certificate.ReviewedBy = actorUserId;
        certificate.ReviewedAt = now;
        certificate.UpdatedBy = actorUserId;
        certificate.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        audit.Append(new AuditEntry(
            approve ? AuditActions.CertificateApproved : AuditActions.CertificateDeclined,
            "certificates",
            certificate.CertificateId,
            Reason: approve ? null : trimmedReason,
            NewValues: new CertificateDecisionAuditValues(
                identity.EventId,
                registrationId,
                actorUserId,
                now,
                approve ? "eligible" : "not_eligible")));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task IssueInsideTransactionAsync(
        int registrationId,
        RegistrationIdentity identity,
        CertificatePdfSnapshot snapshot,
        string issuerName,
        string filePath,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        // AD-4 lock order: the owner's users row, then the event. The certificate row has no lock
        // method and is written only while the event lock is held.
        await users.LockUserAsync(identity.UserId, cancellationToken);
        var eventItem = await events.LockEventAsync(identity.EventId, cancellationToken);

        var certificate = await certificates.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (certificate is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        if (certificate.CertificateStatusId == CertificateStatusCode.Issued)
        {
            throw new DomainException(OutcomeCode.Duplicate, AlreadyIssuedMessage);
        }

        // A decision or invalidation that landed while the PDF rendered is the single Stale refusal.
        // The shared certificate rule covers the seat, valid attendance, approved review and completed
        // event; a missing event row is refused before the rule can treat it as "already known".
        var registration = await registrations.GetForUpdateAsync(registrationId, cancellationToken);
        var attendanceRow = await attendance.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (eventItem is null
            || !ParticipationFacts.IsCertificateEligible(
                registration, attendanceRow, certificate, CertificateStatusCode.Eligible, eventItem))
        {
            throw new DomainException(OutcomeCode.Stale, ChangedMessage);
        }

        certificate.CertificateStatusId = CertificateStatusCode.Issued;
        certificate.CertificateNumber = snapshot.CertificateNumber;
        certificate.RecipientNameSnapshot = snapshot.RecipientName;
        certificate.EventTitleSnapshot = snapshot.EventTitle;
        certificate.EventStartsAtSnapshot = snapshot.EventStartsAt;
        certificate.IssuerNameSnapshot = issuerName;
        certificate.FilePath = filePath;
        certificate.IssuedBy = actorUserId;
        certificate.IssuedAt = snapshot.IssuedAt;
        certificate.UpdatedBy = actorUserId;
        certificate.UpdatedAt = snapshot.IssuedAt;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        audit.Append(new AuditEntry(
            AuditActions.CertificateIssued,
            "certificates",
            certificate.CertificateId,
            NewValues: new CertificateIssuedAuditValues(
                identity.EventId,
                registrationId,
                snapshot.CertificateNumber,
                actorUserId,
                snapshot.IssuedAt)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> RenderAndPromoteAsync(
        CertificatePdfSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        string temporaryPath = null;
        try
        {
            var pdf = await renderer.RenderSnapshotAsync(snapshot, cancellationToken);
            if (pdf is null || pdf.Length == 0)
            {
                throw new DomainException(OutcomeCode.Invalid, RenderFailedMessage);
            }

            await using (var content = new MemoryStream(pdf, writable: false))
            {
                temporaryPath = await files.SaveAsync(PrivateFileArea.Temporary, content, ".pdf", cancellationToken);
            }

            var promotedKey = await files.PromoteAsync(temporaryPath, PrivateFileArea.Certificates, cancellationToken);
            temporaryPath = null;
            return promotedKey;
        }
        catch (DomainException)
        {
            DeleteBestEffort(temporaryPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            DeleteBestEffort(temporaryPath);
            throw;
        }
        catch (Exception)
        {
            // A renderer that throws or returns non-PDF content leaves no file behind.
            DeleteBestEffort(temporaryPath);
            throw new DomainException(OutcomeCode.Invalid, RenderFailedMessage);
        }
    }

    private async Task<CertificateReviewQueueRow> FindReviewAsync(
        int registrationId,
        int eventId,
        CancellationToken cancellationToken)
    {
        var queue = await certificates.GetReviewQueueAsync(eventId, cancellationToken);
        return queue.FirstOrDefault(row => row.RegistrationId == registrationId);
    }

    private void DeleteBestEffort(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            files.Delete(path);
        }
        catch (Exception)
        {
            // The row is unchanged; a leftover file is safer than surfacing a cleanup failure.
        }
    }

    /// <summary>
    /// Resolves the registration's owner and its issued certificate row, and requires a retained
    /// <c>certificates/</c> key. Every refusal is the same not-found, so a caller cannot tell an
    /// unknown id from an unissued row or a row whose file is missing.
    /// </summary>
    private async Task<(Certificate Certificate, RegistrationIdentity Identity)> RequireIssuedDocumentAsync(
        int registrationId,
        CancellationToken cancellationToken)
    {
        var identity = await registrations.DiscoverIdentityAsync(registrationId, cancellationToken);
        if (identity is null)
        {
            throw NotFound();
        }

        var certificate = await certificates.GetByRegistrationIdNoTrackingAsync(registrationId, cancellationToken);
        if (certificate is null
            || certificate.CertificateStatusId != CertificateStatusCode.Issued
            || string.IsNullOrWhiteSpace(certificate.FilePath)
            || !certificate.FilePath.StartsWith(CertificateKeyPrefix, StringComparison.Ordinal))
        {
            throw NotFound();
        }

        return (certificate, identity);
    }

    /// <summary>
    /// Streams the retained PDF. A missing, unreadable or unsafe stored key is a controlled
    /// not-found, never an unhandled 500.
    /// </summary>
    private async Task<CertificateDocument> ReadRetainedAsync(
        Certificate certificate,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await files.OpenReadAsync(certificate.FilePath, cancellationToken);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, cancellationToken);
            return new CertificateDocument(copy.ToArray(), certificate.CertificateNumber);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            throw NotFound();
        }
    }

    private static CertificateLedgerRowViewModel Map(CertificateReviewQueueRow row, string eventTitle)
    {
        var (label, badge) = row.Status switch
        {
            CertificateStatusCode.PendingApproval => ("Pending review", "badge-pending"),
            CertificateStatusCode.Eligible => ("Approved", "badge-approved"),
            CertificateStatusCode.NotEligible => ("Not eligible", "badge-rejected"),
            _ => ("Issued", "badge-completed")
        };

        return new CertificateLedgerRowViewModel
        {
            RegistrationId = row.RegistrationId,
            MemberId = row.MemberId,
            MemberName = row.RecipientNameSnapshot ?? row.MemberName,
            StudentNumber = row.StudentNumber,
            EventTitle = row.Status == CertificateStatusCode.Issued
                ? row.EventTitleSnapshot ?? eventTitle
                : eventTitle,
            AttendedAt = row.AttendedAt,
            Status = row.Status,
            StatusLabel = label,
            StatusBadgeClass = badge,
            Reason = row.ReviewNotes,
            CertificateNumber = row.CertificateNumber,
            IssuedAt = row.IssuedAt
        };
    }

    private int RequireManagement()
    {
        var userId = RequireAuthenticated();
        if (!IsManagement())
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        return userId;
    }

    private int RequireAuthenticated()
    {
        if (!actor.IsAuthenticated)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        int userId;
        try
        {
            userId = actor.UserId;
        }
        catch (InvalidOperationException)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        if (userId <= 0)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        return userId;
    }

    private bool IsManagement() =>
        string.Equals(actor.Role, RoleCodes.Admin, StringComparison.Ordinal)
        || string.Equals(actor.Role, RoleCodes.Officer, StringComparison.Ordinal);

    private static DomainException NotFound() => new(OutcomeCode.NotFound, "Certificate not found.");

    private sealed record CertificateDecisionAuditValues(
        int EventId,
        int RegistrationId,
        int ReviewedBy,
        DateTime ReviewedAt,
        string Status) : IAuditValues;

    private sealed record CertificateIssuedAuditValues(
        int EventId,
        int RegistrationId,
        string CertificateNumber,
        int IssuedBy,
        DateTime IssuedAt) : IAuditValues;
}
