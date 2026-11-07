using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Options;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The one in-memory demo store (register M-01). Every demo surface reads and writes the same
/// instance, keyed by real record ids, so an action on one screen shows on the others for the rest
/// of the session. Nothing here touches SQL, the private file store or the Activity Log, and a
/// restart clears it.
/// </summary>
public sealed class DemoStore
{
    private readonly object _gate = new();
    private readonly string _tokenKey;
    private readonly HashSet<int> _completedEventIds = new();
    private readonly Dictionary<int, DemoAttendance> _attendance = new();
    private readonly Dictionary<int, DemoCertificateReview> _reviews = new();
    private readonly Dictionary<int, DemoCertificateIssue> _issues = new();

    public DemoStore(IOptions<DemoDataOptions> options)
    {
        var tokenKey = options?.Value?.TokenKey;
        if (string.IsNullOrWhiteSpace(tokenKey))
        {
            // The startup guard already refuses this; keep the store unusable rather than guess a key.
            throw new InvalidOperationException("DemoData:TokenKey is required when demo data is enabled.");
        }

        _tokenKey = tokenKey;
    }

    /// <summary>
    /// The stable demo pass token: HMAC-SHA256 of the registration id under
    /// <c>DemoData:TokenKey</c>. The same registration id always derives the same token, so a pass
    /// printed before a restart keeps scanning.
    /// </summary>
    public static string DerivePassToken(int registrationId, string tokenKey)
    {
        if (registrationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(registrationId));
        }

        if (string.IsNullOrWhiteSpace(tokenKey))
        {
            throw new ArgumentException("A token key is required to derive a pass token.", nameof(tokenKey));
        }

        var message = Encoding.UTF8.GetBytes(registrationId.ToString(CultureInfo.InvariantCulture));
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(tokenKey), message);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>The stable demo pass token for a real registration id.</summary>
    public string GetPassToken(int registrationId) => DerivePassToken(registrationId, _tokenKey);

    // ---- Event completion state ---------------------------------------------

    public bool IsCompleted(int eventId)
    {
        lock (_gate)
        {
            return _completedEventIds.Contains(eventId);
        }
    }

    /// <summary>
    /// Marks the event completed. This is only the certificate-ledger demo and test fixture now; the
    /// app completes events through the real <c>IEventLifecycleService</c>, and the real completion
    /// hook creates the certificate reviews. Reviews for the demo ledger are seeded explicitly
    /// through <see cref="SeedPendingReviewsForEvent"/>.
    /// </summary>
    public void MarkCompleted(int eventId)
    {
        lock (_gate)
        {
            _completedEventIds.Add(eventId);
        }
    }

    // ---- Attendance ---------------------------------------------------------

    public bool TryGetAttendance(int registrationId, out DemoAttendance attendance)
    {
        lock (_gate)
        {
            return _attendance.TryGetValue(registrationId, out attendance);
        }
    }

    /// <summary>
    /// Records a check-in. A repeat returns the original record so the first time is preserved.
    /// </summary>
    public DemoAttendance RecordAttendance(
        int registrationId,
        int eventId,
        string memberName,
        string studentNumber,
        DateTime recordedAt,
        CheckInMethodCode method)
    {
        lock (_gate)
        {
            if (_attendance.TryGetValue(registrationId, out var existing))
            {
                return existing;
            }

            var record = new DemoAttendance(registrationId, eventId, memberName, studentNumber, recordedAt, method, null, null);
            _attendance[registrationId] = record;
            return record;
        }
    }

    /// <summary>
    /// Invalidates a seeded attendance with a reason. The app invalidates through the real
    /// <c>IAttendanceInvalidationService</c>; this helper only builds the start-up seed and the
    /// demo tests. Refused when the registration's certificate is already issued. The review flip
    /// is owned by the real <c>CertificateReviewInvalidationHandler</c> (6.5), so this helper only
    /// stamps the attendance.
    /// </summary>
    public DemoAttendance InvalidateAttendance(int registrationId, string reason, DateTime invalidatedAt)
    {
        var trimmedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new DomainException(OutcomeCode.Invalid, "An invalidation reason is required.", "reason");
        }

        lock (_gate)
        {
            if (!_attendance.TryGetValue(registrationId, out var record))
            {
                throw new DomainException(OutcomeCode.NotFound, "Attendance not found.");
            }

            if (_issues.ContainsKey(registrationId))
            {
                throw new DomainException(OutcomeCode.Invalid, "A certificate has already been issued for this attendance.");
            }

            var invalidated = record with { InvalidatedAt = invalidatedAt, InvalidationReason = trimmedReason };
            _attendance[registrationId] = invalidated;
            return invalidated;
        }
    }

    // ---- Certificate reviews and issues -------------------------------------

    // M-11a: registration identity makes the number unique within the shared store.
    public string GetCertificateNumber(int registrationId, DateTime issuedAt) => $"AT-{issuedAt:yyyy}-{registrationId:D6}";

    // M-10a removed by 6.4: the real completion hook creates the reviews. This helper only seeds the
    // demo store for the demo ledger and its tests, and never overwrites an existing review.
    public void SeedPendingReviewsForEvent(int eventId, DateTime now)
    {
        lock (_gate)
        {
            foreach (var attendance in _attendance.Values.Where(row => row.EventId == eventId && row.IsValid))
            {
                _reviews.TryAdd(attendance.RegistrationId, new DemoCertificateReview(
                    attendance.RegistrationId, eventId, CertificateStatusCode.PendingApproval, null, now));
            }
        }
    }

    // M-10b: 6.6 replaces the guarded in-memory decisions.
    public void DecideCertificate(int registrationId, bool approve, string reason, int actorId, DateTime now)
    {
        var trimmed = reason?.Trim();
        if (!approve && (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 1000))
            throw new DomainException(OutcomeCode.Invalid, "Enter a decline reason of up to 1,000 characters.", "reason");
        lock (_gate)
        {
            RequireValidAttendance(registrationId);
            if (!_reviews.TryGetValue(registrationId, out var review))
                throw new DomainException(OutcomeCode.NotFound, "Certificate review not found.");
            if (review.Status != CertificateStatusCode.PendingApproval)
                throw new DomainException(OutcomeCode.Invalid, "This review has already been decided.");
            _reviews[registrationId] = review with
            {
                Status = approve ? CertificateStatusCode.Eligible : CertificateStatusCode.NotEligible,
                Reason = approve ? null : trimmed,
                UpdatedAt = now,
                ReviewedBy = actorId
            };
        }
    }

    // M-11a: publish only after successful rendering, rechecking against invalidation and duplicate issuance.
    public void PublishCertificate(int registrationId, CertificatePdfSnapshot snapshot, byte[] pdf, int actorId)
    {
        lock (_gate)
        {
            RequireValidAttendance(registrationId);
            if (_issues.ContainsKey(registrationId))
                throw new DomainException(OutcomeCode.Duplicate, "A certificate has already been issued.");
            if (!_reviews.TryGetValue(registrationId, out var review) || review.Status != CertificateStatusCode.Eligible)
                throw new DomainException(OutcomeCode.Invalid, "Approve this review before issuing a certificate.");
            if (pdf == null || pdf.Length == 0)
                throw new DomainException(OutcomeCode.Invalid, "The certificate could not be rendered. Please try again.");
            _issues[registrationId] = new DemoCertificateIssue(registrationId, review.EventId,
                snapshot.CertificateNumber, snapshot.IssuedAt, snapshot, (byte[])pdf.Clone(), actorId);
            _reviews[registrationId] = review with { Status = CertificateStatusCode.Issued, UpdatedAt = snapshot.IssuedAt };
        }
    }

    // M-11b: a seeded issue is rendered once, then its original bytes are reused for this process lifetime.
    public DemoCertificateIssue AttachCertificateDocument(int registrationId, CertificatePdfSnapshot snapshot, byte[] pdf)
    {
        lock (_gate)
        {
            if (!_issues.TryGetValue(registrationId, out var issue))
                throw new DomainException(OutcomeCode.NotFound, "Certificate not found.");
            if (issue.Pdf == null)
            {
                issue = issue with { Snapshot = issue.Snapshot ?? snapshot, Pdf = (byte[])pdf.Clone() };
                _issues[registrationId] = issue;
            }
            return issue;
        }
    }

    private void RequireValidAttendance(int registrationId)
    {
        if (!_attendance.TryGetValue(registrationId, out var attendance) || !attendance.IsValid)
            throw new DomainException(OutcomeCode.Invalid, "Valid attendance is required for a certificate.");
    }

    public bool TryGetReview(int registrationId, out DemoCertificateReview review)
    {
        lock (_gate)
        {
            return _reviews.TryGetValue(registrationId, out review);
        }
    }

    public void SeedReview(int registrationId, int eventId, CertificateStatusCode status, string reason, DateTime updatedAt)
    {
        lock (_gate)
        {
            _reviews[registrationId] = new DemoCertificateReview(registrationId, eventId, status, reason, updatedAt);
        }
    }

    public bool IsCertificateIssued(int registrationId)
    {
        lock (_gate)
        {
            return _issues.ContainsKey(registrationId);
        }
    }

    public bool TryGetIssuedCertificate(int registrationId, out DemoCertificateIssue issue)
    {
        lock (_gate)
        {
            return _issues.TryGetValue(registrationId, out issue);
        }
    }

    public void SeedIssuedCertificate(int registrationId, int eventId, string certificateNumber, DateTime issuedAt, CertificatePdfSnapshot snapshot = null)
    {
        lock (_gate)
        {
            _issues[registrationId] = new DemoCertificateIssue(registrationId, eventId, certificateNumber, issuedAt, snapshot);
            _reviews[registrationId] = new DemoCertificateReview(
                registrationId, eventId, CertificateStatusCode.Issued, null, issuedAt);
        }
    }
}

/// <summary>A demo attendance record keyed by the real registration id.</summary>
public sealed record DemoAttendance(
    int RegistrationId,
    int EventId,
    string MemberName,
    string StudentNumber,
    DateTime RecordedAt,
    CheckInMethodCode Method,
    DateTime? InvalidatedAt,
    string InvalidationReason)
{
    public bool IsValid => InvalidatedAt is null;
}

/// <summary>A demo certificate review keyed by the real registration id.</summary>
public sealed record DemoCertificateReview(
    int RegistrationId,
    int EventId,
    CertificateStatusCode Status,
    string Reason,
    DateTime UpdatedAt,
    int? ReviewedBy = null);

/// <summary>A demo certificate issue keyed by the real registration id.</summary>
public sealed record DemoCertificateIssue(
    int RegistrationId,
    int EventId,
    string CertificateNumber,
    DateTime IssuedAt,
    CertificatePdfSnapshot Snapshot = null,
    byte[] Pdf = null,
    int? IssuedBy = null);
