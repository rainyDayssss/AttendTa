using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public sealed class MemberEventServiceTests
{
    private static readonly IMapper Mapper = new MapperConfiguration(config =>
    {
        config.CreateMap<PublishedEventListItem, MemberEventListItemViewModel>();
        config.CreateMap<PublishedEventDetail, MemberEventDetailViewModel>();
    }).CreateMapper();

    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FeedPagesOnlyPublishedEventsInStartThenIdOrderAndKeepsClosedEvents()
    {
        var harness = new Harness();
        harness.AddUser(RoleCode.Member, isActive: true);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var draft = harness.AddEvent("Draft event", type.TypeId, venue.VenueId, EventStatusCode.Draft, Now.AddDays(5), Now.AddDays(4));
        var closed = harness.AddEvent("Closed event", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(1), Now);
        for (var index = 1; index <= 26; index++)
        {
            // Two rows share a start so the event id tiebreak decides their order.
            var startsAt = Now.AddDays(10).AddHours(index <= 25 ? index : 25);
            harness.AddEvent($"Published {index:D2}", type.TypeId, venue.VenueId, EventStatusCode.Published, startsAt, startsAt.AddHours(-1));
        }

        var first = await harness.Service.GetFeedAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(27, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(25, first.Items.Count);
        Assert.DoesNotContain(first.Items, item => item.EventId == draft.EventId);
        Assert.Contains(first.Items, item => item.EventId == closed.EventId);
        Assert.False(first.Items.Single(item => item.EventId == closed.EventId).IsSignupOpen);
        Assert.Equal(first.Items.OrderBy(item => item.StartsAt).ThenBy(item => item.EventId).Select(item => item.EventId), first.Items.Select(item => item.EventId));

        var second = await harness.Service.GetFeedAsync(2, TestContext.Current.CancellationToken);
        Assert.Equal(2, second.Items.Count);

        var pastEnd = await harness.Service.GetFeedAsync(999, TestContext.Current.CancellationToken);
        Assert.Equal(2, pastEnd.PageNumber);
        Assert.Equal(2, pastEnd.Items.Count);

        var invalid = await harness.Service.GetFeedAsync(0, TestContext.Current.CancellationToken);
        Assert.Equal(1, invalid.PageNumber);
    }

    [Fact]
    public async Task EmptyFeedReturnsPageOneOfZeroAndNoItems()
    {
        var harness = new Harness();
        harness.AddUser(RoleCode.Member, isActive: true);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        harness.AddEvent("Draft only", type.TypeId, venue.VenueId, EventStatusCode.Draft, Now.AddDays(2), Now.AddDays(1));

        var page = await harness.Service.GetFeedAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(1, page.PageNumber);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task FeedProjectsCoverAsABooleanAndNeverExposesThePrivatePath()
    {
        var harness = new Harness();
        harness.AddUser(RoleCode.Member, isActive: true);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        harness.AddEvent("With cover", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1),
            imagePath: "uploads/events/0123456789abcdef0123456789abcdef.jpg");
        harness.AddEvent("Without cover", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(3), Now.AddDays(2));

        var page = await harness.Service.GetFeedAsync(1, TestContext.Current.CancellationToken);

        Assert.True(page.Items.Single(item => item.Title == "With cover").HasCover);
        Assert.False(page.Items.Single(item => item.Title == "Without cover").HasCover);
        Assert.Null(typeof(MemberEventListItemViewModel).GetProperty("ImagePath"));
        Assert.Null(typeof(MemberEventDetailViewModel).GetProperty("ImagePath"));
    }

    [Fact]
    public async Task RegisterCreatesOnePendingRowWithSnapshotsAndNoSeatOrQr()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active, course: "BSIT", yearLevel: 2, section: "B");
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Open event", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4));

        var result = await harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        var row = Assert.Single(harness.Registrations.Items);
        Assert.Equal(result.RegistrationId, row.RegistrationId);
        Assert.Equal(member.MemberId, row.MemberId);
        Assert.Equal(eventItem.EventId, row.EventId);
        Assert.Equal(RegistrationStatusCode.Pending, row.RegistrationStatusId);
        Assert.Equal("BSIT", row.CourseSnapshot);
        Assert.Equal(2, row.YearLevelSnapshot);
        Assert.Equal("B", row.SectionSnapshot);
        Assert.Equal(Now, row.SubmittedAt);
        Assert.Equal(actor.Id, row.CreatedBy);
        Assert.Equal(Now, row.CreatedAt);
        Assert.Null(row.QrToken);
        Assert.Null(row.ConfirmedBy);
        Assert.Null(row.ConfirmedAt);
        Assert.Null(row.ReviewedBy);
        Assert.Null(row.ReviewedAt);
        Assert.False(result.SeatNotGuaranteed);
        Assert.Equal(new[] { "user", "event" }, harness.Locks);
    }

    [Fact]
    public async Task FullEventStillCreatesPendingAndWarnsThatNoSeatIsGuaranteed()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Full event", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4), participantLimit: 2);
        for (var index = 0; index < 2; index++)
        {
            var other = harness.AddUser(RoleCode.Member, isActive: true);
            harness.AddMember(other.Id, MembershipStatusCode.Active);
            harness.AddRegistration(eventItem.EventId, harness.Members.Items[^1].MemberId, RegistrationStatusCode.Confirmed);
        }

        var result = await harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        Assert.True(result.SeatNotGuaranteed);
        Assert.Equal(3, harness.Registrations.Items.Count);
        Assert.Equal(RegistrationStatusCode.Pending, harness.Registrations.Items[^1].RegistrationStatusId);
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Pending)]
    [InlineData(RegistrationStatusCode.Confirmed)]
    [InlineData(RegistrationStatusCode.Waitlisted)]
    [InlineData(RegistrationStatusCode.Cancelled)]
    public async Task RegisterRefusesADuplicateOfAnyStatus(RegistrationStatusCode status)
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Duplicate event", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4));
        harness.AddRegistration(eventItem.EventId, member.MemberId, status);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, error.Outcome);
        Assert.Single(harness.Registrations.Items);
    }

    [Fact]
    public async Task RegisterRefusesAtTheExactDeadlineAndForNonPublishedEvents()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var atDeadline = harness.AddEvent("At deadline", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now);
        var draft = harness.AddEvent("Draft", type.TypeId, venue.VenueId, EventStatusCode.Draft, Now.AddDays(5), Now.AddDays(4));
        var cancelled = harness.AddEvent("Cancelled", type.TypeId, venue.VenueId, EventStatusCode.Cancelled, Now.AddDays(5), Now.AddDays(4));

        foreach (var eventId in new[] { atDeadline.EventId, draft.EventId, cancelled.EventId })
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                harness.Service.RegisterAsync(eventId, TestContext.Current.CancellationToken));
            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
        }

        Assert.Empty(harness.Registrations.Items);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task RegisterUsesTickPrecisionAfterBothLocks(long offsetTicks, bool succeeds)
    {
        var clock = new FixedClock { Now = Now.AddMinutes(-1) };
        var harness = new Harness(clock);
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Boundary", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(1), Now);
        harness.Events.BeforeLock = _ => clock.Now = Now.AddTicks(offsetTicks);

        if (succeeds)
        {
            await harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken);
            var row = Assert.Single(harness.Registrations.Items);
            Assert.Equal(member.MemberId, row.MemberId);
            Assert.Equal(RegistrationStatusCode.Pending, row.RegistrationStatusId);
            Assert.Equal(Now.AddTicks(-1), row.SubmittedAt);
        }
        else
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken));
            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
            Assert.Empty(harness.Registrations.Items);
        }

        Assert.Equal(new[] { "user", "event" }, harness.Locks);
    }

    [Fact]
    public async Task RegisterCapturesTimeAfterBothLocks()
    {
        var clock = new FixedClock { Now = Now };
        var harness = new Harness(clock);
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Late clock", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4));
        var afterLocks = Now.AddMinutes(30);
        harness.Events.BeforeLock = _ => clock.Now = afterLocks;

        await harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        Assert.Equal(afterLocks, Assert.Single(harness.Registrations.Items).SubmittedAt);
    }

    [Fact]
    public async Task RegisterRechecksTheDeadlineAgainstTheLockedEvent()
    {
        var clock = new FixedClock { Now = Now };
        var harness = new Harness(clock);
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Stale deadline", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddMinutes(15));
        harness.Events.BeforeLock = _ => clock.Now = Now.AddMinutes(30);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
        Assert.Empty(harness.Registrations.Items);
    }

    [Fact]
    public async Task RegisterRefusesIneligibleAccountsAndMissingProfiles()
    {
        var harness = new Harness();
        var inactiveUser = harness.AddUser(RoleCode.Member, isActive: false);
        harness.AddMember(inactiveUser.Id, MembershipStatusCode.Active);
        var inactiveMemberUser = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(inactiveMemberUser.Id, MembershipStatusCode.Inactive);
        var missingProfile = harness.AddUser(RoleCode.Member, isActive: true);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Eligibility", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4));

        foreach (var userId in new[] { inactiveUser.Id, inactiveMemberUser.Id, missingProfile.Id })
        {
            harness.Actor.UserId = userId;
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken));
            Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        }

        Assert.Empty(harness.Registrations.Items);
    }

    [Fact]
    public async Task AnonymousRegistrationIsForbiddenAndWritesNothing()
    {
        var harness = new Harness(actor: new FixedActor(0, RoleCodes.Member, isAuthenticated: false));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RegisterAsync(1, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.Empty(harness.Registrations.Items);
    }

    [Fact]
    public async Task MissingEventIsNotFoundAndWritesNothing()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(actor.Id, MembershipStatusCode.Active);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RegisterAsync(404, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
        Assert.Empty(harness.Registrations.Items);
        Assert.Equal(new[] { "user", "event" }, harness.Locks);
    }

    [Theory]
    [InlineData(RoleCode.Member)]
    [InlineData(RoleCode.Officer)]
    [InlineData(RoleCode.Admin)]
    public async Task EveryRoleWithAnEligibleMemberProfileCanRegister(RoleCode role)
    {
        var harness = new Harness(actor: new FixedActor(0, RoleCodes.Member));
        var actor = harness.AddUser(role, isActive: true);
        harness.Actor.UserId = actor.Id;
        harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("All roles", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4));

        await harness.Service.RegisterAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        Assert.Single(harness.Registrations.Items);
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Pending)]
    [InlineData(RegistrationStatusCode.Confirmed)]
    [InlineData(RegistrationStatusCode.Waitlisted)]
    public async Task CancelRegistrationCancelsTheOwnersOpenRowThroughTheCancellationService(RegistrationStatusCode status)
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Cancelable", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));
        var registration = harness.AddRegistration(eventItem.EventId, member.MemberId, status);
        registration.QrToken = new string('a', 64);

        await harness.Service.CancelRegistrationAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        var call = Assert.Single(harness.Cancellation.Calls);
        Assert.Equal(registration.RegistrationId, call.RegistrationId);
        Assert.Equal(CancellationCause.MemberRequested, call.Cause);
        Assert.Null(call.Reason);
        Assert.Equal(RegistrationStatusCode.Cancelled, registration.RegistrationStatusId);
        Assert.Null(registration.QrToken);
        Assert.Equal(new[] { "user", "event" }, harness.Locks);
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Pending, -1, true)]
    [InlineData(RegistrationStatusCode.Pending, 0, false)]
    [InlineData(RegistrationStatusCode.Pending, 1, false)]
    [InlineData(RegistrationStatusCode.Confirmed, -1, true)]
    [InlineData(RegistrationStatusCode.Confirmed, 0, false)]
    [InlineData(RegistrationStatusCode.Confirmed, 1, false)]
    [InlineData(RegistrationStatusCode.Waitlisted, -1, true)]
    [InlineData(RegistrationStatusCode.Waitlisted, 0, false)]
    [InlineData(RegistrationStatusCode.Waitlisted, 1, false)]
    public async Task CancelRegistrationUsesTickPrecisionAfterBothLocks(RegistrationStatusCode status, long offsetTicks, bool succeeds)
    {
        var clock = new FixedClock { Now = Now.AddTicks(-1) };
        var harness = new Harness(clock);
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        // The event starts exactly at Now, so only the clock captured after both locks decides.
        var eventItem = harness.AddEvent("Boundary", type.TypeId, venue.VenueId, EventStatusCode.Published, Now, Now.AddDays(-1));
        var registration = harness.AddRegistration(eventItem.EventId, member.MemberId, status);
        registration.QrToken = status == RegistrationStatusCode.Confirmed ? new string('a', 64) : null;
        harness.Events.BeforeLock = _ => clock.Now = Now.AddTicks(offsetTicks);

        if (succeeds)
        {
            await harness.Service.CancelRegistrationAsync(eventItem.EventId, TestContext.Current.CancellationToken);
            var call = Assert.Single(harness.Cancellation.Calls);
            Assert.Equal(registration.RegistrationId, call.RegistrationId);
            Assert.Equal(CancellationCause.MemberRequested, call.Cause);
            Assert.Equal(RegistrationStatusCode.Cancelled, registration.RegistrationStatusId);
            Assert.Null(registration.QrToken);
        }
        else
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                harness.Service.CancelRegistrationAsync(eventItem.EventId, TestContext.Current.CancellationToken));
            Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
            Assert.Empty(harness.Cancellation.Calls);
            Assert.Equal(status, registration.RegistrationStatusId);
            Assert.Equal(status == RegistrationStatusCode.Confirmed ? new string('a', 64) : null, registration.QrToken);
        }

        Assert.Equal(new[] { "user", "event" }, harness.Locks);

        var detail = await harness.Service.GetDetailAsync(eventItem.EventId, TestContext.Current.CancellationToken);
        Assert.Equal(!succeeds, detail.IsStartCutoffPassed);
    }

    [Fact]
    public async Task CancelRegistrationRefusesValidAttendanceWithoutCancelling()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Attended", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));
        var registration = harness.AddRegistration(eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed);
        harness.Registrations.ValidAttendanceRegistrationIds.Add(registration.RegistrationId);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.CancelRegistrationAsync(eventItem.EventId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Empty(harness.Cancellation.Calls);
        Assert.Equal(RegistrationStatusCode.Confirmed, registration.RegistrationStatusId);
    }

    [Fact]
    public async Task CancelRegistrationRefusesAnotherMembersRegistrationAsNotFound()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var other = harness.AddUser(RoleCode.Member, isActive: true);
        var otherMember = harness.AddMember(other.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Someone else", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));
        var otherRegistration = harness.AddRegistration(eventItem.EventId, otherMember.MemberId, RegistrationStatusCode.Confirmed);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.CancelRegistrationAsync(eventItem.EventId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
        Assert.Empty(harness.Cancellation.Calls);
        Assert.Equal(RegistrationStatusCode.Confirmed, otherRegistration.RegistrationStatusId);
        Assert.Empty(harness.Locks);
    }

    [Fact]
    public async Task CancelRegistrationRefusesMissingRegistrationWithoutLocking()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("No row", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.CancelRegistrationAsync(eventItem.EventId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
        Assert.Empty(harness.Cancellation.Calls);
        Assert.Empty(harness.Locks);
    }

    [Theory]
    [InlineData(EventStatusCode.Draft)]
    [InlineData(EventStatusCode.Cancelled)]
    [InlineData(EventStatusCode.Completed)]
    public async Task CancelRegistrationRefusesAnEventThatIsNoLongerPublishedAsStale(EventStatusCode status)
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Not published", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));
        var registration = harness.AddRegistration(eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed);
        harness.Events.BeforeLock = item => item.EventStatusId = status;

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.CancelRegistrationAsync(eventItem.EventId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.Empty(harness.Cancellation.Calls);
        Assert.Equal(RegistrationStatusCode.Confirmed, registration.RegistrationStatusId);
        Assert.Equal(new[] { "user", "event" }, harness.Locks);
    }

    [Fact]
    public async Task CancelRegistrationRefusesAnAlreadyCancelledRowAsStale()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Already cancelled", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));
        var registration = harness.AddRegistration(eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed);
        // The row changes between discovery and the post-lock recheck.
        harness.Events.BeforeLock = _ => registration.RegistrationStatusId = RegistrationStatusCode.Cancelled;

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.CancelRegistrationAsync(eventItem.EventId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.Empty(harness.Cancellation.Calls);
        Assert.Equal(RegistrationStatusCode.Cancelled, registration.RegistrationStatusId);
    }

    [Fact]
    public async Task AnonymousCancellationIsForbiddenAndWritesNothing()
    {
        var harness = new Harness(actor: new FixedActor(0, RoleCodes.Member, isAuthenticated: false));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.CancelRegistrationAsync(1, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.Empty(harness.Cancellation.Calls);
        Assert.Empty(harness.Locks);
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Pending, true)]
    [InlineData(RegistrationStatusCode.Confirmed, true)]
    [InlineData(RegistrationStatusCode.Waitlisted, true)]
    [InlineData(RegistrationStatusCode.Cancelled, false)]
    public async Task DetailOffersCancellationOnlyForAnOpenRegistrationBeforeStart(RegistrationStatusCode status, bool canCancel)
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Detail cancel", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));
        var registration = harness.AddRegistration(eventItem.EventId, member.MemberId, status);

        var detail = await harness.Service.GetDetailAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        Assert.True(detail.HasRegistration);
        Assert.Equal(registration.RegistrationId, detail.RegistrationId);
        Assert.Equal(status, detail.RegistrationStatus);
        Assert.Equal(canCancel, detail.CanCancel);
    }

    [Fact]
    public async Task DetailHidesCancellationAfterStartOrWithValidAttendance()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var atStart = harness.AddEvent("Started", type.TypeId, venue.VenueId, EventStatusCode.Published, Now, Now.AddDays(-1));
        harness.AddRegistration(atStart.EventId, member.MemberId, RegistrationStatusCode.Confirmed);
        var attended = harness.AddEvent("Attended", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));
        var attendedRow = harness.AddRegistration(attended.EventId, member.MemberId, RegistrationStatusCode.Confirmed);
        harness.Registrations.ValidAttendanceRegistrationIds.Add(attendedRow.RegistrationId);

        Assert.False((await harness.Service.GetDetailAsync(atStart.EventId, TestContext.Current.CancellationToken)).CanCancel);
        Assert.False((await harness.Service.GetDetailAsync(attended.EventId, TestContext.Current.CancellationToken)).CanCancel);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task DetailReportsTheStartCutoffFlagFromTheCapturedClock(long offsetTicks, bool expected)
    {
        var clock = new FixedClock { Now = Now.AddTicks(offsetTicks) };
        var harness = new Harness(clock);
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Started", type.TypeId, venue.VenueId, EventStatusCode.Published, Now, Now.AddDays(-1));
        var registration = harness.AddRegistration(eventItem.EventId, member.MemberId, RegistrationStatusCode.Pending);

        var detail = await harness.Service.GetDetailAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        Assert.Equal(expected, detail.IsStartCutoffPassed);
        Assert.Equal(RegistrationStatusCode.Pending, detail.RegistrationStatus);
        Assert.Equal(RegistrationStatusCode.Pending, registration.RegistrationStatusId);
        Assert.Equal(!expected, detail.CanCancel);
    }

    [Theory]
    [InlineData(RegistrationStatusCode.Pending)]
    [InlineData(RegistrationStatusCode.Waitlisted)]
    public async Task DetailDerivesTheCutoffLabelWithoutChangingTheStoredStatus(RegistrationStatusCode status)
    {
        var harness = new Harness(new FixedClock { Now = Now });
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Started", type.TypeId, venue.VenueId, EventStatusCode.Published, Now, Now.AddDays(-1));
        var registration = harness.AddRegistration(eventItem.EventId, member.MemberId, status);

        var detail = await harness.Service.GetDetailAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        Assert.True(detail.IsStartCutoffPassed);
        Assert.Equal(status, detail.RegistrationStatus);
        Assert.Equal(status, registration.RegistrationStatusId);
        Assert.Equal(RegistrationStatusDisplay.NotConfirmedBeforeCutoff, RegistrationStatusDisplay.Label(detail.RegistrationStatus, detail.IsStartCutoffPassed));
        Assert.Equal("badge-closed", RegistrationStatusDisplay.BadgeClass(detail.RegistrationStatus, detail.IsStartCutoffPassed));
        Assert.False(detail.CanCancel);
    }

    [Fact]
    public async Task DetailStillOffersCancellationToADeactivatedMember()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Inactive);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Inactive member", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(2), Now.AddDays(1));
        harness.AddRegistration(eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed);

        var detail = await harness.Service.GetDetailAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        Assert.True(detail.HasRegistration);
        Assert.True(detail.CanCancel);
        Assert.False(detail.CanRegister);
    }

    [Fact]
    public async Task DetailHidesDraftsAndReportsSignupRegistrationAndFullState()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        var member = harness.AddMember(actor.Id, MembershipStatusCode.Active);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var draft = harness.AddEvent("Draft", type.TypeId, venue.VenueId, EventStatusCode.Draft, Now.AddDays(5), Now.AddDays(4));
        var open = harness.AddEvent("Open", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4), participantLimit: 1);
        var closed = harness.AddEvent("Closed", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now);
        var registered = harness.AddEvent("Registered", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4));
        harness.AddRegistration(registered.EventId, member.MemberId, RegistrationStatusCode.Cancelled);

        Assert.Null(await harness.Service.GetDetailAsync(draft.EventId, TestContext.Current.CancellationToken));

        var openDetail = await harness.Service.GetDetailAsync(open.EventId, TestContext.Current.CancellationToken);
        Assert.True(openDetail.IsSignupOpen);
        Assert.True(openDetail.CanRegister);
        Assert.False(openDetail.IsFull);
        Assert.False(openDetail.HasRegistration);
        Assert.Null(openDetail.RegistrationId);
        Assert.Null(openDetail.RegistrationStatus);
        Assert.False(openDetail.CanCancel);

        var closedDetail = await harness.Service.GetDetailAsync(closed.EventId, TestContext.Current.CancellationToken);
        Assert.False(closedDetail.IsSignupOpen);
        Assert.False(closedDetail.CanRegister);

        var registeredDetail = await harness.Service.GetDetailAsync(registered.EventId, TestContext.Current.CancellationToken);
        Assert.True(registeredDetail.HasRegistration);
        Assert.NotNull(registeredDetail.RegistrationId);
        Assert.Equal(RegistrationStatusCode.Cancelled, registeredDetail.RegistrationStatus);
        Assert.False(registeredDetail.CanCancel);
        Assert.False(registeredDetail.CanRegister);

        var other = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(other.Id, MembershipStatusCode.Active);
        harness.AddRegistration(open.EventId, harness.Members.Items[^1].MemberId, RegistrationStatusCode.Confirmed);
        var fullDetail = await harness.Service.GetDetailAsync(open.EventId, TestContext.Current.CancellationToken);
        Assert.True(fullDetail.IsFull);
        Assert.True(fullDetail.CanRegister);
    }

    [Fact]
    public async Task DetailDoesNotOfferRegistrationToAnIneligibleActor()
    {
        var harness = new Harness();
        var actor = harness.AddUser(RoleCode.Member, isActive: true);
        harness.AddMember(actor.Id, MembershipStatusCode.Inactive);
        var type = harness.AddType("Workshop");
        var venue = harness.AddVenue("Room A");
        var eventItem = harness.AddEvent("Ineligible detail", type.TypeId, venue.VenueId, EventStatusCode.Published, Now.AddDays(5), Now.AddDays(4));

        var detail = await harness.Service.GetDetailAsync(eventItem.EventId, TestContext.Current.CancellationToken);

        Assert.False(detail.CanRegister);
        Assert.False(detail.HasRegistration);
    }

    [Fact]
    public async Task FeedAndDetailRequireAuthentication()
    {
        var harness = new Harness(actor: new FixedActor(0, RoleCodes.Member, isAuthenticated: false));

        Assert.Equal(OutcomeCode.Forbidden, (await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.GetFeedAsync(1, TestContext.Current.CancellationToken))).Outcome);
        Assert.Equal(OutcomeCode.Forbidden, (await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.GetDetailAsync(1, TestContext.Current.CancellationToken))).Outcome);
    }

    private sealed class Harness
    {
        public Harness(TimeProvider? clock = null, ICurrentActor? actor = null)
        {
            Clock = clock ?? new FixedClock { Now = Now };
            Actor = actor as FixedActor ?? new FixedActor(0, RoleCodes.Member);
            Events = new FakeEvents(Locks);
            Members = new FakeMembers();
            Registrations = new FakeRegistrations(Members);
            Users = new FakeUsers(Locks);
            Cancellation = new FakeCancellation(Registrations);
            Events.ConfirmedCount = eventId => Registrations.Items.Count(row =>
                row.EventId == eventId && row.RegistrationStatusId == RegistrationStatusCode.Confirmed);
            Service = new MemberEventService(
                Events,
                Registrations,
                Members,
                Users,
                new ImmediateUnitOfWork(),
                Clock,
                Actor,
                Cancellation,
                Mapper);
        }

        public TimeProvider Clock { get; }
        public FixedActor Actor { get; }
        public FakeEvents Events { get; }
        public FakeRegistrations Registrations { get; }
        public FakeMembers Members { get; }
        public FakeUsers Users { get; }
        public FakeCancellation Cancellation { get; }
        public MemberEventService Service { get; }
        public List<string> Locks { get; } = [];

        public User AddUser(RoleCode role, bool isActive)
        {
            var user = new User
            {
                Id = Users.Items.Count + 1,
                UserId = $"user-{Users.Items.Count + 1}@attendta.test",
                Name = $"User {Users.Items.Count + 1}",
                Password = string.Empty,
                RoleId = role,
                IsActive = isActive
            };
            Users.Items.Add(user);
            if (Actor.UserId == 0)
            {
                Actor.UserId = user.Id;
            }

            return user;
        }

        public Member AddMember(int userId, MembershipStatusCode status, string course = "BSIT", int yearLevel = 1, string section = "A")
        {
            var member = new Member
            {
                MemberId = Members.Items.Count + 1,
                UserId = userId,
                StudentNumber = $"SN-{Members.Items.Count + 1}",
                Course = course,
                YearLevel = yearLevel,
                Section = section,
                MembershipStatusId = status,
                CreatedBy = userId,
                CreatedAt = Now
            };
            Members.Items.Add(member);
            return member;
        }

        public EventType AddType(string name)
        {
            var type = new EventType { TypeId = Events.TypeNames.Count + 1, TypeName = name, Description = name, IsActive = true, CreatedBy = 1, CreatedAt = Now };
            Events.TypeNames[type.TypeId] = name;
            return type;
        }

        public Venue AddVenue(string roomName)
        {
            var venue = new Venue { VenueId = Events.VenueNames.Count + 1, RoomName = roomName, Capacity = 50, ImagePath = "uploads/venues/test.jpg", IsActive = true, CreatedBy = 1, CreatedAt = Now };
            Events.VenueNames[venue.VenueId] = roomName;
            return venue;
        }

        public Event AddEvent(
            string title,
            int typeId,
            int venueId,
            EventStatusCode status,
            DateTime startsAt,
            DateTime deadline,
            int participantLimit = 10,
            string? imagePath = null)
        {
            var eventItem = new Event
            {
                EventId = Events.Items.Count + 1,
                Title = title,
                Description = $"About {title}",
                TypeId = typeId,
                VenueId = venueId,
                StartsAt = startsAt,
                RegistrationDeadline = deadline,
                ParticipantLimit = participantLimit,
                EventStatusId = status,
                ImagePath = imagePath,
                CreatedBy = 1,
                CreatedAt = Now
            };
            Events.Items.Add(eventItem);
            return eventItem;
        }

        public Registration AddRegistration(int eventId, int memberId, RegistrationStatusCode status)
        {
            var registration = new Registration
            {
                RegistrationId = Registrations.Items.Count + 1,
                EventId = eventId,
                MemberId = memberId,
                RegistrationStatusId = status,
                CourseSnapshot = "BSIT",
                YearLevelSnapshot = 1,
                SectionSnapshot = "A",
                SubmittedAt = Now,
                CreatedBy = 1,
                CreatedAt = Now
            };
            Registrations.Items.Add(registration);
            return registration;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTime Now { get; set; }
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
    }

    private sealed class FixedActor(int userId, string role, bool isAuthenticated = true) : ICurrentActor
    {
        public int UserId { get; set; } = userId;
        public bool IsAuthenticated => isAuthenticated;
        public string Role => role;
    }

    private sealed class ImmediateUnitOfWork : IUnitOfWork
    {
        public DbContext Database => throw new NotSupportedException();
        public void SaveChanges() { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) => work(cancellationToken);
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default) => work(cancellationToken);
    }

    private sealed class FakeEvents(List<string> locks) : IEventRepository
    {
        public List<Event> Items { get; } = [];
        public Dictionary<int, string> TypeNames { get; } = [];
        public Dictionary<int, string> VenueNames { get; } = [];
        public Func<int, int> ConfirmedCount { get; set; } = _ => 0;
        public Action<Event>? BeforeLock { get; set; }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

        public Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventListItem>>([]);

        public Task<Event> GetEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.EventId == eventId)!);

        public Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            locks.Add("event");
            var item = Items.FirstOrDefault(row => row.EventId == eventId);
            if (item != null)
            {
                BeforeLock?.Invoke(item);
            }

            return Task.FromResult(item!);
        }

        public Task AddEventAsync(Event eventItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateEventAsync(Event eventItem, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Count(item => item.EventStatusId == EventStatusCode.Published));

        public Task<int> CountMemberVisibleAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Count(item => IsMemberVisible(item, includeCompleted: true)));

        public Task<IReadOnlyList<PublishedEventListItem>> GetPublishedPageAsync(int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PublishedEventListItem>>(Page(skip, take, includeCompleted: false));

        public Task<IReadOnlyList<PublishedEventListItem>> GetMemberVisiblePageAsync(int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PublishedEventListItem>>(Page(skip, take, includeCompleted: true));

        public Task<PublishedEventDetail> GetPublishedEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Detail(eventId, includeCompleted: false));

        public Task<PublishedEventDetail> GetMemberVisibleEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Detail(eventId, includeCompleted: true));

        private static bool IsMemberVisible(Event item, bool includeCompleted) =>
            item.EventStatusId == EventStatusCode.Published
            || (includeCompleted && item.EventStatusId == EventStatusCode.Completed);

        private List<PublishedEventListItem> Page(int skip, int take, bool includeCompleted) =>
            Items
                .Where(item => IsMemberVisible(item, includeCompleted))
                .OrderBy(item => item.StartsAt)
                .ThenBy(item => item.EventId)
                .Skip(skip)
                .Take(take)
                .Select(item => new PublishedEventListItem
                {
                    EventId = item.EventId,
                    Title = item.Title,
                    TypeName = TypeNames.GetValueOrDefault(item.TypeId),
                    VenueName = VenueNames.GetValueOrDefault(item.VenueId),
                    StartsAt = item.StartsAt,
                    RegistrationDeadline = item.RegistrationDeadline,
                    ParticipantLimit = item.ParticipantLimit,
                    ConfirmedCount = ConfirmedCount(item.EventId),
                    HasCover = item.ImagePath != null && item.ImagePath.StartsWith("uploads/events/"),
                    EventStatusId = item.EventStatusId
                })
                .ToList();

        private PublishedEventDetail Detail(int eventId, bool includeCompleted)
        {
            var item = Items.FirstOrDefault(row => row.EventId == eventId && IsMemberVisible(row, includeCompleted));
            return item == null ? null! : new PublishedEventDetail
            {
                EventId = item.EventId,
                Title = item.Title,
                Description = item.Description,
                TypeName = TypeNames.GetValueOrDefault(item.TypeId),
                VenueName = VenueNames.GetValueOrDefault(item.VenueId),
                StartsAt = item.StartsAt,
                RegistrationDeadline = item.RegistrationDeadline,
                ParticipantLimit = item.ParticipantLimit,
                ConfirmedCount = ConfirmedCount(item.EventId),
                HasCover = item.ImagePath != null && item.ImagePath.StartsWith("uploads/events/"),
                EventStatusId = item.EventStatusId
            };
        }
    }

    private sealed class FakeRegistrations(FakeMembers members) : IRegistrationRepository
    {
        public Task<RegistrationIdentity> DiscoverIdentityAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasEligibleWaitlistAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Registration> GetForUpdateAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int id, DateTime now, int? eventId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public List<Registration> Items { get; } = [];
        public HashSet<int> ValidAttendanceRegistrationIds { get; } = [];

        public Task<bool> IsConfirmedAsync(int registrationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Any(row => row.RegistrationId == registrationId && row.RegistrationStatusId == RegistrationStatusCode.Confirmed));

        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Any(row => row.EventId == eventId && row.MemberId == memberId));

        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default)
        {
            var memberId = members.Items.FirstOrDefault(member => member.UserId == userId)?.MemberId;
            var row = Items.FirstOrDefault(registration => registration.EventId == eventId && registration.MemberId == memberId);
            return Task.FromResult(row == null
                ? null!
                : new MemberRegistrationRow(row.RegistrationId, row.MemberId, row.EventId, row.RegistrationStatusId));
        }

        public Task<bool> HasValidAttendanceAsync(int registrationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ValidAttendanceRegistrationIds.Contains(registrationId));

        public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Count(row => row.EventId == eventId && row.RegistrationStatusId == RegistrationStatusCode.Confirmed));

        public Task<int> CountConfirmedLookupAsync(int eventId, string query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(int eventId, string query, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetConfirmedStudentNumberAsync(int eventId, int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddAsync(Registration registration, CancellationToken cancellationToken = default)
        {
            registration.RegistrationId = Items.Count + 1;
            Items.Add(registration);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records the delegation the service must make; the real writer is covered by the SQL-backed
    /// cancellation tests. It mirrors the status change so later detail reads see the result.
    /// </summary>
    private sealed class FakeCancellation(FakeRegistrations registrations) : IRegistrationCancellationService
    {
        public List<(int RegistrationId, CancellationCause Cause, string? Reason)> Calls { get; } = [];

        public Task CancelRegistrationAsync(int registrationId, CancellationCause cause, string reason, CancellationToken cancellationToken = default)
        {
            Calls.Add((registrationId, cause, reason));
            var row = registrations.Items.Single(item => item.RegistrationId == registrationId);
            row.RegistrationStatusId = RegistrationStatusCode.Cancelled;
            row.QrToken = null;
            row.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Member requested cancellation" : reason.Trim();
            return Task.CompletedTask;
        }

        public Task CancelAllForEventAsync(int eventId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CancelUpcomingForUserAsync(int userId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UpcomingRegistrationPreview>> PreviewUpcomingForUserAsync(int userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMembers : IMemberRepository
    {
        public List<Member> Items { get; } = [];

        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(member => member.UserId == userId)!);

        public IQueryable<MemberRecord> GetMemberRecords() => Array.Empty<MemberRecord>().AsQueryable();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Single(member => member.MemberId == memberId));
        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Any(member => member.UserId == userId));
        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUsers(List<string> locks) : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User> LockUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            locks.Add("user");
            return Task.FromResult(Items.FirstOrDefault(user => user.Id == userId)!);
        }

        public Task<User> GetByIdAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(user => user.Id == userId)!);

        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<User>>(Items);
        public Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
