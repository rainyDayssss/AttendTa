using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Queries;

public static class ParticipationFacts
{
    public static readonly Expression<Func<User, Member, bool>> EligibleParticipant =
        (user, member) => user.IsActive && member.MembershipStatusId == MembershipStatusCode.Active;

    public static readonly Expression<Func<Attendance, bool>> ValidAttendance =
        attendance => attendance.InvalidatedAt == null;

    public static readonly Expression<Func<Registration, bool>> SeatHeld =
        registration => registration.RegistrationStatusId == RegistrationStatusCode.Confirmed;

    /// <summary>
    /// The SQL twin of <see cref="IsOpenRegistration"/>: a registration that still competes for a seat.
    /// </summary>
    public static readonly Expression<Func<Registration, bool>> OpenRegistration =
        registration => registration.RegistrationStatusId == RegistrationStatusCode.Pending
                        || registration.RegistrationStatusId == RegistrationStatusCode.Confirmed
                        || registration.RegistrationStatusId == RegistrationStatusCode.Waitlisted;

    /// <summary>
    /// In-memory twin of <see cref="ValidAttendance"/>. Every module that already holds the row uses
    /// this instead of re-testing <c>InvalidatedAt</c>, so the rule changes in one place.
    /// </summary>
    public static bool IsValidAttendance(DateTime? invalidatedAt) => invalidatedAt is null;

    /// <summary>In-memory twin of <see cref="ValidAttendance"/> over the attendance entity.</summary>
    public static bool IsValidAttendance(Attendance attendance) =>
        attendance is not null && IsValidAttendance(attendance.InvalidatedAt);

    /// <summary>
    /// The named negation of <see cref="IsValidAttendance(DateTime?)"/>, for a caller whose own
    /// property is "invalidated". Defined in terms of the rule, so the two cannot drift.
    /// </summary>
    public static bool IsInvalidated(DateTime? invalidatedAt) => !IsValidAttendance(invalidatedAt);

    /// <summary>
    /// The one "open registration" rule: a registration that still competes for a seat. Cancellation
    /// and the member's own cancel action use this instead of repeating the status list.
    /// </summary>
    public static bool IsOpenRegistration(RegistrationStatusCode status) =>
        status is RegistrationStatusCode.Pending or RegistrationStatusCode.Confirmed or RegistrationStatusCode.Waitlisted;

    /// <summary>
    /// In-memory twin of <see cref="SeatHeld"/>. Every module that already holds the status uses this
    /// instead of re-testing <c>RegistrationStatusId</c>, so the rule changes in one place.
    /// </summary>
    public static bool HoldsSeat(RegistrationStatusCode status) => status == RegistrationStatusCode.Confirmed;

    /// <summary>In-memory twin of <see cref="SeatHeld"/> over the registration entity.</summary>
    public static bool HoldsSeat(Registration registration) =>
        registration is not null && HoldsSeat(registration.RegistrationStatusId);

    /// <summary>
    /// The one certificate-eligibility rule: a Confirmed seat with valid attendance on a completed
    /// event, whose review is in the state the operation requires — <c>PendingApproval</c> for a
    /// decision, <c>Eligible</c> for issuance. The event is required: a caller that cannot read it must
    /// refuse before calling, because a missing event is not a completed event. The completion hook's
    /// SQL twin is <c>CertificateRepository.GetRegistrationsAwaitingReviewAsync</c>, which selects the
    /// same Confirmed seats with valid attendance and no review yet.
    /// </summary>
    public static bool IsCertificateEligible(
        Registration registration,
        Attendance attendance,
        Certificate certificate,
        CertificateStatusCode requiredStatus,
        Event eventItem) =>
        HoldsSeat(registration)
        && IsValidAttendance(attendance)
        && certificate is not null
        && certificate.CertificateStatusId == requiredStatus
        && eventItem.EventStatusId == EventStatusCode.Completed;

    public static IQueryable<int> EligibleParticipantMemberIds(
        IQueryable<User> users,
        IQueryable<Member> members)
    {
        var user = Expression.Parameter(typeof(User), "user");
        var member = Expression.Parameter(typeof(Member), "member");
        var predicate = Replace(EligibleParticipant.Body, new Dictionary<ParameterExpression, Expression>
        {
            [EligibleParticipant.Parameters[0]] = user,
            [EligibleParticipant.Parameters[1]] = member
        });
        var memberFilter = Expression.Lambda<Func<Member, bool>>(
            Expression.AndAlso(Expression.Equal(Expression.Property(member, nameof(Member.UserId)), Expression.Property(user, nameof(User.Id))), predicate), member);
        var collection = Expression.Lambda<Func<User, IEnumerable<Member>>>(members.Where(memberFilter).Expression, user);
        return users.SelectMany(collection, (outer, inner) => inner.MemberId);
    }

    public static IQueryable<Attendance> ValidAttendances(IQueryable<Attendance> attendance) =>
        attendance.Where(ValidAttendance);

    public static IQueryable<Registration> SeatHolders(IQueryable<Registration> registrations) =>
        registrations.Where(SeatHeld);

    public static IQueryable<Registration> WaitlistPriority(IQueryable<Registration> registrations, int eventId) =>
        registrations.Where(registration => registration.EventId == eventId
                                            && registration.RegistrationStatusId == RegistrationStatusCode.Waitlisted)
            .OrderBy(registration => registration.ReviewedAt)
            .ThenBy(registration => registration.RegistrationId);

    public static Expression<Func<Event, bool>> StartCutoffPassed(DateTime nowUtc) =>
        eventItem => nowUtc >= eventItem.StartsAt;

    public static Expression<Func<Event, bool>> SignupOpen(DateTime nowUtc) =>
        eventItem => eventItem.EventStatusId == EventStatusCode.Published && nowUtc < eventItem.RegistrationDeadline;

    public static Expression<Func<Event, EventType, Venue, bool>> Publishable(DateTime nowUtc) =>
        (eventItem, eventType, venue) => eventItem.EventStatusId == EventStatusCode.Draft
                                         && eventType.IsActive
                                         && venue.IsActive
                                         && eventItem.ImagePath != null
                                         && eventItem.ImagePath.Trim() != ""
                                         && eventItem.ParticipantLimit <= venue.Capacity
                                         && nowUtc < eventItem.RegistrationDeadline;

    public static IQueryable<int> PublishableEventIds(
        IQueryable<Event> events,
        IQueryable<EventType> eventTypes,
        IQueryable<Venue> venues,
        DateTime nowUtc)
    {
        var eventItem = Expression.Parameter(typeof(Event), "eventItem");
        var eventType = Expression.Parameter(typeof(EventType), "eventType");
        var venue = Expression.Parameter(typeof(Venue), "venue");
        var publishable = Publishable(nowUtc);
        var predicate = Replace(publishable.Body, new Dictionary<ParameterExpression, Expression>
        {
            [publishable.Parameters[0]] = eventItem,
            [publishable.Parameters[1]] = eventType,
            [publishable.Parameters[2]] = venue
        });
        // The three-table predicate is rebuilt as a correlated subquery: the venue filter carries the
        // event's venue id, and the event-type filter carries the event's type id, so the whole
        // publishable rule stays one expression tree that EF can translate without client evaluation.
        var venueFilter = Expression.Lambda<Func<Venue, bool>>(
            Expression.AndAlso(Expression.Equal(Expression.Property(venue, nameof(Venue.VenueId)), Expression.Property(eventItem, nameof(Event.VenueId))), predicate), venue);
        var venueIds = venues.Where(venueFilter).Select(Expression.Lambda<Func<Venue, int>>(Expression.Property(eventItem, nameof(Event.EventId)), venue));
        var eventTypeFilter = Expression.Lambda<Func<EventType, bool>>(
            Expression.Equal(Expression.Property(eventType, nameof(EventType.TypeId)), Expression.Property(eventItem, nameof(Event.TypeId))), eventType);
        var eventTypeIds = eventTypes.Where(eventTypeFilter).SelectMany(
            Expression.Lambda<Func<EventType, IEnumerable<int>>>(venueIds.Expression, eventType));
        return events.SelectMany(Expression.Lambda<Func<Event, IEnumerable<int>>>(eventTypeIds.Expression, eventItem));
    }

    private static Expression Replace(Expression expression, IReadOnlyDictionary<ParameterExpression, Expression> replacements) =>
        new ParameterReplacementVisitor(replacements).Visit(expression)!;

    private sealed class ParameterReplacementVisitor(IReadOnlyDictionary<ParameterExpression, Expression> replacements) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            replacements.TryGetValue(node, out var replacement) ? replacement : base.VisitParameter(node);
    }
}
