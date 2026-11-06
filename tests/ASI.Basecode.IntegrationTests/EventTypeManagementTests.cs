using System.Data.Common;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventTypeManagementTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private AsiBasecodeDBContext Context(params IInterceptor[] interceptors) => EventTypeTestSupport.Context(fixture.ConnectionString, interceptors);

    private async Task<(int TypeId, int VenueId)> SeedTypeAndVenueAsync(int actorId, CancellationToken cancellationToken, bool withEvent = false)
    {
        await using var context = Context();
        var type = new EventType
        {
            TypeName = $"Type {Guid.NewGuid():N}",
            Description = "Original description",
            IsActive = true,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        var venue = new Venue
        {
            RoomName = $"Room {Guid.NewGuid():N}",
            Capacity = 30,
            ImagePath = "uploads/venues/test.jpg",
            IsActive = true,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        context.EventTypes.Add(type);
        context.Venues.Add(venue);
        await context.SaveChangesAsync(cancellationToken);

        if (withEvent)
        {
            context.Events.Add(new Event
            {
                Title = "Linked event",
                TypeId = type.TypeId,
                VenueId = venue.VenueId,
                StartsAt = DateTime.UtcNow.AddDays(2),
                RegistrationDeadline = DateTime.UtcNow.AddDays(1),
                ParticipantLimit = 20,
                EventStatusId = EventStatusCode.Draft,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        return (type.TypeId, venue.VenueId);
    }

    private static EventTypeViewModel Model(int typeId, string name, string description = "Description") =>
        new() { TypeId = typeId, TypeName = name, Description = description };

    [Fact]
    public async Task CreateEditAndToggleWriteOldAndNewAuditValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventTypeTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        await using var writer = Context();
        var service = EventTypeTestSupport.Service(writer, actorId);
        var name = $"Audit Type {Guid.NewGuid():N}";

        await service.AddEventTypeAsync(Model(0, $"  {name}  ", "  Created description  "), ct);

        await using var observer = Context();
        var created = await observer.EventTypes.AsNoTracking().SingleAsync(t => t.TypeName == name, ct);
        Assert.Equal("Created description", created.Description);
        Assert.Equal(actorId, created.CreatedBy);
        var createLog = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            a => a.EntityType == "event_types" && a.EntityId == created.TypeId && a.Action == AuditActions.EventTypeCreated, ct);
        Assert.Equal(actorId, createLog.ActorUserId);
        Assert.Null(createLog.OldValues);
        Assert.Contains($"\"TypeName\":\"{name}\"", createLog.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"Description\":\"Created description\"", createLog.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"IsActive\":true", createLog.NewValues, StringComparison.Ordinal);

        var editedName = $"Edited Type {Guid.NewGuid():N}";
        await service.UpdateEventTypeAsync(Model(created.TypeId, editedName, "Edited description"), ct);

        var edited = await observer.EventTypes.AsNoTracking().SingleAsync(t => t.TypeId == created.TypeId, ct);
        Assert.Equal(editedName, edited.TypeName);
        Assert.Equal("Edited description", edited.Description);
        Assert.Equal(actorId, edited.UpdatedBy);
        var editLog = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            a => a.EntityType == "event_types" && a.EntityId == created.TypeId && a.Action == AuditActions.EventTypeEdited, ct);
        Assert.Contains($"\"TypeName\":\"{name}\"", editLog.OldValues, StringComparison.Ordinal);
        Assert.Contains("\"Description\":\"Created description\"", editLog.OldValues, StringComparison.Ordinal);
        Assert.Contains($"\"TypeName\":\"{editedName}\"", editLog.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"Description\":\"Edited description\"", editLog.NewValues, StringComparison.Ordinal);

        await service.ToggleActiveAsync(created.TypeId, ct);

        var toggled = await observer.EventTypes.AsNoTracking().SingleAsync(t => t.TypeId == created.TypeId, ct);
        Assert.False(toggled.IsActive);
        var statusLog = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            a => a.EntityType == "event_types" && a.EntityId == created.TypeId && a.Action == AuditActions.EventTypeStatusChanged, ct);
        Assert.Contains("\"IsActive\":true", statusLog.OldValues, StringComparison.Ordinal);
        Assert.Contains("\"IsActive\":false", statusLog.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"LinkedEventCount\":0", statusLog.OldValues, StringComparison.Ordinal);
        Assert.Contains("\"LinkedEventCount\":0", statusLog.NewValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeactivationKeepsLinkedEventsAndRecordsTheCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventTypeTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, _) = await SeedTypeAndVenueAsync(actorId, ct, withEvent: true);
        await using var writer = Context();
        var service = EventTypeTestSupport.Service(writer, actorId);

        await service.ToggleActiveAsync(typeId, ct);

        await using var observer = Context();
        var type = await observer.EventTypes.AsNoTracking().SingleAsync(t => t.TypeId == typeId, ct);
        Assert.False(type.IsActive);
        var linked = await observer.Events.AsNoTracking().SingleAsync(e => e.TypeId == typeId, ct);
        Assert.Equal(typeId, linked.TypeId);
        Assert.Equal(EventStatusCode.Draft, linked.EventStatusId);
        var log = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            a => a.EntityType == "event_types" && a.EntityId == typeId && a.Action == AuditActions.EventTypeStatusChanged, ct);
        Assert.Contains("\"LinkedEventCount\":1", log.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"IsActive\":false", log.NewValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToggleWaitsForAConcurrentLockAndAppliesAfterRelease()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventTypeTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, _) = await SeedTypeAndVenueAsync(actorId, ct);
        var signal = new LockCommandSignal();
        await using var writer = Context(signal);
        var service = EventTypeTestSupport.Service(writer, actorId);

        await using var owner = Context();
        await using var transaction = await owner.Database.BeginTransactionAsync(ct);
        var locked = await new EventTypeRepository(new UnitOfWork(owner)).LockEventTypeAsync(typeId, ct);
        Assert.NotNull(locked);

        var pending = service.ToggleActiveAsync(typeId, ct);
        var committed = false;
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(300, ct)));
            await transaction.CommitAsync(ct);
            committed = true;
            await pending;
        }
        finally
        {
            if (!committed) await transaction.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
        }

        await using var observer = Context();
        var type = await observer.EventTypes.AsNoTracking().SingleAsync(t => t.TypeId == typeId, ct);
        Assert.False(type.IsActive);
    }

    // Signals immediately before the waiting SQL command is sent, not merely when a task is scheduled.
    private sealed class LockCommandSignal : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("event_types", StringComparison.OrdinalIgnoreCase)) Started.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}

public sealed class EventTypePagingTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task TwentySixRowsPageInNameAndIdOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventTypeTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        await using (var context = EventTypeTestSupport.Context(fixture.ConnectionString))
        {
            for (var index = 1; index <= 26; index++)
            {
                context.EventTypes.Add(new EventType
                {
                    TypeName = $"Paging Type {index:D2}",
                    Description = $"Paging description {index:D2}",
                    IsActive = true,
                    CreatedBy = actorId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync(ct);
        }

        await using var reader = EventTypeTestSupport.Context(fixture.ConnectionString);
        var service = EventTypeTestSupport.Service(reader, actorId);

        var first = await service.GetPageAsync(null, 1, ct);
        Assert.Equal(26, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(1, first.PageNumber);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(Enumerable.Range(1, 25).Select(index => $"Paging Type {index:D2}"), first.Items.Select(item => item.TypeName));

        var second = await service.GetPageAsync(null, 2, ct);
        Assert.Equal(2, second.PageNumber);
        Assert.Single(second.Items);
        Assert.Equal("Paging Type 26", second.Items[0].TypeName);

        var pastEnd = await service.GetPageAsync(null, 999, ct);
        Assert.Equal(2, pastEnd.PageNumber);
        Assert.Single(pastEnd.Items);
        Assert.Equal("Paging Type 26", pastEnd.Items[0].TypeName);

        var zero = await service.GetPageAsync(null, 0, ct);
        Assert.Equal(1, zero.PageNumber);
        Assert.Equal(25, zero.Items.Count);

        var missing = await service.GetPageAsync(null, null, ct);
        Assert.Equal(1, missing.PageNumber);
        Assert.Equal(25, missing.Items.Count);
    }
}

internal static class EventTypeTestSupport
{
    public static readonly IMapper Mapper = new MapperConfiguration(config =>
    {
        config.CreateMap<EventTypeViewModel, EventType>();
        config.CreateMap<EventType, EventTypeViewModel>();
    }).CreateMapper();

    public static AsiBasecodeDBContext Context(string connectionString, params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(connectionString)
            .AddInterceptors(interceptors).Options);

    public static EventTypeService Service(AsiBasecodeDBContext context, int actorId)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorId);
        return new EventTypeService(new EventTypeRepository(unitOfWork), unitOfWork, Mapper, TimeProvider.System,
            new ActivityLogWriter(context, actor, TimeProvider.System), actor);
    }

    public static async Task<int> SeedActorAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var context = Context(connectionString);
        var actor = new User
        {
            Name = "Event Type Tester",
            UserId = $"{Guid.NewGuid():N}@example.invalid",
            Password = "test",
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        context.Users.Add(actor);
        await context.SaveChangesAsync(cancellationToken);
        return actor.Id;
    }

    private sealed class Actor(int id) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => "officer";
    }
}
