using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>
/// Certificate review persistence for the event completion and attendance invalidation hooks. Reads
/// compose the canonical facts; writes stage or mutate rows for the caller's unit of work and never
/// save on their own.
/// </summary>
public interface ICertificateRepository
{
    /// <summary>
    /// Registration ids of the event that have Valid Attendance and no certificate row yet, ordered by
    /// registration id. Invalidated attendance and registrations already carrying a review never match.
    /// </summary>
    Task<IReadOnlyList<int>> GetRegistrationsAwaitingReviewAsync(int eventId, CancellationToken ct = default);

    /// <summary>
    /// The registration's certificate row, tracked by the caller's unit of work, or <c>null</c> when
    /// the registration has none. Callers mutate the returned row inside their own transaction and
    /// save through their unit of work; this read never saves and never detaches.
    /// </summary>
    Task<Certificate> GetByRegistrationIdAsync(int registrationId, CancellationToken ct = default);

    /// <summary>
    /// The registration's certificate row without tracking, or <c>null</c> when the registration has
    /// none. Read-only callers use this so a GET never attaches a row the request cannot mutate; the
    /// decision and issuance transactions keep the tracked read.
    /// </summary>
    Task<Certificate> GetByRegistrationIdNoTrackingAsync(int registrationId, CancellationToken ct = default);

    /// <summary>
    /// The event's review queue: every confirmed registration joined to its member, the participant's
    /// user, its attendance row and its certificate row. A registration without a certificate row
    /// still appears with a null <see cref="CertificateReviewQueueRow.CertificateId"/>; the caller
    /// decides whether it belongs on a screen. Ordered by member name then registration id.
    /// </summary>
    Task<IReadOnlyList<CertificateReviewQueueRow>> GetReviewQueueAsync(int eventId, CancellationToken ct = default);

    /// <summary>Stages the certificate rows; the caller saves them inside its transaction.</summary>
    Task AddRangeAsync(IReadOnlyList<Certificate> certificates, CancellationToken ct = default);
}

/// <summary>
/// One confirmed registration of an event with its participant, attendance and certificate state.
/// The certificate fields are null when the registration has no review yet.
/// </summary>
public sealed record CertificateReviewQueueRow(
    int RegistrationId,
    int MemberId,
    string MemberName,
    string StudentNumber,
    DateTime? AttendedAt,
    int? CertificateId,
    CertificateStatusCode? Status,
    string ReviewNotes,
    string RecipientNameSnapshot,
    string EventTitleSnapshot,
    string CertificateNumber,
    DateTime? IssuedAt);
