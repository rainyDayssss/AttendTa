using System.Collections.Generic;
using System.Text.Json;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class EventTypeTimestampTests
{
    [Fact]
    public async Task CreateUpdateAndToggleUseTheInjectedClockAndWriteAuditSnapshots()
    {
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 1, 1, 18, 30, 0, TimeSpan.Zero) };
        var repository = new MemoryEventTypes();
        var audit = new RecordingAudit();
        var service = new EventTypeService(repository, new ImmediateUnitOfWork(), null!, clock, audit, new FixedActor(7));
        var model = new EventTypeViewModel { TypeId = 1, TypeName = "  Workshop  ", Description = "  Description  " };

        await service.AddEventTypeAsync(model, TestContext.Current.CancellationToken);
        Assert.Equal(clock.Now.UtcDateTime, repository.Item!.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, repository.Item.CreatedAt.Kind);
        Assert.Equal("Workshop", repository.Item.TypeName);
        Assert.Equal("Description", repository.Item.Description);
        Assert.Equal(7, repository.Item.CreatedBy);
        var created = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.EventTypeCreated, created.Action);
        Assert.Equal("event_types", created.EntityType);
        Assert.Equal(repository.Item.TypeId, created.EntityId);
        Assert.Null(created.OldValues);
        Assert.Contains("\"TypeName\":\"Workshop\"", Json(created.NewValues));
        Assert.Contains("\"Description\":\"Description\"", Json(created.NewValues));
        Assert.Contains("\"IsActive\":true", Json(created.NewValues));

        clock.Now = clock.Now.AddHours(1);
        await service.UpdateEventTypeAsync(new EventTypeViewModel { TypeId = 1, TypeName = "Seminar", Description = "Edited" }, TestContext.Current.CancellationToken);
        Assert.Equal(clock.Now.UtcDateTime, repository.Item.UpdatedAt);
        Assert.Equal(7, repository.Item.UpdatedBy);
        Assert.Equal(2, audit.Entries.Count);
        var edited = audit.Entries[1];
        Assert.Equal(AuditActions.EventTypeEdited, edited.Action);
        Assert.Contains("\"TypeName\":\"Workshop\"", Json(edited.OldValues));
        Assert.Contains("\"TypeName\":\"Seminar\"", Json(edited.NewValues));

        clock.Now = clock.Now.AddHours(1);
        repository.LinkedEvents = 3;
        await service.ToggleActiveAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(clock.Now.UtcDateTime, repository.Item.UpdatedAt);
        Assert.False(repository.Item.IsActive);
        Assert.Equal(3, audit.Entries.Count);
        var status = audit.Entries[2];
        Assert.Equal(AuditActions.EventTypeStatusChanged, status.Action);
        Assert.Contains("\"IsActive\":true", Json(status.OldValues));
        Assert.Contains("\"IsActive\":false", Json(status.NewValues));
        Assert.Contains("\"LinkedEventCount\":3", Json(status.OldValues));
        Assert.Contains("\"LinkedEventCount\":3", Json(status.NewValues));
    }

    [Fact]
    public async Task AnonymousActorIsForbiddenAndWritesNothing()
    {
        var repository = new MemoryEventTypes();
        var audit = new RecordingAudit();
        var service = new EventTypeService(repository, new ImmediateUnitOfWork(), null!, TimeProvider.System, audit, new FixedActor(0, isAuthenticated: false));

        var error = await Assert.ThrowsAsync<DomainException>(() => service.AddEventTypeAsync(
            new EventTypeViewModel { TypeName = "Workshop", Description = "Description" },
            TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.Null(repository.Item);
        Assert.Empty(audit.Entries);
    }

    [Theory]
    [InlineData(false, 7, "officer")]
    [InlineData(true, 0, "officer")]
    [InlineData(true, -1, "officer")]
    [InlineData(true, 7, "member")]
    public async Task VenueWritesRejectInvalidActorsBeforeAnyStorageOrDatabaseWork(bool authenticated, int id, string role)
    {
        var service = new VenueService(null!, null!, null!, TimeProvider.System, null!, null!,
            new VenueActor(authenticated, id, role), null!);
        var token = TestContext.Current.CancellationToken;
        var create = await Assert.ThrowsAsync<DomainException>(() => service.AddVenueAsync(new VenueViewModel(), null!, null!, token));
        var edit = await Assert.ThrowsAsync<DomainException>(() => service.UpdateVenueAsync(new VenueViewModel(), null!, null!, token));
        var toggle = await Assert.ThrowsAsync<DomainException>(() => service.ToggleActiveAsync(1, token));
        Assert.Equal(OutcomeCode.Forbidden, create.Outcome);
        Assert.Equal(OutcomeCode.Forbidden, edit.Outcome);
        Assert.Equal(OutcomeCode.Forbidden, toggle.Outcome);
        Assert.Equal("A valid authenticated user is required to update a venue.", toggle.Message);
    }

    [Fact]
    public async Task PrivateFileBoundaryMapsValidationErrorsButPreservesIoFailures()
    {
        var token = TestContext.Current.CancellationToken;
        var invalid = new FailingStore(new InvalidDataException("Invalid image."));
        var upload = await Assert.ThrowsAsync<DomainException>(() => invalid.SaveImageAsync(
            PrivateFileArea.VenueImages, Stream.Null, ".jpg", "photo", token));
        Assert.Equal(OutcomeCode.Invalid, upload.Outcome);
        Assert.Equal("photo", upload.Field);
        Assert.Equal("Invalid image.", upload.Message);
        var read = await Assert.ThrowsAsync<DomainException>(() => invalid.OpenSafelyAsync("unsafe", token));
        Assert.Equal(OutcomeCode.NotFound, read.Outcome);
        Assert.DoesNotContain("unsafe", read.Message);
        var io = new FailingStore(new IOException("I/O failure"));
        await Assert.ThrowsAsync<IOException>(() => io.SaveImageAsync(PrivateFileArea.VenueImages, Stream.Null, ".jpg", "photo", token));
        await Assert.ThrowsAsync<IOException>(() => io.OpenSafelyAsync("key", token));
    }

    private sealed class VenueActor(bool authenticated, int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => authenticated;
        public int UserId => id == -1 ? throw new InvalidOperationException() : id;
        public string Role => role;
    }

    private sealed class FailingStore(Exception error) : IPrivateFileStore
    {
        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default) => Task.FromException<string>(error);
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => Task.FromException<Stream>(error);
        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();
        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Delete(string relativePath) => throw new NotSupportedException();
    }

    private static string Json(IAuditValues values) => JsonSerializer.Serialize(values, values.GetType());

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FixedActor(int userId, bool isAuthenticated = true) : ICurrentActor
    {
        public bool IsAuthenticated => isAuthenticated;
        public int UserId => userId;
        public string Role => "officer";
    }

    private sealed class RecordingAudit : IActivityLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class MemoryEventTypes : IEventTypeRepository
    {
        public EventType? Item { get; private set; }
        public int LinkedEvents { get; set; }

        public Task<IReadOnlyList<EventType>> GetActiveEventTypesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventType>>(Item is { IsActive: true } ? [Item] : []);

        public Task<int> CountEventTypesAsync(string status, CancellationToken cancellationToken = default) => Task.FromResult(Item is null ? 0 : 1);

        public Task<IReadOnlyList<EventType>> GetEventTypesPageAsync(string status, int offset, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventType>>(Item is null ? [] : [Item]);

        public EventType GetEventType(int typeId) => Item!;

        public Task<EventType> LockEventTypeAsync(int typeId, CancellationToken cancellationToken = default) => Task.FromResult(Item!);

        public Task<int> CountLinkedEventsAsync(int typeId, CancellationToken cancellationToken = default) => Task.FromResult(LinkedEvents);

        public bool EventTypeExists(int typeId) => Item is not null;

        public bool TypeNameExists(string typeName, int? excludeId = null) => false;

        public Task AddEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default) { Item = eventType; return Task.CompletedTask; }

        public Task UpdateEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default) { Item = eventType; return Task.CompletedTask; }
    }

    private sealed class ImmediateUnitOfWork : IUnitOfWork
    {
        public DbContext Database => throw new NotSupportedException();
        public void SaveChanges() { }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) => work(cancellationToken);
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default) => work(cancellationToken);
    }
}
