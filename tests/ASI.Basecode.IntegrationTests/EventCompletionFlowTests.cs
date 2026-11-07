using ASI.Basecode.Data;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventCompletionServiceTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CompletePublishedEventClosesCheckInRecordsCompletionWritesAuditAndRunsHandlersOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, "Complete published event", ct);
        await OpenCheckInAsync(eventItem.EventId, officerId, ct);

        var recorder = new EventDraftPersistenceTests.LockOrderRecorder();
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, recorder);
        var handler = new RecordingCompletionHandler(context);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now),
            completionHandlers: [handler]);

        await service.CompleteAsync(eventItem.EventId, ct);

        // AD-4: completion takes only the event lock.
        Assert.Single(recorder.Locks);
        Assert.Contains("events", recorder.Locks[0], StringComparison.OrdinalIgnoreCase);

        // The handler ran once, saw the saved Completed row inside the caller's transaction and
        // received the persisted completion metadata.
        Assert.Equal(1, handler.Calls);
        Assert.Equal(EventStatusCode.Completed, handler.StatusSeen);
        Assert.NotNull(handler.Context);
        Assert.Equal(eventItem.EventId, handler.Context!.EventId);
        Assert.Equal(officerId, handler.Context.CompletedBy);
        Assert.Equal(Now, handler.Context.CompletedAt);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Completed, persisted.EventStatusId);
        Assert.Equal(officerId, persisted.CompletedBy);
        Assert.Equal(Now, persisted.CompletedAt);
        Assert.False(persisted.IsCheckInOpen);
        Assert.Equal(officerId, persisted.CheckInClosedBy);
        Assert.Equal(Now, persisted.CheckInClosedAt);
        Assert.Equal(officerId, persisted.UpdatedBy);
        Assert.Equal(Now, persisted.UpdatedAt);

        var log = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct);
        Assert.Equal(AuditActions.EventCompleted, log.Action);
        Assert.Equal(officerId, log.ActorUserId);
        Assert.Contains("\"Status\":2", log.OldValues!, StringComparison.Ordinal);
        Assert.Contains("\"IsCheckInOpen\":true", log.OldValues!, StringComparison.Ordinal);
        Assert.Contains("\"Status\":4", log.NewValues!, StringComparison.Ordinal);
        Assert.Contains("\"IsCheckInOpen\":false", log.NewValues!, StringComparison.Ordinal);
        Assert.Contains($"\"CompletedBy\":{officerId}", log.NewValues!, StringComparison.Ordinal);
        Assert.DoesNotContain("token", log.NewValues!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("path", log.NewValues!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteWithNoRegisteredHandlersStillSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, "Complete without handlers", ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now));

        await service.CompleteAsync(eventItem.EventId, ct);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Completed, persisted.EventStatusId);
        Assert.Equal(officerId, persisted.CompletedBy);
        Assert.Null(persisted.CheckInClosedBy);
        Assert.Single(await observer.ActivityLogs.AsNoTracking()
            .Where(row => row.EntityType == "events" && row.EntityId == eventItem.EventId).ToListAsync(ct));
    }

    [Fact]
    public async Task CompleteBeforeStartIsRefusedWithoutChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(1), Now.AddMinutes(-30), 20, null, "Complete before start", ct);
        await OpenCheckInAsync(eventItem.EventId, officerId, ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var handler = new RecordingCompletionHandler(context);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now),
            completionHandlers: [handler]);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.CompleteAsync(eventItem.EventId, ct));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(0, handler.Calls);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Published, persisted.EventStatusId);
        Assert.True(persisted.IsCheckInOpen);
        Assert.Null(persisted.CompletedBy);
        Assert.Null(persisted.CompletedAt);
        Assert.Null(persisted.CheckInClosedBy);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct));
    }

    [Theory]
    [InlineData(EventStatusCode.Draft)]
    [InlineData(EventStatusCode.Completed)]
    [InlineData(EventStatusCode.Cancelled)]
    public async Task CompleteRefusesNonPublishedEventsWithoutChanges(EventStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, status,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, $"Complete {status}", ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now));

        var error = await Assert.ThrowsAsync<DomainException>(() => service.CompleteAsync(eventItem.EventId, ct));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(status, persisted.EventStatusId);
        Assert.Null(persisted.CompletedBy);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct));
    }

    [Fact]
    public async Task ThrowingHandlerRollsBackTheCompletionCheckInClosureAndAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, "Throwing handler event", ct);
        await OpenCheckInAsync(eventItem.EventId, officerId, ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var handler = new ThrowingCompletionHandler();
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now),
            completionHandlers: [handler]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteAsync(eventItem.EventId, ct));

        Assert.Equal(1, handler.Calls);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Published, persisted.EventStatusId);
        Assert.True(persisted.IsCheckInOpen);
        Assert.Null(persisted.CompletedBy);
        Assert.Null(persisted.CompletedAt);
        Assert.Null(persisted.CheckInClosedBy);
        Assert.Null(persisted.CheckInClosedAt);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct));
    }

    [Fact]
    public async Task CompletedEventStaysOnTheMemberFeedAndDetailButCannotBeRegisteredFor()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var memberUser = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"complete-member-{Guid.NewGuid():N}@test.invalid",
            MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(-1), Now.AddDays(-2), 20, null, "Completed member event", ct);

        await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            await EventDraftTestSupport.Service(context, officerId, new FixedClock(Now)).CompleteAsync(eventItem.EventId, ct);
        }

        await using var memberContext = MemberEventTestSupport.Context(fixture.ConnectionString);
        var memberService = MemberEventTestSupport.Service(memberContext, memberUser.Id, RoleCodes.Member, new FixedClock(Now));

        var feed = await memberService.GetFeedAsync(1, ct);
        var card = Assert.Single(feed.Items, item => item.EventId == eventItem.EventId);
        Assert.Equal(EventStatusCode.Completed, card.EventStatusId);
        Assert.Equal("Completed", card.StatusLabel);
        Assert.False(card.IsSignupOpen);

        var detail = await memberService.GetDetailAsync(eventItem.EventId, ct);
        Assert.NotNull(detail);
        Assert.Equal(EventStatusCode.Completed, detail!.EventStatusId);
        Assert.Equal("Completed", detail.StatusLabel);
        Assert.False(detail.IsSignupOpen);
        Assert.False(detail.CanRegister);

        var error = await Assert.ThrowsAsync<DomainException>(() => memberService.RegisterAsync(eventItem.EventId, ct));
        Assert.Equal(OutcomeCode.CutoffPassed, error.Outcome);
    }

    private async Task OpenCheckInAsync(int eventId, int actorId, CancellationToken cancellationToken)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Events.SingleAsync(item => item.EventId == eventId, cancellationToken);
        row.IsCheckInOpen = true;
        row.CheckInOpenedBy = actorId;
        row.CheckInOpenedAt = Now.AddHours(-1);
        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed class RecordingCompletionHandler(AsiBasecodeDBContext db) : IEventCompletionHandler
    {
        public int Calls { get; private set; }
        public EventCompletionContext? Context { get; private set; }
        public EventStatusCode? StatusSeen { get; private set; }

        public async Task HandleAsync(EventCompletionContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            Context = context;
            StatusSeen = (await db.Events.AsNoTracking()
                .SingleAsync(row => row.EventId == context.EventId, cancellationToken)).EventStatusId;
        }
    }

    private sealed class ThrowingCompletionHandler : IEventCompletionHandler
    {
        public int Calls { get; private set; }

        public Task HandleAsync(EventCompletionContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("simulated completion handler failure");
        }
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}
