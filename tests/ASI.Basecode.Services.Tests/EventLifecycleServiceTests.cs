using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class EventLifecycleServiceTests
{
    private static readonly IMapper Mapper = new MapperConfiguration(config =>
    {
        config.CreateMap<EventViewModel, Event>();
        config.CreateMap<Event, EventViewModel>();
        config.CreateMap<EventListItem, EventViewModel>();
    }).CreateMapper();

    private static readonly DateTime Start = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Deadline = new(2026, 3, 9, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateDraftStoresADraftWithTheActorAndInjectedClockAndWritesNoAudit()
    {
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 3, 1, 2, 0, 0, TimeSpan.Zero) };
        var harness = new Harness(clock);
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);

        var id = await harness.Service.CreateDraftAsync(
            Model("  Orientation  ", eventType.TypeId, venue.VenueId, limit: 30, description: "  Welcome  "),
            TestContext.Current.CancellationToken);

        var created = Assert.Single(harness.Events.Items);
        Assert.Equal(id, created.EventId);
        Assert.Equal("Orientation", created.Title);
        Assert.Equal("Welcome", created.Description);
        Assert.Equal(EventStatusCode.Draft, created.EventStatusId);
        Assert.Equal(7, created.CreatedBy);
        Assert.Equal(clock.Now.UtcDateTime, created.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task BlankDescriptionIsStoredAsNull()
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);

        await harness.Service.CreateDraftAsync(
            Model("Orientation", eventType.TypeId, venue.VenueId, description: "   "),
            TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(harness.Events.Items).Description);
    }

    [Fact]
    public async Task InactiveEventTypeIsInvalidOnTypeIdAndWritesNothing()
    {
        var harness = new Harness();
        var eventType = harness.AddType("Retired", isActive: false);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CreateDraftAsync(
            Model("Orientation", eventType.TypeId, venue.VenueId), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(EventViewModel.TypeId), error.Field);
        Assert.Empty(harness.Events.Items);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task MissingEventTypeIsInvalidOnTypeId()
    {
        var harness = new Harness();
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CreateDraftAsync(
            Model("Orientation", 999, venue.VenueId), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(EventViewModel.TypeId), error.Field);
        Assert.Empty(harness.Events.Items);
    }

    [Fact]
    public async Task InactiveVenueIsInvalidOnVenueIdAndWritesNothing()
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Closed room", capacity: 30, isActive: false);

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CreateDraftAsync(
            Model("Orientation", eventType.TypeId, venue.VenueId), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(EventViewModel.VenueId), error.Field);
        Assert.Empty(harness.Events.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(31)]
    public async Task ParticipantLimitOutsideOneToVenueCapacityIsInvalidAndWritesNothing(int limit)
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CreateDraftAsync(
            Model("Orientation", eventType.TypeId, venue.VenueId, limit: limit), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(EventViewModel.ParticipantLimit), error.Field);
        Assert.Empty(harness.Events.Items);
    }

    [Fact]
    public async Task DeadlineAfterStartIsInvalidOnRegistrationDeadlineAndWritesNothing()
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var model = Model("Orientation", eventType.TypeId, venue.VenueId);
        model.RegistrationDeadline = Start.AddMinutes(1);

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CreateDraftAsync(
            model, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(EventViewModel.RegistrationDeadline), error.Field);
        Assert.Empty(harness.Events.Items);
    }

    [Fact]
    public async Task DeadlineEqualToStartIsAccepted()
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var model = Model("Orientation", eventType.TypeId, venue.VenueId);
        model.RegistrationDeadline = Start;

        await harness.Service.CreateDraftAsync(model, TestContext.Current.CancellationToken);

        Assert.Equal(Start, Assert.Single(harness.Events.Items).RegistrationDeadline);
    }

    [Fact]
    public async Task EditingADraftUpdatesTheRowAndWritesOneEventUpdatedEntryWithOldAndNewValues()
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(
            Model("Orientation", eventType.TypeId, venue.VenueId), TestContext.Current.CancellationToken);

        var edited = Model("Orientation renamed", eventType.TypeId, venue.VenueId, limit: 12);
        edited.EventId = id;
        await harness.Service.UpdateDraftAsync(edited, TestContext.Current.CancellationToken);

        var row = Assert.Single(harness.Events.Items);
        Assert.Equal("Orientation renamed", row.Title);
        Assert.Equal(12, row.ParticipantLimit);
        Assert.Equal(7, row.UpdatedBy);
        Assert.NotNull(row.UpdatedAt);

        var entry = Assert.Single(harness.Audit.Entries);
        Assert.Equal(AuditActions.EventUpdated, entry.Action);
        Assert.Equal("events", entry.EntityType);
        Assert.Equal(id, entry.EntityId);
        Assert.Contains("\"Title\":\"Orientation\"", Json(entry.OldValues));
        Assert.Contains("\"Title\":\"Orientation renamed\"", Json(entry.NewValues));
        Assert.Contains("\"TypeName\":\"Workshop\"", Json(entry.OldValues));
        Assert.Contains("\"VenueName\":\"Room A\"", Json(entry.NewValues));
        Assert.Contains("\"ParticipantLimit\":12", Json(entry.NewValues));
        Assert.Contains("\"Status\":1", Json(entry.OldValues));
    }

    [Theory]
    [InlineData(EventStatusCode.Cancelled)]
    [InlineData(EventStatusCode.Completed)]
    public async Task EditingANonDraftIsInvalidAndChangesNothing(EventStatusCode status)
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(
            Model("Orientation", eventType.TypeId, venue.VenueId), TestContext.Current.CancellationToken);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = status;

        var edited = Model("Should not apply", eventType.TypeId, venue.VenueId);
        edited.EventId = id;
        edited.EventStatusId = status;
        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.UpdateDraftAsync(
            edited, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal("Orientation", row.Title);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task PublishedTextEditWorksWithInactiveMastersAndAuditsOldAndNewValues()
    {
        var harness = new Harness();
        var type = harness.AddType("Type", true);
        var venue = harness.AddVenue("Room", 30, true);
        var id = await harness.Service.CreateDraftAsync(Model("Old", type.TypeId, venue.VenueId, description: "Before"), TestContext.Current.CancellationToken);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        type.IsActive = false;
        venue.IsActive = false;
        harness.Locks.Clear();
        var edit = Model("New", type.TypeId, venue.VenueId, description: "After");
        edit.EventId = id;
        edit.EventStatusId = EventStatusCode.Published;
        await harness.Service.UpdateDraftAsync(edit, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "type", "venue", "event" }, harness.Locks);
        Assert.Equal("New", row.Title);
        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Contains("\"Description\":\"Before\"", Json(audit.OldValues));
        Assert.Contains("\"Description\":\"After\"", Json(audit.NewValues));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task PublishedImmutableFieldsCannotBeChanged(int field)
    {
        var harness = new Harness();
        var type = harness.AddType("Type", true);
        var venue = harness.AddVenue("Room", 30, true);
        var id = await harness.Service.CreateDraftAsync(Model("Old", type.TypeId, venue.VenueId), TestContext.Current.CancellationToken);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        var edit = Model("New", type.TypeId, venue.VenueId);
        edit.EventId = id;
        edit.EventStatusId = EventStatusCode.Published;
        switch (field)
        {
            case 0: edit.TypeId++; break;
            case 1: edit.VenueId++; break;
            case 2: edit.StartsAt = edit.StartsAt.AddMinutes(1); break;
            case 3: edit.RegistrationDeadline = edit.RegistrationDeadline.AddMinutes(-1); break;
            case 4: edit.ParticipantLimit++; break;
        }
        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.UpdateDraftAsync(edit, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal("Old", row.Title);
        Assert.Empty(harness.Audit.Entries);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EditRejectsChangedStateOrReferenceDuringLock(bool changeStatus)
    {
        var harness = new Harness();
        var type = harness.AddType("Type", true);
        var venue = harness.AddVenue("Room", 30, true);
        var id = await harness.Service.CreateDraftAsync(Model("Old", type.TypeId, venue.VenueId), TestContext.Current.CancellationToken);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        harness.Events.BeforeLock = item => { if (changeStatus) item.EventStatusId = EventStatusCode.Completed; else item.VenueId++; };
        var edit = Model("New", type.TypeId, venue.VenueId);
        edit.EventId = id;
        edit.EventStatusId = EventStatusCode.Published;
        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.UpdateDraftAsync(edit, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.Equal("Old", row.Title);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task EditRejectsPublicationAfterControllerReadBeforeServiceDiscovery()
    {
        var harness = new Harness();
        var type = harness.AddType("Type", true);
        var venue = harness.AddVenue("Room", 30, true);
        var id = await harness.Service.CreateDraftAsync(Model("Old", type.TypeId, venue.VenueId), TestContext.Current.CancellationToken);
        var edit = Model("New", type.TypeId, venue.VenueId);
        edit.EventId = id;
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        harness.Locks.Clear();

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.UpdateDraftAsync(edit, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.Equal("Old", row.Title);
        Assert.Empty(harness.Audit.Entries);
        Assert.Empty(harness.Locks);
    }

    [Theory]
    [InlineData(0, false, RoleCodes.Officer)]
    [InlineData(7, true, RoleCodes.Member)]
    [InlineData(0, true, RoleCodes.Officer)]
    public async Task EditRejectsUnauthorizedActorsBeforeTakingLocks(int userId, bool authenticated, string role)
    {
        var harness = new Harness(actor: new FixedActor(userId, authenticated, role));
        var model = Model("Edit", 1, 1);
        model.EventId = 1;
        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.UpdateDraftAsync(model, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.Empty(harness.Locks);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task EditingAMissingEventIsNotFound()
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var model = Model("Orientation", eventType.TypeId, venue.VenueId);
        model.EventId = 404;

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.UpdateDraftAsync(
            model, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task AnonymousActorIsForbiddenAndWritesNothing()
    {
        var harness = new Harness(actor: new FixedActor(0, isAuthenticated: false));
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CreateDraftAsync(
            Model("Orientation", eventType.TypeId, venue.VenueId), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.Empty(harness.Events.Items);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task TwentySixEventsPageInStartThenIdOrder()
    {
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        for (var index = 1; index <= 26; index++)
        {
            // Two rows share a start so the event_id tie-break decides their order.
            var startsAt = Start.AddHours(index <= 25 ? index : 25);
            harness.Events.Items.Add(new Event
            {
                EventId = index,
                Title = $"Event {index:D2}",
                TypeId = eventType.TypeId,
                VenueId = venue.VenueId,
                StartsAt = startsAt,
                RegistrationDeadline = startsAt.AddHours(-1),
                ParticipantLimit = 10,
                EventStatusId = EventStatusCode.Draft,
                CreatedBy = 7,
                CreatedAt = Start
            });
        }

        var first = await harness.Service.GetPageAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(26, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(1, first.PageNumber);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(Enumerable.Range(1, 25).Select(index => $"Event {index:D2}"), first.Items.Select(item => item.Title));
        Assert.Equal("Workshop", first.Items[0].TypeName);
        Assert.Equal("Room A", first.Items[0].VenueName);

        var second = await harness.Service.GetPageAsync(2, TestContext.Current.CancellationToken);
        Assert.Equal(2, second.PageNumber);
        Assert.Single(second.Items);
        Assert.Equal("Event 26", second.Items[0].Title);

        var pastEnd = await harness.Service.GetPageAsync(999, TestContext.Current.CancellationToken);
        Assert.Equal(2, pastEnd.PageNumber);
        Assert.Single(pastEnd.Items);
        Assert.Equal("Event 26", pastEnd.Items[0].Title);

        var zero = await harness.Service.GetPageAsync(0, TestContext.Current.CancellationToken);
        Assert.Equal(1, zero.PageNumber);
        Assert.Equal(25, zero.Items.Count);

        var missing = await harness.Service.GetPageAsync(null, TestContext.Current.CancellationToken);
        Assert.Equal(1, missing.PageNumber);
        Assert.Equal(25, missing.Items.Count);
    }

    [Fact]
    public async Task EmptyListShowsPageOneOfZero()
    {
        var harness = new Harness();

        var page = await harness.Service.GetPageAsync(999, TestContext.Current.CancellationToken);

        Assert.Equal(1, page.PageNumber);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task FormOptionsOfferOnlyActiveReferences()
    {
        var harness = new Harness();
        harness.AddType("Active type", isActive: true);
        harness.AddType("Inactive type", isActive: false);
        harness.AddVenue("Active room", capacity: 20, isActive: true);
        harness.AddVenue("Inactive room", capacity: 20, isActive: false);

        var options = await harness.Service.GetFormOptionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Active type" }, options.EventTypes.Select(option => option.TypeName));
        Assert.Equal(new[] { "Active room" }, options.Venues.Select(option => option.RoomName));
        Assert.Equal(20, options.Venues[0].Capacity);
    }

    [Fact]
    public async Task GetEventReturnsNullForAMissingRow()
    {
        var harness = new Harness();

        Assert.Null(await harness.Service.GetEventAsync(404, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishLocksReferencesPublishesAndWritesOneAuditEntry()
    {
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 3, 1, 2, 0, 0, TimeSpan.Zero) };
        var harness = new Harness(clock);
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), TestContext.Current.CancellationToken);
        var row = Assert.Single(harness.Events.Items);
        row.ImagePath = "uploads/events/0123456789abcdef0123456789abcdef.jpg";
        harness.Locks.Clear();

        await harness.Service.PublishAsync(id, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "type", "venue", "event" }, harness.Locks);
        Assert.Equal(EventStatusCode.Published, row.EventStatusId);
        Assert.Equal(7, row.PublishedBy);
        Assert.Equal(clock.Now.UtcDateTime, row.PublishedAt);
        Assert.Equal(7, row.UpdatedBy);
        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Equal(AuditActions.EventPublished, audit.Action);
        Assert.Equal(id, audit.EntityId);
    }

    [Theory]
    [InlineData(EventStatusCode.Published, true, true, true, 10, 1)]
    [InlineData(EventStatusCode.Completed, true, true, true, 10, 1)]
    [InlineData(EventStatusCode.Cancelled, true, true, true, 10, 1)]
    [InlineData(EventStatusCode.Draft, false, true, true, 10, 1)]
    [InlineData(EventStatusCode.Draft, true, false, true, 10, 1)]
    [InlineData(EventStatusCode.Draft, true, true, false, 10, 1)]
    [InlineData(EventStatusCode.Draft, true, true, true, 31, 1)]
    [InlineData(EventStatusCode.Draft, true, true, true, 10, 0)]
    [InlineData(EventStatusCode.Draft, true, true, true, 10, -1)]
    public async Task PublishRefusalLeavesStatusAndAuditUnchanged(
        EventStatusCode status, bool activeType, bool activeVenue, bool hasCover, int limit, int deadlineOffsetSeconds)
    {
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 3, 1, 2, 0, 0, TimeSpan.Zero) };
        var harness = new Harness(clock);
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), TestContext.Current.CancellationToken);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = status;
        row.ImagePath = hasCover ? "uploads/events/0123456789abcdef0123456789abcdef.jpg" : null;
        row.ParticipantLimit = limit;
        eventType.IsActive = activeType;
        venue.IsActive = activeVenue;
        row.RegistrationDeadline = clock.Now.UtcDateTime.AddSeconds(deadlineOffsetSeconds);

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.PublishAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(status, row.EventStatusId);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task PublishMissingEventIsNotFoundAndChangedReferencesAreStaleWithoutWrites()
    {
        var missing = new Harness();
        var missingError = await Assert.ThrowsAsync<DomainException>(() => missing.Service.PublishAsync(404, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.NotFound, missingError.Outcome);

        var clock = new FixedClock();
        var harness = new Harness(clock);
        var eventType = harness.AddType("Workshop", true);
        var venue = harness.AddVenue("Room A", 30, true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), TestContext.Current.CancellationToken);
        var row = Assert.Single(harness.Events.Items);
        row.ImagePath = "uploads/events/0123456789abcdef0123456789abcdef.jpg";
        harness.Events.BeforeLock = item => item.VenueId++;

        var stale = await Assert.ThrowsAsync<DomainException>(() => harness.Service.PublishAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Stale, stale.Outcome);
        Assert.Equal(EventStatusCode.Draft, row.EventStatusId);
        Assert.Empty(harness.Audit.Entries);
    }

    [Theory]
    [InlineData(0, false, RoleCodes.Officer)]
    [InlineData(7, true, RoleCodes.Member)]
    [InlineData(0, true, RoleCodes.Officer)]
    public async Task PublishRejectsAnonymousMemberAndInvalidActors(int userId, bool authenticated, string role)
    {
        var harness = new Harness(actor: new FixedActor(userId, authenticated, role));

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.PublishAsync(1, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task CancelPublishedEventCancelsOpenRegistrationsClosesCheckInAndAuditsEachChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero) };
        var harness = new Harness(clock);
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), ct);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        row.IsCheckInOpen = true;
        row.CheckInOpenedBy = 7;
        row.CheckInOpenedAt = Start;
        harness.Registrations.Items.AddRange(
        [
            Registration(1, id, RegistrationStatusCode.Pending, new string('a', 64)),
            Registration(2, id, RegistrationStatusCode.Confirmed, new string('b', 64)),
            Registration(3, id, RegistrationStatusCode.Waitlisted, new string('c', 64)),
            Registration(4, id, RegistrationStatusCode.Cancelled, null)
        ]);
        harness.Registrations.Items[3].CancelledBy = 99;
        harness.Registrations.Items[3].CancelledAt = Start;
        harness.Registrations.Items[3].CancellationReason = "earlier";
        harness.Locks.Clear();

        await harness.Service.CancelAsync(id, "  Weather  ", ct);

        Assert.Equal(["event"], harness.Locks);
        Assert.Equal(EventStatusCode.Cancelled, row.EventStatusId);
        Assert.Equal(7, row.CancelledBy);
        Assert.Equal(clock.Now.UtcDateTime, row.CancelledAt);
        Assert.Equal("Weather", row.CancellationReason);
        Assert.False(row.IsCheckInOpen);
        Assert.Equal(7, row.CheckInClosedBy);
        Assert.Equal(clock.Now.UtcDateTime, row.CheckInClosedAt);
        Assert.Equal(7, row.UpdatedBy);
        Assert.Equal(clock.Now.UtcDateTime, row.UpdatedAt);

        var call = Assert.Single(harness.Cancellations.Calls);
        Assert.Equal(new CancellationCall(id, CancellationCause.EventCancelled, "Weather"), call);

        foreach (var cancelled in harness.Registrations.Items.Take(3))
        {
            Assert.Equal(RegistrationStatusCode.Cancelled, cancelled.RegistrationStatusId);
            Assert.Null(cancelled.QrToken);
            Assert.Equal("Weather", cancelled.CancellationReason);
            Assert.Equal(7, cancelled.CancelledBy);
        }

        var earlier = harness.Registrations.Items[3];
        Assert.Equal(99, earlier.CancelledBy);
        Assert.Equal(Start, earlier.CancelledAt);
        Assert.Equal("earlier", earlier.CancellationReason);

        Assert.Equal(4, harness.Audit.Entries.Count);
        Assert.Equal(3, harness.Audit.Entries.Count(entry => entry.Action == AuditActions.RegistrationCancelled));
        var eventAudit = Assert.Single(harness.Audit.Entries, entry => entry.Action == AuditActions.EventCancelled);
        Assert.Equal("events", eventAudit.EntityType);
        Assert.Equal(id, eventAudit.EntityId);
        Assert.Equal("Weather", eventAudit.Reason);
        Assert.Contains("\"Status\":2", Json(eventAudit.OldValues));
        Assert.Contains("\"IsCheckInOpen\":true", Json(eventAudit.OldValues));
        Assert.Contains("\"Status\":3", Json(eventAudit.NewValues));
        Assert.Contains("\"IsCheckInOpen\":false", Json(eventAudit.NewValues));
        Assert.Contains("\"CancelledBy\":7", Json(eventAudit.NewValues));
        Assert.Contains("\"CancellationReason\":\"Weather\"", Json(eventAudit.NewValues));
        Assert.DoesNotContain("qr", Json(eventAudit.NewValues), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", Json(eventAudit.NewValues), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelDraftEventRecordsCancellationWithoutClosingAnAlreadyClosedCheckIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero) };
        var harness = new Harness(clock);
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), ct);
        var row = Assert.Single(harness.Events.Items);
        harness.Locks.Clear();

        await harness.Service.CancelAsync(id, "No longer needed", ct);

        Assert.Equal(["event"], harness.Locks);
        Assert.Equal(EventStatusCode.Cancelled, row.EventStatusId);
        Assert.Equal("No longer needed", row.CancellationReason);
        Assert.Equal(7, row.CancelledBy);
        Assert.Equal(clock.Now.UtcDateTime, row.CancelledAt);
        Assert.False(row.IsCheckInOpen);
        Assert.Null(row.CheckInClosedBy);
        Assert.Null(row.CheckInClosedAt);
        Assert.Single(harness.Cancellations.Calls);
        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Equal(AuditActions.EventCancelled, audit.Action);
        Assert.Contains("\"IsCheckInOpen\":false", Json(audit.OldValues));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CancelRefusesValidAttendanceOrAnIssuedCertificateWithoutChanges(bool attendance, bool certificate)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), ct);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        row.IsCheckInOpen = true;
        harness.Registrations.Items.Add(Registration(1, id, RegistrationStatusCode.Confirmed, new string('a', 64)));
        harness.Registrations.ValidAttendanceForEvent = attendance;
        harness.Registrations.IssuedCertificateForEvent = certificate;
        harness.Locks.Clear();

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CancelAsync(id, "Weather", ct));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(["event"], harness.Locks);
        Assert.Equal(EventStatusCode.Published, row.EventStatusId);
        Assert.True(row.IsCheckInOpen);
        Assert.Null(row.CancelledBy);
        Assert.Null(row.CancellationReason);
        Assert.Equal(RegistrationStatusCode.Confirmed, harness.Registrations.Items[0].RegistrationStatusId);
        Assert.NotNull(harness.Registrations.Items[0].QrToken);
        Assert.Empty(harness.Cancellations.Calls);
        Assert.Empty(harness.Audit.Entries);
    }

    [Theory]
    [InlineData(EventStatusCode.Completed)]
    [InlineData(EventStatusCode.Cancelled)]
    public async Task CancelRefusesTerminalEventsWithoutTakingLocks(EventStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), ct);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = status;
        harness.Locks.Clear();

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CancelAsync(id, "Weather", ct));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(status, row.EventStatusId);
        Assert.Empty(harness.Locks);
        Assert.Empty(harness.Cancellations.Calls);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task CancelMissingEventIsNotFoundWithoutTakingLocks()
    {
        var harness = new Harness();

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CancelAsync(404, "Weather", TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
        Assert.Empty(harness.Locks);
        Assert.Empty(harness.Audit.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CancelRefusesABlankReasonInsideTheTransactionWithoutChanges(string? reason)
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), ct);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        row.IsCheckInOpen = true;
        harness.Locks.Clear();

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CancelAsync(id, reason!, ct));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(EventCancelViewModel.Reason), error.Field);
        Assert.Equal(["event"], harness.Locks);
        Assert.Equal(EventStatusCode.Published, row.EventStatusId);
        Assert.True(row.IsCheckInOpen);
        Assert.Empty(harness.Cancellations.Calls);
        Assert.Empty(harness.Audit.Entries);
    }

    [Theory]
    [InlineData(0, false, RoleCodes.Officer)]
    [InlineData(7, true, RoleCodes.Member)]
    [InlineData(0, true, RoleCodes.Officer)]
    public async Task CancelRejectsAnonymousMemberAndInvalidActorsBeforeTakingLocks(int userId, bool authenticated, string role)
    {
        var harness = new Harness(actor: new FixedActor(userId, authenticated, role));

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CancelAsync(1, "Weather", TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.Empty(harness.Locks);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task CancelRejectsAStatusChangeBeforeTheLockAsStale()
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), ct);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        harness.Events.BeforeLock = item => item.EventStatusId = EventStatusCode.Cancelled;
        harness.Locks.Clear();

        var error = await Assert.ThrowsAsync<DomainException>(() => harness.Service.CancelAsync(id, "Weather", ct));

        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.Empty(harness.Cancellations.Calls);
        Assert.Empty(harness.Audit.Entries);
    }

    [Fact]
    public async Task CancelRollsBackEventRegistrationsAndAuditWhenTheSaveFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), ct);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        row.IsCheckInOpen = true;
        harness.Registrations.Items.AddRange(
        [
            Registration(1, id, RegistrationStatusCode.Pending, new string('a', 64)),
            Registration(2, id, RegistrationStatusCode.Confirmed, new string('b', 64)),
            Registration(3, id, RegistrationStatusCode.Waitlisted, new string('c', 64))
        ]);
        harness.UnitOfWork.FailSave = true;
        var commits = harness.UnitOfWork.CommitCount;

        await Assert.ThrowsAsync<IOException>(() => harness.Service.CancelAsync(id, "Weather", ct));

        AssertUnchangedAfterRollback(harness, id, commits);
    }

    [Fact]
    public async Task CancelRollsBackWhenTheEventAuditFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var harness = new Harness();
        var eventType = harness.AddType("Workshop", isActive: true);
        var venue = harness.AddVenue("Room A", capacity: 30, isActive: true);
        var id = await harness.Service.CreateDraftAsync(Model("Orientation", eventType.TypeId, venue.VenueId), ct);
        var row = Assert.Single(harness.Events.Items);
        row.EventStatusId = EventStatusCode.Published;
        row.IsCheckInOpen = true;
        harness.Registrations.Items.AddRange(
        [
            Registration(1, id, RegistrationStatusCode.Pending, new string('a', 64)),
            Registration(2, id, RegistrationStatusCode.Confirmed, new string('b', 64))
        ]);
        harness.Audit.FailOnAction = AuditActions.EventCancelled;
        var commits = harness.UnitOfWork.CommitCount;

        await Assert.ThrowsAsync<IOException>(() => harness.Service.CancelAsync(id, "Weather", ct));

        AssertUnchangedAfterRollback(harness, id, commits);
    }

    private static void AssertUnchangedAfterRollback(Harness harness, int eventId, int expectedCommits)
    {
        var row = Assert.Single(harness.Events.Items);
        Assert.Equal(eventId, row.EventId);
        Assert.Equal(EventStatusCode.Published, row.EventStatusId);
        Assert.True(row.IsCheckInOpen);
        Assert.Null(row.CancelledBy);
        Assert.Null(row.CancelledAt);
        Assert.Null(row.CancellationReason);
        Assert.Null(row.CheckInClosedBy);
        Assert.Null(row.CheckInClosedAt);
        Assert.All(harness.Registrations.Items, registration =>
        {
            Assert.NotEqual(RegistrationStatusCode.Cancelled, registration.RegistrationStatusId);
            Assert.NotNull(registration.QrToken);
        });
        Assert.Empty(harness.Audit.Entries);
        Assert.Equal(expectedCommits, harness.UnitOfWork.CommitCount);
    }

    private static Registration Registration(int id, int eventId, RegistrationStatusCode status, string? qrToken) => new()
    {
        RegistrationId = id,
        MemberId = id,
        EventId = eventId,
        RegistrationStatusId = status,
        CourseSnapshot = "BSIT",
        YearLevelSnapshot = 1,
        SectionSnapshot = "A",
        SubmittedAt = Start,
        QrToken = qrToken,
        CreatedBy = 7,
        CreatedAt = Start
    };

    private static EventViewModel Model(
        string title,
        int typeId,
        int venueId,
        int limit = 10,
        string description = "Description") => new()
        {
            Title = title,
            Description = description,
            TypeId = typeId,
            VenueId = venueId,
            StartsAt = Start,
            RegistrationDeadline = Deadline,
            ParticipantLimit = limit,
            EventStatusId = EventStatusCode.Draft
        };

    private static string Json(IAuditValues values) => JsonSerializer.Serialize(values, values.GetType());

    private sealed class Harness
    {
        public Harness(TimeProvider? clock = null, ICurrentActor? actor = null)
        {
            Events = new MemoryEvents(this);
            EventTypes = new MemoryEventTypes(this);
            Venues = new MemoryVenues(this);
            Registrations = new MemoryRegistrations();
            Audit = new RecordingAudit();
            Actor = actor ?? new FixedActor(7);
            Clock = clock ?? TimeProvider.System;
            Cancellations = new RecordingCancellations(Registrations, Audit, Actor, Clock);
            UnitOfWork = new TransactionUnitOfWork(Events, Registrations, Audit);
            Service = new EventLifecycleService(
                Events,
                EventTypes,
                Venues,
                Registrations,
                Cancellations,
                UnitOfWork,
                Mapper,
                Clock,
                Audit,
                Actor,
                new UnusedPrivateFileStore(),
                NullLogger<EventLifecycleService>.Instance);
        }

        public MemoryEvents Events { get; }
        public MemoryEventTypes EventTypes { get; }
        public MemoryVenues Venues { get; }
        public MemoryRegistrations Registrations { get; }
        public RecordingCancellations Cancellations { get; }
        public RecordingAudit Audit { get; }
        public TransactionUnitOfWork UnitOfWork { get; }
        public ICurrentActor Actor { get; }
        public TimeProvider Clock { get; }
        public EventLifecycleService Service { get; }
        public List<string> Locks { get; } = [];

        public EventType AddType(string name, bool isActive)
        {
            var eventType = new EventType
            {
                TypeId = EventTypes.Items.Count + 1,
                TypeName = name,
                Description = name,
                IsActive = isActive,
                CreatedBy = 7,
                CreatedAt = Start
            };
            EventTypes.Items.Add(eventType);
            Events.TypeNames[eventType.TypeId] = name;
            return eventType;
        }

        public Venue AddVenue(string roomName, int capacity, bool isActive)
        {
            var venue = new Venue
            {
                VenueId = Venues.Items.Count + 1,
                RoomName = roomName,
                Capacity = capacity,
                ImagePath = "uploads/venues/test.jpg",
                IsActive = isActive,
                CreatedBy = 7,
                CreatedAt = Start
            };
            Venues.Items.Add(venue);
            Events.VenueNames[venue.VenueId] = roomName;
            return venue;
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 3, 1, 2, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FixedActor(int userId, bool isAuthenticated = true, string role = RoleCodes.Officer) : ICurrentActor
    {
        public bool IsAuthenticated => isAuthenticated;
        public int UserId => userId;
        public string Role => role;
    }

    private sealed class RecordingAudit : IActivityLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public string? FailOnAction { get; set; }

        public void Append(AuditEntry entry)
        {
            if (entry.Action == FailOnAction) throw new IOException("simulated audit failure");
            Entries.Add(entry);
        }
    }

    private sealed class MemoryRegistrations : IRegistrationRepository
    {
        public List<Registration> Items { get; } = [];
        public bool ValidAttendanceForEvent { get; set; }
        public bool IssuedCertificateForEvent { get; set; }

        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ValidAttendanceForEvent);

        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(IssuedCertificateForEvent);

        public Task<RegistrationReviewIdentity> DiscoverReviewAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasEligibleWaitlistAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Registration> GetForCancellationAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int id, DateTime now, int? eventId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsConfirmedAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountConfirmedAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Registration registration, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed record CancellationCall(int EventId, CancellationCause Cause, string Reason);

    /// <summary>Mimics the real cancellation service contract without touching another module's rules.</summary>
    private sealed class RecordingCancellations(
        MemoryRegistrations registrations,
        RecordingAudit audit,
        ICurrentActor actor,
        TimeProvider clock) : IRegistrationCancellationService
    {
        public List<CancellationCall> Calls { get; } = [];
        public bool Fail { get; set; }

        public Task CancelAllForEventAsync(int eventId, CancellationCause cause, string reason, CancellationToken cancellationToken = default)
        {
            Calls.Add(new CancellationCall(eventId, cause, reason));
            if (Fail) throw new IOException("simulated cancellation failure");
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var row in registrations.Items.Where(row => row.EventId == eventId
                && row.RegistrationStatusId is RegistrationStatusCode.Pending or RegistrationStatusCode.Confirmed or RegistrationStatusCode.Waitlisted))
            {
                row.RegistrationStatusId = RegistrationStatusCode.Cancelled;
                row.QrToken = null;
                row.CancelledBy = actor.UserId;
                row.CancelledAt = now;
                row.CancellationReason = reason;
                row.UpdatedBy = actor.UserId;
                row.UpdatedAt = now;
                audit.Append(new AuditEntry(AuditActions.RegistrationCancelled, "registrations", row.RegistrationId, reason));
            }

            return Task.CompletedTask;
        }

        public Task CancelRegistrationAsync(int registrationId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelUpcomingForUserAsync(int userId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationPreview>> PreviewUpcomingForUserAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MemoryEvents(Harness harness) : IEventRepository
    {
        public List<Event> Items { get; } = [];
        public Dictionary<int, string> TypeNames { get; } = [];
        public Dictionary<int, string> VenueNames { get; } = [];
        private int nextId = 1;
        public Action<Event>? BeforeLock { get; set; }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

        public Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<EventListItem> page = Items
                .OrderBy(item => item.StartsAt)
                .ThenBy(item => item.EventId)
                .Skip(skip)
                .Take(take)
                .Select(item => new EventListItem
                {
                    EventId = item.EventId,
                    Title = item.Title,
                    TypeName = TypeNames.GetValueOrDefault(item.TypeId),
                    VenueName = VenueNames.GetValueOrDefault(item.VenueId),
                    StartsAt = item.StartsAt,
                    RegistrationDeadline = item.RegistrationDeadline,
                    ParticipantLimit = item.ParticipantLimit,
                    EventStatusId = item.EventStatusId
                })
                .ToList();
            return Task.FromResult(page);
        }

        public Task<Event> GetEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var item = Items.FirstOrDefault(item => item.EventId == eventId);
            return Task.FromResult<Event>(item == null ? null! : new Event
            {
                EventId = item.EventId,
                TypeId = item.TypeId,
                VenueId = item.VenueId,
                EventStatusId = item.EventStatusId,
                ImagePath = item.ImagePath
            });
        }

        public Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            harness.Locks.Add("event");
            var item = Items.FirstOrDefault(item => item.EventId == eventId);
            if (item != null) BeforeLock?.Invoke(item);
            return Task.FromResult<Event>(item!);
        }

        public Task AddEventAsync(Event eventItem, CancellationToken cancellationToken = default)
        {
            eventItem.EventId = nextId++;
            Items.Add(eventItem);
            return Task.CompletedTask;
        }

        public Task UpdateEventAsync(Event eventItem, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Count(item => item.EventStatusId == EventStatusCode.Published));

        public Task<IReadOnlyList<PublishedEventListItem>> GetPublishedPageAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PublishedEventListItem> page = Items
                .Where(item => item.EventStatusId == EventStatusCode.Published)
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
                    ConfirmedCount = 0,
                    HasCover = item.ImagePath != null && item.ImagePath.StartsWith("uploads/events/"),
                    EventStatusId = item.EventStatusId
                })
                .ToList();
            return Task.FromResult(page);
        }

        public Task<PublishedEventDetail> GetPublishedEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var item = Items.FirstOrDefault(row => row.EventId == eventId && row.EventStatusId == EventStatusCode.Published);
            return Task.FromResult(item == null ? null! : new PublishedEventDetail
            {
                EventId = item.EventId,
                Title = item.Title,
                Description = item.Description,
                TypeName = TypeNames.GetValueOrDefault(item.TypeId),
                VenueName = VenueNames.GetValueOrDefault(item.VenueId),
                StartsAt = item.StartsAt,
                RegistrationDeadline = item.RegistrationDeadline,
                ParticipantLimit = item.ParticipantLimit,
                ConfirmedCount = 0,
                HasCover = item.ImagePath != null && item.ImagePath.StartsWith("uploads/events/"),
                EventStatusId = item.EventStatusId
            });
        }
    }

    private sealed class MemoryEventTypes(Harness harness) : IEventTypeRepository
    {
        public List<EventType> Items { get; } = [];

        private EventType? Find(int typeId) => Items.FirstOrDefault(item => item.TypeId == typeId);

        public Task<IReadOnlyList<EventType>> GetActiveEventTypesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventType>>(Items.Where(item => item.IsActive).OrderBy(item => item.TypeName).ThenBy(item => item.TypeId).ToList());

        public Task<int> CountEventTypesAsync(string status, CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

        public Task<IReadOnlyList<EventType>> GetEventTypesPageAsync(string status, int offset, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventType>>(Items.Skip(offset).Take(pageSize).ToList());

        public EventType GetEventType(int typeId) => Find(typeId)!;

        public Task<EventType> LockEventTypeAsync(int typeId, CancellationToken cancellationToken = default)
        {
            harness.Locks.Add("type");
            return Task.FromResult(Find(typeId)!);
        }

        public Task<int> CountLinkedEventsAsync(int typeId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task AddEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default)
        {
            Items.Add(eventType);
            return Task.CompletedTask;
        }

        public Task UpdateEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool EventTypeExists(int typeId) => Find(typeId) != null;

        public bool TypeNameExists(string typeName, int? excludeId = null) =>
            Items.Any(item => item.TypeName == typeName && (!excludeId.HasValue || item.TypeId != excludeId.Value));
    }

    private sealed class MemoryVenues(Harness harness) : IVenueRepository
    {
        public List<Venue> Items { get; } = [];

        private Venue? Find(int venueId) => Items.FirstOrDefault(item => item.VenueId == venueId);


        public Task<IReadOnlyList<Venue>> GetActiveVenuesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Venue>>(Items.Where(item => item.IsActive).OrderBy(item => item.RoomName).ThenBy(item => item.VenueId).ToList());

        public Task<int> CountAsync(string status, CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

        public Task<IReadOnlyList<Venue>> GetPageAsync(string status, int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Venue>>(Items.Skip(skip).Take(take).ToList());

        public Venue GetVenue(int venueId) => Find(venueId)!;

        public Task<Venue> LockVenueAsync(int venueId, CancellationToken cancellationToken = default)
        {
            harness.Locks.Add("venue");
            return Task.FromResult(Find(venueId)!);
        }

        public Task<int> CountLinkedEventsAsync(int venueId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<bool> HasEventAboveCapacityAsync(int venueId, int capacity, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task AddVenueAsync(Venue venue, CancellationToken cancellationToken = default)
        {
            Items.Add(venue);
            return Task.CompletedTask;
        }

        public Task UpdateVenueAsync(Venue venue, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool VenueExists(int venueId) => Find(venueId) != null;
    }

    /// <summary>
    /// Applies the work immediately but restores the rows, registrations and audit entries when the
    /// work throws, so rollback behavior is observable without SQL Server.
    /// </summary>
    private sealed class TransactionUnitOfWork(MemoryEvents events, MemoryRegistrations registrations, RecordingAudit audit) : IUnitOfWork
    {
        public bool FailSave { get; set; }
        public int CommitCount { get; private set; }
        public DbContext Database => throw new NotSupportedException();
        public void SaveChanges() => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            FailSave ? Task.FromException<int>(new IOException("simulated database failure")) : Task.FromResult(1);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async ct => { await work(ct); return true; }, cancellationToken);

        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default) =>
            await ExecuteAsync(work, cancellationToken);

        private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
        {
            var eventSnapshots = events.Items.Select(Clone).ToList();
            var registrationSnapshots = registrations.Items.Select(Clone).ToList();
            var auditCount = audit.Entries.Count;
            try
            {
                var result = await work(cancellationToken);
                CommitCount++;
                return result;
            }
            catch
            {
                events.Items.Clear();
                events.Items.AddRange(eventSnapshots);
                registrations.Items.Clear();
                registrations.Items.AddRange(registrationSnapshots);
                audit.Entries.RemoveRange(auditCount, audit.Entries.Count - auditCount);
                throw;
            }
        }

        private static Event Clone(Event item) => new()
        {
            EventId = item.EventId,
            Title = item.Title,
            Description = item.Description,
            TypeId = item.TypeId,
            VenueId = item.VenueId,
            StartsAt = item.StartsAt,
            RegistrationDeadline = item.RegistrationDeadline,
            ParticipantLimit = item.ParticipantLimit,
            EventStatusId = item.EventStatusId,
            ImagePath = item.ImagePath,
            PublishedBy = item.PublishedBy,
            PublishedAt = item.PublishedAt,
            IsCheckInOpen = item.IsCheckInOpen,
            CheckInOpenedBy = item.CheckInOpenedBy,
            CheckInOpenedAt = item.CheckInOpenedAt,
            CheckInClosedBy = item.CheckInClosedBy,
            CheckInClosedAt = item.CheckInClosedAt,
            CompletedBy = item.CompletedBy,
            CompletedAt = item.CompletedAt,
            CancelledBy = item.CancelledBy,
            CancelledAt = item.CancelledAt,
            CancellationReason = item.CancellationReason,
            CreatedBy = item.CreatedBy,
            CreatedAt = item.CreatedAt,
            UpdatedBy = item.UpdatedBy,
            UpdatedAt = item.UpdatedAt
        };

        private static Registration Clone(Registration item) => new()
        {
            RegistrationId = item.RegistrationId,
            MemberId = item.MemberId,
            EventId = item.EventId,
            RegistrationStatusId = item.RegistrationStatusId,
            CourseSnapshot = item.CourseSnapshot,
            YearLevelSnapshot = item.YearLevelSnapshot,
            SectionSnapshot = item.SectionSnapshot,
            SubmittedAt = item.SubmittedAt,
            ReviewedBy = item.ReviewedBy,
            ReviewedAt = item.ReviewedAt,
            ReviewNotes = item.ReviewNotes,
            ConfirmedBy = item.ConfirmedBy,
            ConfirmedAt = item.ConfirmedAt,
            QrToken = item.QrToken,
            CancelledBy = item.CancelledBy,
            CancelledAt = item.CancelledAt,
            CancellationReason = item.CancellationReason,
            CreatedBy = item.CreatedBy,
            CreatedAt = item.CreatedAt,
            UpdatedBy = item.UpdatedBy,
            UpdatedAt = item.UpdatedAt
        };
    }

    private sealed class UnusedPrivateFileStore : IPrivateFileStore
    {
        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();
        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Delete(string relativePath) => throw new NotSupportedException();
    }
}
