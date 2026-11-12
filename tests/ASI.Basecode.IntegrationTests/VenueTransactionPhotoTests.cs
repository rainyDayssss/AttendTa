using System.Data.Common;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class VenueTransactionPhotoTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly IMapper Mapper = new MapperConfiguration(config =>
    {
        config.CreateMap<Venue, VenueViewModel>();
        config.CreateMap<VenueViewModel, Venue>();
    }).CreateMapper();

    private AsiBasecodeDBContext Context(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(interceptors).Options);

    private sealed class Actor(int id) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => "officer";
    }

    private static VenueService Service(AsiBasecodeDBContext context, IPrivateFileStore files, int actorId, ILogger<VenueService>? logger = null)
    {
        var unit = new UnitOfWork(context);
        var actor = new Actor(actorId);
        return new VenueService(new VenueRepository(unit), unit, Mapper, TimeProvider.System, files,
            new ActivityLogWriter(context, actor, TimeProvider.System), actor, logger ?? NullLogger<VenueService>.Instance);
    }

    private async Task<(int ActorId, int VenueId, int TypeId)> SeedAsync(string photo, CancellationToken ct)
    {
        await using var context = Context();
        var actor = new User { Name = "Venue tester", UserId = $"{Guid.NewGuid():N}@example.invalid", Password = "test", RoleId = RoleCode.Officer, IsActive = true };
        context.Users.Add(actor);
        await context.SaveChangesAsync(ct);
        var venue = new Venue { RoomName = "Original room", Capacity = 40, ImagePath = photo, IsActive = true, CreatedBy = actor.Id, CreatedAt = DateTime.UtcNow };
        var type = new EventType { TypeName = $"Type {Guid.NewGuid():N}", Description = "test", IsActive = true, CreatedBy = actor.Id, CreatedAt = DateTime.UtcNow };
        context.Venues.Add(venue);
        context.EventTypes.Add(type);
        await context.SaveChangesAsync(ct);
        return (actor.Id, venue.VenueId, type.TypeId);
    }

    private static async Task<byte[]> ImageAsync(CancellationToken ct) =>
        await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "TestAssets", "venue-upload.jpg"), ct);

    private static async Task<string> SaveAsync(IPrivateFileStore files, byte[] image, CancellationToken ct)
    {
        await using var stream = new MemoryStream(image);
        return await files.SaveAsync(PrivateFileArea.VenueImages, stream, ".jpg", ct);
    }

    private static string Physical(AttendTaWebApplicationFactory factory, string key) =>
        Path.Combine(factory.SetupRoot, key.Replace('/', Path.DirectorySeparatorChar));

    private static VenueViewModel Model(int id, int capacity = 30) => new() { VenueId = id, RoomName = "Edited room", Capacity = capacity };

    private static async Task AssertPathFreeAuditAsync(AsiBasecodeDBContext context, int venueId, int count, CancellationToken ct)
    {
        var logs = await context.ActivityLogs.AsNoTracking().Where(a => a.EntityType == "venues" && a.EntityId == venueId).ToListAsync(ct);
        Assert.Equal(count, logs.Count);
        foreach (var log in logs)
        {
            Assert.DoesNotContain("uploads/", log.OldValues ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("uploads/", log.NewValues ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    // Signals immediately before the waiting SQL command is sent, not merely when a task is scheduled.
    private sealed class LockCommandSignal : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("venues", StringComparison.OrdinalIgnoreCase)) Started.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    // Throws from EF's actual pre-commit callback after the venue and audit INSERT/UPDATE have reached SQL Server.
    private sealed class FailCommit(int venueId) : DbTransactionInterceptor
    {
        public bool Reached { get; private set; }
        public int VisibleChanges { get; private set; }

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Reached = true;
            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT (SELECT COUNT(*) FROM dbo.venues WHERE venue_id = @id AND room_name = 'Edited room') + (SELECT COUNT(*) FROM dbo.activity_logs WHERE entity_type = 'venues' AND entity_id = @id)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@id";
            parameter.Value = venueId;
            command.Parameters.Add(parameter);
            VisibleChanges = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            throw new InvalidOperationException("Injected commit failure");
        }
    }

    [Fact]
    public async Task BlockedEditWaitsThenRechecksCommittedLimitAndCurrentPhoto()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var image = await ImageAsync(ct);
        var initialPhoto = await SaveAsync(files, image, ct);
        var currentPhoto = await SaveAsync(files, image, ct);
        var (actorId, venueId, typeId) = await SeedAsync(initialPhoto, ct);
        var signal = new LockCommandSignal();
        await using var writer = Context(signal);
        var service = Service(writer, files, actorId);
        Assert.Equal(initialPhoto, service.GetVenue(venueId).ImagePath); // track the state before the competing transaction
        await using var owner = Context();
        var ownerUnit = new UnitOfWork(owner);
        await using var transaction = await owner.Database.BeginTransactionAsync(ct);
        var locked = await new VenueRepository(ownerUnit).LockVenueAsync(venueId, ct);
        locked.ImagePath = currentPhoto;
        owner.Set<Event>().Add(new Event
        {
            Title = "Concurrent draft",
            TypeId = typeId,
            VenueId = venueId,
            StartsAt = DateTime.UtcNow.AddDays(2),
            RegistrationDeadline = DateTime.UtcNow.AddDays(1),
            ParticipantLimit = 30,
            EventStatusId = EventStatusCode.Draft,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        });
        await owner.SaveChangesAsync(ct);

        using var replacement = new MemoryStream(image);
        var pending = service.UpdateVenueAsync(Model(venueId, 29), replacement, ".jpg", ct);
        var committed = false;
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(300, ct)));
            await transaction.CommitAsync(ct);
            committed = true;
            var error = await Assert.ThrowsAsync<DomainException>(() => pending);
            Assert.Equal(OutcomeCode.Invalid, error.Outcome);
            Assert.Equal(nameof(VenueViewModel.Capacity), error.Field);
            await using var observer = Context();
            var unchanged = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venueId, ct);
            Assert.Equal(currentPhoto, unchanged.ImagePath);
            Assert.Equal(40, unchanged.Capacity);
            await AssertPathFreeAuditAsync(observer, venueId, 0, ct);
            Assert.True(File.Exists(Physical(factory, currentPhoto)));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(factory.SetupRoot, "uploads", "venues")).Length);

            // A successful retry must delete the newly committed current photo, not the stale tracked one.
            using var retryPhoto = new MemoryStream(image);
            await service.UpdateVenueAsync(Model(venueId), retryPhoto, ".jpg", ct);
            var updated = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venueId, ct);
            Assert.Equal(30, updated.Capacity);
            Assert.NotEqual(currentPhoto, updated.ImagePath);
            Assert.True(File.Exists(Physical(factory, updated.ImagePath)));
            Assert.False(File.Exists(Physical(factory, currentPhoto)));
            Assert.True(File.Exists(Physical(factory, initialPhoto)));
            await AssertPathFreeAuditAsync(observer, venueId, 1, ct);
        }
        finally
        {
            // Do not leave an in-flight command holding a context open if an assertion fails.
            if (!committed) await transaction.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
        }
    }

    [Fact]
    public async Task CommitFailureRollsBackVenueAndAuditAndDeletesOnlyNewPhoto()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var image = await ImageAsync(ct);
        var oldPhoto = await SaveAsync(files, image, ct);
        var (actorId, venueId, _) = await SeedAsync(oldPhoto, ct);
        var failure = new FailCommit(venueId);
        await using var writer = Context(failure);
        using var replacement = new MemoryStream(image);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(writer, files, actorId).UpdateVenueAsync(Model(venueId), replacement, ".jpg", ct));
        Assert.Equal("Injected commit failure", thrown.Message);
        Assert.True(failure.Reached);
        Assert.Equal(2, failure.VisibleChanges);
        await using var observer = Context();
        var venue = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venueId, ct);
        Assert.Equal("Original room", venue.RoomName);
        Assert.Equal(40, venue.Capacity);
        Assert.Equal(oldPhoto, venue.ImagePath);
        await AssertPathFreeAuditAsync(observer, venueId, 0, ct);
        Assert.True(File.Exists(Physical(factory, oldPhoto)));
        Assert.Equal(image, await File.ReadAllBytesAsync(Physical(factory, oldPhoto), ct));
        Assert.Single(Directory.GetFiles(Path.Combine(factory.SetupRoot, "uploads", "venues")));
    }

    [Fact]
    public async Task BlockedReplacementDeletesThePhotoCurrentAfterLockRelease()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var image = await ImageAsync(ct);
        var initial = await SaveAsync(files, image, ct);
        var current = await SaveAsync(files, image, ct);
        var (actorId, venueId, _) = await SeedAsync(initial, ct);
        var signal = new LockCommandSignal();
        await using var writer = Context(signal);
        var service = Service(writer, files, actorId);
        Assert.Equal(initial, service.GetVenue(venueId).ImagePath);
        await using var owner = Context();
        await using var transaction = await owner.Database.BeginTransactionAsync(ct);
        var row = await new VenueRepository(new UnitOfWork(owner)).LockVenueAsync(venueId, ct);
        row.ImagePath = current;
        await owner.SaveChangesAsync(ct);
        using var photo = new MemoryStream(image);
        var pending = service.UpdateVenueAsync(Model(venueId), photo, ".jpg", ct);
        var committed = false;
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(300, ct)));
            await transaction.CommitAsync(ct);
            committed = true;
            await pending;
            await using var observer = Context();
            var updated = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venueId, ct);
            Assert.NotEqual(current, updated.ImagePath);
            Assert.True(File.Exists(Physical(factory, updated.ImagePath)));
            Assert.False(File.Exists(Physical(factory, current)));
            Assert.True(File.Exists(Physical(factory, initial)));
            await AssertPathFreeAuditAsync(observer, venueId, 1, ct);
        }
        finally
        {
            if (!committed) await transaction.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
        }
    }

    [Fact]
    public async Task ToggleWaitsForConcurrentEditAndPreservesCurrentVenueFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var (actorId, venueId, _) = await SeedAsync("uploads/venues/old.jpg", ct);
        var signal = new LockCommandSignal();
        await using var writer = Context(signal);
        var service = Service(writer, null!, actorId);
        Assert.Equal("uploads/venues/old.jpg", service.GetVenue(venueId).ImagePath);
        await using var owner = Context();
        await using var transaction = await owner.Database.BeginTransactionAsync(ct);
        var row = await new VenueRepository(new UnitOfWork(owner)).LockVenueAsync(venueId, ct);
        row.RoomName = "Concurrent edit";
        row.ImagePath = "uploads/venues/new.jpg";
        row.Capacity = 75;
        await owner.SaveChangesAsync(ct);
        var pending = service.ToggleActiveAsync(venueId, ct);
        var committed = false;
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(300, ct)));
            await transaction.CommitAsync(ct);
            committed = true;
            await pending;
            await using var observer = Context();
            var updated = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venueId, ct);
            Assert.False(updated.IsActive);
            Assert.Equal("Concurrent edit", updated.RoomName);
            Assert.Equal("uploads/venues/new.jpg", updated.ImagePath);
            Assert.Equal(75, updated.Capacity);
            var log = Assert.Single(await observer.ActivityLogs.AsNoTracking()
                .Where(a => a.EntityType == "venues" && a.EntityId == venueId).ToListAsync(ct));
            Assert.Equal("venue.status_changed", log.Action);
            Assert.Contains("\"IsActive\":true", log.OldValues, StringComparison.Ordinal);
            Assert.Contains("\"IsActive\":false", log.NewValues, StringComparison.Ordinal);
            Assert.Contains("\"LinkedEventCount\":0", log.OldValues, StringComparison.Ordinal);
            Assert.Contains("\"LinkedEventCount\":0", log.NewValues, StringComparison.Ordinal);
            Assert.DoesNotContain("uploads/", log.OldValues, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("uploads/", log.NewValues, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!committed) await transaction.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
        }
    }

    [Fact]
    public async Task CancellationWhileBlockedRemovesReplacementWithoutChangingVenueOrAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var image = await ImageAsync(ct);
        var oldPhoto = await SaveAsync(files, image, ct);
        var (actorId, venueId, _) = await SeedAsync(oldPhoto, ct);
        await using var owner = Context();
        await using var transaction = await owner.Database.BeginTransactionAsync(ct);
        await new VenueRepository(new UnitOfWork(owner)).LockVenueAsync(venueId, ct);
        var signal = new LockCommandSignal();
        await using var writer = Context(signal);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var photo = new MemoryStream(image);
        var pending = Service(writer, files, actorId).UpdateVenueAsync(Model(venueId), photo, ".jpg", cancellation.Token);
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(300, ct)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        finally
        {
            cancellation.Cancel();
            await transaction.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
        }
        await using var observer = Context();
        var unchanged = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venueId, ct);
        Assert.Equal(oldPhoto, unchanged.ImagePath);
        Assert.Equal(40, unchanged.Capacity);
        Assert.True(File.Exists(Physical(factory, oldPhoto)));
        Assert.Single(Directory.GetFiles(Path.Combine(factory.SetupRoot, "uploads", "venues")));
        await AssertPathFreeAuditAsync(observer, venueId, 0, ct);
    }

    [Fact]
    public async Task InvalidCreateImageLeavesNoNewVenueFileOrAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var oldPhoto = await SaveAsync(files, await ImageAsync(ct), ct);
        var (actorId, _, _) = await SeedAsync(oldPhoto, ct);
        await using var writer = Context();
        var uniqueName = $"Invalid image {Guid.NewGuid():N}";
        using var invalid = new MemoryStream([1, 2, 3, 4]);
        var error = await Assert.ThrowsAsync<DomainException>(() => Service(writer, files, actorId).AddVenueAsync(
            new VenueViewModel { RoomName = uniqueName, Capacity = 20 }, invalid, ".jpg", ct));
        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal("photo", error.Field);
        await using var observer = Context();
        Assert.False(await observer.Venues.AnyAsync(v => v.RoomName == uniqueName, ct));
        Assert.False(await observer.ActivityLogs.AnyAsync(a => a.EntityType == "venues" && a.ActorUserId == actorId, ct));
        Assert.Single(Directory.GetFiles(Path.Combine(factory.SetupRoot, "uploads", "venues")));
        Assert.True(File.Exists(Physical(factory, oldPhoto)));
    }

    [Fact]
    public async Task ExternalTransactionRejectsCreateAndEditBeforeSavingPhotos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var image = await ImageAsync(ct);
        var oldPhoto = await SaveAsync(files, image, ct);
        var (actorId, venueId, _) = await SeedAsync(oldPhoto, ct);
        await using var writer = Context();
        await using var transaction = await writer.Database.BeginTransactionAsync(ct);
        var service = Service(writer, files, actorId);
        using var createPhoto = new MemoryStream(image);
        using var editPhoto = new MemoryStream(image);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddVenueAsync(
            new VenueViewModel { RoomName = "Must not be created", Capacity = 10 }, createPhoto, ".jpg", ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateVenueAsync(Model(venueId), editPhoto, ".jpg", ct));
        Assert.Equal(0, createPhoto.Position);
        Assert.Equal(0, editPhoto.Position);
        Assert.Single(Directory.GetFiles(Path.Combine(factory.SetupRoot, "uploads", "venues")));
        await transaction.RollbackAsync(ct);
        await using var observer = Context();
        Assert.False(await observer.Venues.AnyAsync(v => v.RoomName == "Must not be created", ct));
        var venue = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venueId, ct);
        Assert.Equal("Original room", venue.RoomName);
        Assert.Equal(oldPhoto, venue.ImagePath);
        await AssertPathFreeAuditAsync(observer, venueId, 0, ct);
    }

    private sealed class DeleteFails(IPrivateFileStore inner) : IPrivateFileStore
    {
        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken ct = default) => inner.SaveAsync(area, content, extension, ct);
        public string CreateTemporaryPath(string extension) => inner.CreateTemporaryPath(extension);
        public Task<string> PromoteAsync(string path, PrivateFileArea area, CancellationToken ct = default) => inner.PromoteAsync(path, area, ct);
        public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) => inner.OpenReadAsync(path, ct);
        public void Delete(string path) => throw new IOException($"Injected cleanup failure: {path}");
    }

    private sealed class RecordingLogger : ILogger<VenueService>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }

    [Fact]
    public async Task OldPhotoCleanupFailureDoesNotUndoCommittedReplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var image = await ImageAsync(ct);
        var oldPhoto = await SaveAsync(files, image, ct);
        var (actorId, venueId, _) = await SeedAsync(oldPhoto, ct);
        await using var writer = Context();
        using var photo = new MemoryStream(image);
        var logger = new RecordingLogger();
        await Service(writer, new DeleteFails(files), actorId, logger).UpdateVenueAsync(Model(venueId), photo, ".jpg", ct);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain(oldPhoto, entry.Message);
        Assert.Null(entry.Exception);
        await using var observer = Context();
        var venue = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venueId, ct);
        Assert.Equal("Edited room", venue.RoomName);
        Assert.NotEqual(oldPhoto, venue.ImagePath);
        Assert.True(File.Exists(Physical(factory, oldPhoto)));
        Assert.True(File.Exists(Physical(factory, venue.ImagePath)));
        await AssertPathFreeAuditAsync(observer, venueId, 1, ct);
    }
}
