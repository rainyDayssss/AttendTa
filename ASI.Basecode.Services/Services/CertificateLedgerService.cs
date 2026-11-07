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

/// <summary>
/// The real certificate ledger (6.6, R13, R27, AD-3, AD-4, AD-7, AD-13). It reads one completed
/// event's confirmed registrations with their attendance and certificate state, pages the review
/// queue in memory, and runs the approve and decline decisions in one caller-owned transaction that
/// locks the owner's <c>users</c> row and then the event. A decision rechecks the pending state and
/// Valid Attendance under those locks, mutates the review, and appends exactly one
/// <c>certificate.approved</c> or <c>certificate.declined</c> audit entry before the commit.
/// Issuance and document retrieval are 6.7 and 6.8, so they refuse until those tickets land.
/// </summary>
public sealed class CertificateLedgerService(
    IEventRepository events,
    ICertificateRepository certificates,
    IRegistrationRepository registrations,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    IActivityLogWriter audit,
    TimeProvider clock) : ICertificateLedgerService
{
    private const int MaxReasonLength = 1000;
    private const string NotFoundMessage = "Certificate review not found.";
    private const string ReasonRequiredMessage = "Enter a decline reason before declining this review.";
    private const string ReasonTooLongMessage = "Enter a decline reason of up to 1,000 characters.";
    private const string AlreadyDecidedMessage = "This review has already been decided.";
    private const string AttendanceInvalidMessage = "This review's attendance is no longer valid.";
    private const string UnavailableMessage = "Certificate issuance is currently unavailable. Please try again later.";

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
    /// Reads the selected completed event's full review set. The real page path pages this read and
    /// the demo hand-over overlays its still-demo issued state before paging the same read, so tab
    /// counts and paging stay identical in both switch states.
    /// </summary>
    public async Task<CertificateLedgerRead> ReadAsync(int? eventId, CancellationToken cancellationToken = default)
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

    /// <summary>Builds one tab's page from a full read; shared with the demo hand-over.</summary>
    public static CertificateLedgerViewModel BuildPage(
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
        var identity = await registrations.DiscoverReviewAsync(registrationId, cancellationToken);
        if (identity is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        await unitOfWork.ExecuteInTransactionAsync(
            token => DecideInsideTransactionAsync(registrationId, approve, reason, actorUserId, identity, token),
            cancellationToken);
    }

    public Task IssueAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        return Task.FromException(Unavailable());
    }

    public Task<CertificateViewerViewModel> GetViewerAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        return Task.FromException<CertificateViewerViewModel>(NotFound());
    }

    public Task<CertificateDocument> GetPdfAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        RequireManagement();
        return Task.FromException<CertificateDocument>(NotFound());
    }

    private async Task DecideInsideTransactionAsync(
        int registrationId,
        bool approve,
        string reason,
        int actorUserId,
        RegistrationReviewIdentity identity,
        CancellationToken cancellationToken)
    {
        // AD-4 lock order: the participant's users row, then the event. The certificate row has no
        // lock method and is written only while the event lock is held.
        await users.LockUserAsync(identity.UserId, cancellationToken);
        await events.LockEventAsync(identity.EventId, cancellationToken);

        var certificate = await certificates.GetByRegistrationIdAsync(registrationId, cancellationToken);
        if (certificate is null)
        {
            throw new DomainException(OutcomeCode.NotFound, NotFoundMessage);
        }

        // A changed candidate is the single Stale refusal: already decided or no longer attended.
        if (certificate.CertificateStatusId != CertificateStatusCode.PendingApproval)
        {
            throw new DomainException(OutcomeCode.Stale, AlreadyDecidedMessage);
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

        if (!await registrations.HasValidAttendanceAsync(registrationId, cancellationToken))
        {
            throw new DomainException(OutcomeCode.Stale, AttendanceInvalidMessage);
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

        if (userId <= 0
            || (!string.Equals(actor.Role, RoleCodes.Admin, StringComparison.Ordinal)
                && !string.Equals(actor.Role, RoleCodes.Officer, StringComparison.Ordinal)))
        {
            throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
        }

        return userId;
    }

    private static DomainException Unavailable() => new(OutcomeCode.Invalid, UnavailableMessage);
    private static DomainException NotFound() => new(OutcomeCode.NotFound, "Certificate not found.");

    private sealed record CertificateDecisionAuditValues(
        int EventId,
        int RegistrationId,
        int ReviewedBy,
        DateTime ReviewedAt,
        string Status) : IAuditValues;
}
