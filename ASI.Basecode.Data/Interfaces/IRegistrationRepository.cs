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

    /// <summary>Counts confirmed seats for the event using the canonical seat fact.</summary>
    Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default);

    Task AddAsync(Registration registration, CancellationToken cancellationToken = default);

    Task<Registration> GetForCancellationAsync(int registrationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int userId, DateTime nowUtc, int? eventId = null, CancellationToken cancellationToken = default);
    Task<RegistrationReviewIdentity> DiscoverReviewAsync(int registrationId, CancellationToken cancellationToken = default);
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
public sealed record RegistrationReviewIdentity(int UserId, int MemberId, int EventId);
public sealed record RegistrationPromotionCandidate(int RegistrationId, int UserId, int MemberId, int EventId, DateTime? ReviewedAt);
public sealed record MyEventRegistrationRow(int RegistrationId, int EventId, string EventTitle, string TypeName, string VenueName, DateTime StartsAt, ASI.Basecode.Data.Models.Lookups.EventStatusCode EventStatusId, ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode Status);
public sealed record RegistrationReviewRow(int RegistrationId, int EventId, string EventTitle, string MemberName, string StudentNumber, DateTime SubmittedAt, ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode Status)
{
    /// <summary>The course captured at signup, used by the participation report breakdowns.</summary>
    public string Course { get; init; }

    /// <summary>The year level captured at signup, used by the participation report breakdowns.</summary>
    public int YearLevel { get; init; }
}
