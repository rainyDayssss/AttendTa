using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public sealed class ParticipationFactsTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void SignupBoundaryUsesClockTicks(long offsetTicks, bool expectedOpen)
    {
        var deadline = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(deadline.AddTicks(offsetTicks));
        var eventItem = Event(deadline.UtcDateTime, EventStatusCode.Published);
        eventItem.RegistrationDeadline = deadline.UtcDateTime;

        Assert.Equal(expectedOpen, ParticipationFacts.SignupOpen(clock.GetUtcNow().UtcDateTime).Compile()(eventItem));
    }

    [Fact]
    public void ExpressionsApplyExactEligibilityAttendanceSeatCutoffSignupAndPublishBoundaries()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var now = clock.GetUtcNow().UtcDateTime;
        var eligible = ParticipationFacts.EligibleParticipant.Compile();
        var validAttendance = ParticipationFacts.ValidAttendance.Compile();
        var seatHeld = ParticipationFacts.SeatHeld.Compile();
        var startCutoff = ParticipationFacts.StartCutoffPassed(now).Compile();
        var signupOpen = ParticipationFacts.SignupOpen(now).Compile();
        var publishable = ParticipationFacts.Publishable(now).Compile();
        var activeMember = new Member { MembershipStatusId = MembershipStatusCode.Active };

        Assert.True(eligible(new User { IsActive = true }, activeMember));
        Assert.False(eligible(new User { IsActive = false }, activeMember));
        Assert.False(eligible(new User { IsActive = true }, new Member { MembershipStatusId = MembershipStatusCode.Inactive }));
        Assert.True(validAttendance(new Attendance()));
        Assert.False(validAttendance(new Attendance { InvalidatedAt = now }));
        Assert.True(seatHeld(new Registration { RegistrationStatusId = RegistrationStatusCode.Confirmed }));
        Assert.False(seatHeld(new Registration { RegistrationStatusId = RegistrationStatusCode.Waitlisted }));

        var eventItem = Event(now, EventStatusCode.Published);
        eventItem.StartsAt = now.AddTicks(1);
        Assert.False(startCutoff(eventItem));
        eventItem.StartsAt = now;
        Assert.True(startCutoff(eventItem));
        eventItem.StartsAt = now.AddTicks(-1);
        Assert.True(startCutoff(eventItem));
        Assert.True(signupOpen(eventItem));
        eventItem.RegistrationDeadline = now;
        Assert.False(signupOpen(eventItem));

        eventItem.EventStatusId = EventStatusCode.Draft;
        eventItem.ImagePath = "uploads/events/cover.jpg";
        eventItem.RegistrationDeadline = now.AddTicks(1);
        Assert.True(publishable(eventItem, new EventType { IsActive = true }, new Venue { IsActive = true, Capacity = 10 }));
        eventItem.ParticipantLimit = 11;
        Assert.False(publishable(eventItem, new EventType { IsActive = true }, new Venue { IsActive = true, Capacity = 10 }));
    }

    [Fact]
    public void WaitlistNoShowAndAttendanceRateApplyOrderingInvalidationAndZeroRules()
    {
        var registrations = new[]
        {
            new Registration { RegistrationId = 3, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Waitlisted, ReviewedAt = new DateTime(2026, 3, 1) },
            new Registration { RegistrationId = 2, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Waitlisted, ReviewedAt = new DateTime(2026, 3, 1) },
            new Registration { RegistrationId = 4, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Confirmed },
            new Registration { RegistrationId = 5, EventId = 2, RegistrationStatusId = RegistrationStatusCode.Confirmed },
            new Registration { RegistrationId = 6, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Confirmed },
            new Registration { RegistrationId = 7, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Confirmed }
        }.AsQueryable();
        var events = new[]
        {
            Event(DateTime.UtcNow, EventStatusCode.Completed, 1),
            Event(DateTime.UtcNow, EventStatusCode.Completed, 2),
            Event(DateTime.UtcNow, EventStatusCode.Completed, 3)
        }.AsQueryable();
        var attendance = new[] { new Attendance { RegistrationId = 5 }, new Attendance { RegistrationId = 6 }, new Attendance { RegistrationId = 4, InvalidatedAt = DateTime.UtcNow } }.AsQueryable();

        Assert.Equal(new[] { 2, 3 }, ParticipationFacts.WaitlistPriority(registrations, 1).Select(row => row.RegistrationId));
        Assert.Equal(new[] { 4, 5, 6, 7 }, ParticipationFacts.SeatHolders(registrations).Select(row => row.RegistrationId));
        Assert.Equal(new[] { 5, 6 }, ParticipationFacts.ValidAttendances(attendance).Select(row => row.RegistrationId));
        Assert.Equal(new[] { 4, 7 }, ParticipationFacts.NoShows(registrations, events, attendance).Select(row => row.RegistrationId));
        Assert.Equal(new decimal?[] { 1m / 3m, 1m, null }, ParticipationFacts.AttendanceRates(events, registrations, attendance).ToArray());
    }

    [Fact]
    public void NoShowsReturnOnlyConfirmedRowsOfCompletedEvents()
    {
        var registrations = new[]
        {
            new Registration { RegistrationId = 1, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Pending },
            new Registration { RegistrationId = 2, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Waitlisted },
            new Registration { RegistrationId = 3, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Cancelled },
            new Registration { RegistrationId = 4, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Confirmed },
            new Registration { RegistrationId = 5, EventId = 1, RegistrationStatusId = RegistrationStatusCode.Confirmed }
        }.AsQueryable();
        var events = new[] { Event(DateTime.UtcNow, EventStatusCode.Completed, 1) }.AsQueryable();
        var attendance = new[] { new Attendance { RegistrationId = 5 } }.AsQueryable();

        var noShows = ParticipationFacts.NoShows(registrations, events, attendance).Select(row => row.RegistrationId);

        Assert.Equal(new[] { 4 }, noShows);
    }

    private static Event Event(DateTime now, EventStatusCode status, int id = 1) => new()
    {
        EventId = id,
        EventStatusId = status,
        StartsAt = now.AddHours(1),
        RegistrationDeadline = now.AddHours(1),
        ParticipantLimit = 10
    };

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
