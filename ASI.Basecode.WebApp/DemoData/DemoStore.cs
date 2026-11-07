using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Options;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The one in-memory demo store (register M-01). After 6.6 it holds only the demo pass token and the
/// still-demo issued certificate documents (M-11a/M-11b); attendance and certificate reviews are real
/// SQL rows. Nothing here touches SQL, the private file store or the Activity Log, and a restart
/// clears the issued documents.
/// </summary>
public sealed class DemoStore
{
    private readonly object _gate = new();
    private readonly string _tokenKey;
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

    // ---- Certificate issues (M-11a/M-11b) -----------------------------------

    // M-11a: registration identity makes the number unique within the shared store.
    public string GetCertificateNumber(int registrationId, DateTime issuedAt) => $"AT-{issuedAt:yyyy}-{registrationId:D6}";

    // M-11a: publish only after successful rendering. The caller rechecks the real review, attendance
    // and event state before calling; this gate only guarantees one document per registration.
    public void PublishCertificate(int registrationId, int eventId, CertificatePdfSnapshot snapshot, byte[] pdf, int actorId)
    {
        lock (_gate)
        {
            if (_issues.ContainsKey(registrationId))
                throw new DomainException(OutcomeCode.Duplicate, "A certificate has already been issued.");
            if (pdf == null || pdf.Length == 0)
                throw new DomainException(OutcomeCode.Invalid, "The certificate could not be rendered. Please try again.");
            _issues[registrationId] = new DemoCertificateIssue(
                registrationId, eventId, snapshot.CertificateNumber, snapshot.IssuedAt, snapshot, (byte[])pdf.Clone(), actorId);
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
        }
    }
}

/// <summary>A demo certificate issue keyed by the real registration id.</summary>
public sealed record DemoCertificateIssue(
    int RegistrationId,
    int EventId,
    string CertificateNumber,
    DateTime IssuedAt,
    CertificatePdfSnapshot Snapshot = null,
    byte[] Pdf = null,
    int? IssuedBy = null);
