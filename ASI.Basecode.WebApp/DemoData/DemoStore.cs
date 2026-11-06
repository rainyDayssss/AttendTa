using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
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
    private readonly HashSet<int> _checkInOpenEventIds = new();
    private readonly HashSet<int> _completedEventIds = new();
    private readonly Dictionary<int, DemoAttendance> _attendance = new();
    private readonly Dictionary<int, DemoCertificateReview> _reviews = new();
    private readonly Dictionary<int, DemoCertificateIssue> _issues = new();
    private readonly Dictionary<int, DemoAccountStatus> _accounts = new();
    private readonly Dictionary<int, DemoMembershipStatus> _memberships = new();

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

    // ---- Event check-in window and completion state -------------------------

    public bool IsCheckInOpen(int eventId)
    {
        lock (_gate)
        {
            return _checkInOpenEventIds.Contains(eventId);
        }
    }

    public bool IsCompleted(int eventId)
    {
        lock (_gate)
        {
            return _completedEventIds.Contains(eventId);
        }
    }

    public void SetCheckInOpen(int eventId, bool open)
    {
        lock (_gate)
        {
            if (open)
            {
                _checkInOpenEventIds.Add(eventId);
            }
            else
            {
                _checkInOpenEventIds.Remove(eventId);
            }
        }
    }

    /// <summary>Marks the event completed and closes its check-in window.</summary>
    public void MarkCompleted(int eventId)
    {
        lock (_gate)
        {
            _completedEventIds.Add(eventId);
            _checkInOpenEventIds.Remove(eventId);
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
    public DemoAttendance RecordAttendance(int registrationId, int eventId, DateTime recordedAt, CheckInMethodCode method)
    {
        lock (_gate)
        {
            if (_attendance.TryGetValue(registrationId, out var existing))
            {
                return existing;
            }

            var record = new DemoAttendance(registrationId, eventId, recordedAt, method, null, null);
            _attendance[registrationId] = record;
            return record;
        }
    }

    /// <summary>
    /// Invalidates an attendance with a reason. Refused when the registration's certificate is
    /// already issued; otherwise a pending or approved review becomes not eligible with that reason
    /// (register rules 4, 5 and 8).
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

            if (_reviews.TryGetValue(registrationId, out var review)
                && review.Status is CertificateStatusCode.PendingApproval or CertificateStatusCode.Eligible)
            {
                _reviews[registrationId] = review with
                {
                    Status = CertificateStatusCode.NotEligible,
                    Reason = trimmedReason,
                    UpdatedAt = invalidatedAt
                };
            }

            return invalidated;
        }
    }

    // ---- Certificate reviews and issues -------------------------------------

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

    public void SeedIssuedCertificate(int registrationId, int eventId, string certificateNumber, DateTime issuedAt)
    {
        lock (_gate)
        {
            _issues[registrationId] = new DemoCertificateIssue(registrationId, eventId, certificateNumber, issuedAt);
            _reviews[registrationId] = new DemoCertificateReview(
                registrationId, eventId, CertificateStatusCode.Issued, null, issuedAt);
        }
    }

    // ---- Account and membership status (read by 5.11) -----------------------

    public bool TryGetAccountStatus(int userId, out DemoAccountStatus status)
    {
        lock (_gate)
        {
            return _accounts.TryGetValue(userId, out status);
        }
    }

    public DemoAccountStatus SetAccountActive(int userId, bool isActive, string reason, DateTime changedAt)
    {
        var trimmedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new DomainException(OutcomeCode.Invalid, "A reason is required.", "reason");
        }

        lock (_gate)
        {
            var status = new DemoAccountStatus(userId, isActive, trimmedReason, changedAt);
            _accounts[userId] = status;
            return status;
        }
    }

    public bool TryGetMembershipStatus(int memberId, out DemoMembershipStatus status)
    {
        lock (_gate)
        {
            return _memberships.TryGetValue(memberId, out status);
        }
    }

    public DemoMembershipStatus SetMembershipStatus(int memberId, MembershipStatusCode status, string reason, DateTime changedAt)
    {
        var trimmedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new DomainException(OutcomeCode.Invalid, "A reason is required.", "reason");
        }

        lock (_gate)
        {
            var membership = new DemoMembershipStatus(memberId, status, trimmedReason, changedAt);
            _memberships[memberId] = membership;
            return membership;
        }
    }
}

/// <summary>A demo attendance record keyed by the real registration id.</summary>
public sealed record DemoAttendance(
    int RegistrationId,
    int EventId,
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
    DateTime UpdatedAt);

/// <summary>A demo certificate issue keyed by the real registration id.</summary>
public sealed record DemoCertificateIssue(
    int RegistrationId,
    int EventId,
    string CertificateNumber,
    DateTime IssuedAt);

/// <summary>A demo account status keyed by the real user id.</summary>
public sealed record DemoAccountStatus(int UserId, bool IsActive, string Reason, DateTime ChangedAt);

/// <summary>A demo membership status keyed by the real member id.</summary>
public sealed record DemoMembershipStatus(int MemberId, MembershipStatusCode Status, string Reason, DateTime ChangedAt);
