using ASI.Basecode.Data.Models;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace ASI.Basecode.Data.Interfaces;

public interface IRegistrationRepository
{
    Task<bool> IsConfirmedAsync(int registrationId, CancellationToken cancellationToken = default);

    /// <summary>Reports whether the member already has a registration of any status for the event.</summary>
    Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the actor's own registration for the event, or <c>null</c> when they have none.
    /// Ownership is resolved through <c>members.user_id</c>, so another member's row never matches.
    /// </summary>
    Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default);

    /// <summary>Counts the actor's registrations across events, resolving ownership through <c>members.user_id</c>.</summary>
    Task<int> CountMyRegistrationsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one owner-scoped page of the actor's registrations across events, newest start first,
    /// with the event and registration facts the My Events card needs.
    /// </summary>
    Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the actor's own door passes in one owner-scoped query: Confirmed registrations on
    /// Published events whose <c>qr_token</c> is set, with the event, type, venue and holder facts
    /// the pass card needs. Another member's registration never matches.
    /// </summary>
    Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one registration by id and owner only. Another member's id and an unknown id both
    /// return <c>null</c>, so the caller cannot tell them apart.
    /// </summary>
    Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the confirmed registration of the event whose <c>qr_token</c> equals the scanned value.
    /// A null, blank or non-hex value never matches.
    /// </summary>
    Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken cancellationToken = default);

    /// <summary>Counts confirmed seats for the event using the canonical seat fact.</summary>
    Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the event's Confirmed registrations whose member name or student number contains the
    /// trimmed query, using the database collation for the comparison.
    /// </summary>
    Task<int> CountConfirmedLookupAsync(int eventId, string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of the event's Confirmed registrations whose member name or student number
    /// contains the trimmed query, ordered by member name then registration id, with the attendance
    /// state the manual check-in table shows. Read only.
    /// </summary>
    Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(int eventId, string query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the student number of a Confirmed registration of the event, or <c>null</c> when the
    /// registration is unknown, belongs to another event or is not Confirmed.
    /// </summary>
    Task<string> GetConfirmedStudentNumberAsync(int eventId, int registrationId, CancellationToken cancellationToken = default);

    Task AddAsync(Registration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the registration fresh for a write inside the caller's transaction: the tracked row is
    /// refreshed from the database, or the snapshot is attached when nothing tracks it. The persisted
    /// status stays authoritative for idempotence.
    /// </summary>
    Task<Registration> GetForUpdateAsync(int registrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads every open (Pending, Confirmed or Waitlisted) registration of the event as a tracked row
    /// ready for a write, ordered by registration id.
    /// </summary>
    Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int userId, DateTime nowUtc, int? eventId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the registration's owner and event identity, or <c>null</c> when the registration does not
    /// exist. Callers use it to discover the write scope before taking locks and re-read it after.
    /// </summary>
    Task<RegistrationIdentity> DiscoverIdentityAsync(int registrationId, CancellationToken cancellationToken = default);
    Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken cancellationToken = default);
    Task<bool> HasEligibleWaitlistAsync(int eventId, CancellationToken cancellationToken = default);
    Task<bool> HasValidAttendanceAsync(int registrationId, CancellationToken cancellationToken = default);

    /// <summary>Reports whether any registration of the event has a non-invalidated attendance row.</summary>
    Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Reports whether any registration of the event has an issued certificate.</summary>
    Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken cancellationToken = default);
    Task<int> CountReviewQueueAsync(ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode status, int? eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken cancellationToken = default);
}

public sealed record UpcomingRegistrationRow(int RegistrationId, int EventId, string EventTitle, DateTime StartsAt);
public sealed record MemberRegistrationRow(int RegistrationId, int MemberId, int EventId, ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode Status);
public sealed record RegistrationIdentity(int UserId, int MemberId, int EventId);
public sealed record RegistrationPromotionCandidate(int RegistrationId, int UserId, int MemberId, int EventId, DateTime? ReviewedAt);
public sealed record MyEventRegistrationRow(int RegistrationId, int EventId, string EventTitle, string TypeName, string VenueName, DateTime StartsAt, ASI.Basecode.Data.Models.Lookups.EventStatusCode EventStatusId, ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode Status);
public sealed record MyPassRow(int RegistrationId, int EventId, string EventTitle, string EventTypeName, string VenueName, DateTime StartsAt, string HolderName, string StudentNumber, string QrToken);
public sealed record OwnedPassRow(int RegistrationId, int EventId, ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode Status, ASI.Basecode.Data.Models.Lookups.EventStatusCode EventStatus, string QrToken);
/// <summary>
/// One confirmed participant in the manual check-in lookup. <see cref="HasAttendance"/> is true for
/// any attendance row; <see cref="InvalidatedAt"/> is that row's invalidation time, or null when the
/// row is valid or absent.
/// </summary>
public sealed record ConfirmedLookupRow(int RegistrationId, string MemberName, string StudentNumber, bool HasAttendance, DateTime? InvalidatedAt);
public sealed record RegistrationReviewRow(int RegistrationId, int EventId, string EventTitle, string MemberName, string StudentNumber, DateTime SubmittedAt, ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode Status)
{
    /// <summary>The course captured at signup, used by the participation report breakdowns.</summary>
    public string Course { get; init; }

    /// <summary>The year level captured at signup, used by the participation report breakdowns.</summary>
    public int YearLevel { get; init; }
}
