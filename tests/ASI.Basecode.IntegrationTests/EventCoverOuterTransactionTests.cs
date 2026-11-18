using System.Security.Cryptography;
using System.Text;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventCoverOuterTransactionTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private const string OldCover = "uploads/events/ffffffffffffffffffffffffffffffff.jpg";
    private const string NewCover = "uploads/events/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg";

    [Theory]
    [InlineData("upload")]
    [InlineData("remove")]
    [InlineData("combined")]
    public async Task OuterRollbackKeepsOriginalCoverAndAnyStagedFile(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        int typeId, venueId;
        int eventId;
        await using (var seed = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var suffix = Guid.NewGuid().ToString("N");
            var type = new EventType { TypeName = $"Type {suffix}", Description = "Test type", IsActive = true, CreatedBy = actor, CreatedAt = DateTime.UtcNow };
            var venue = new Venue { RoomName = $"Room {suffix}", Capacity = 30, ImagePath = "uploads/venues/test.jpg", IsActive = true, CreatedBy = actor, CreatedAt = DateTime.UtcNow };
            seed.EventTypes.Add(type);
            seed.Venues.Add(venue);
            await seed.SaveChangesAsync(ct);
            typeId = type.TypeId;
            venueId = venue.VenueId;
            var item = new Event
            {
                Title = "Outer transaction cover", TypeId = typeId, VenueId = venueId,
                StartsAt = new DateTime(2027, 1, 10, 1, 0, 0),
                RegistrationDeadline = new DateTime(2027, 1, 9, 1, 0, 0),
                ParticipantLimit = 10, EventStatusId = EventStatusCode.Draft,
                ImagePath = OldCover, CreatedBy = actor, CreatedAt = DateTime.UtcNow
            };
            seed.Events.Add(item);
            await seed.SaveChangesAsync(ct);
            eventId = item.EventId;
        }

        var files = new RecordingFiles();
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        await using (var transaction = await context.Database.BeginTransactionAsync(ct))
        {
            var service = EventDraftTestSupport.Service(context, actor, files: files);
            if (operation == "upload")
                await service.UploadCoverAsync(eventId, new MemoryStream([1]), ".jpg", ct);
            else if (operation == "remove")
                await service.RemoveDraftCoverAsync(eventId, ct);
            else
                await service.UpdateDraftWithCoverAsync(new EventViewModel
                {
                    EventId = eventId, Title = "Edited", TypeId = typeId, VenueId = venueId,
                    StartsAt = new DateTime(2027, 1, 10, 1, 0, 0),
                    RegistrationDeadline = new DateTime(2027, 1, 9, 1, 0, 0),
                    ParticipantLimit = 10, EventStatusId = EventStatusCode.Draft,
                    CoverVersion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(OldCover)))
                }, new MemoryStream([1]), ".jpg", false, ct);

            var changed = await context.Events.AsNoTracking().SingleAsync(item => item.EventId == eventId, ct);
            Assert.Equal(operation == "remove" ? null : NewCover, changed.ImagePath);
            Assert.Equal(operation == "combined" ? "Edited" : "Outer transaction cover", changed.Title);
            Assert.Equal(1, await context.ActivityLogs.CountAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
            Assert.Empty(files.Deleted);
            Assert.Contains(OldCover, files.Paths);
            if (operation != "remove") Assert.Contains(NewCover, files.Paths);
            await transaction.RollbackAsync(ct);
        }

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(item => item.EventId == eventId, ct);
        Assert.Equal(OldCover, persisted.ImagePath);
        Assert.Equal("Outer transaction cover", persisted.Title);
        Assert.False(await observer.ActivityLogs.AnyAsync(log => log.EntityType == "events" && log.EntityId == eventId, ct));
        Assert.Empty(files.Deleted);
        Assert.Contains(OldCover, files.Paths);
        if (operation != "remove") Assert.Contains(NewCover, files.Paths);
    }

    private sealed class RecordingFiles : IPrivateFileStore
    {
        public HashSet<string> Paths { get; } = [OldCover];
        public List<string> Deleted { get; } = [];
        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default)
        {
            Assert.Equal(PrivateFileArea.EventImages, area);
            Paths.Add(NewCover);
            return Task.FromResult(NewCover);
        }
        public void Delete(string relativePath) { Deleted.Add(relativePath); Paths.Remove(relativePath); }
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();
        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
