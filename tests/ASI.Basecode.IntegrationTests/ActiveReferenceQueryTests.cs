using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class ActiveReferenceQueryTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private AsiBasecodeDBContext Context() => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    [Fact]
    public async Task ActiveReferenceQueriesFilterOrderAndPreserveInactiveLookups()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var actorId = await EventTypeTestSupport.SeedActorAsync(fixture.ConnectionString, cancellationToken);

        await using var context = Context();
        var eventTypeRepository = new EventTypeRepository(new UnitOfWork(context));
        var venueRepository = new VenueRepository(new UnitOfWork(context));

        Assert.Empty(await eventTypeRepository.GetActiveEventTypesAsync(cancellationToken));
        Assert.Empty(await venueRepository.GetActiveVenuesAsync(cancellationToken));

        var suffix = Guid.NewGuid().ToString("N");
        var eventTypes = new[]
        {
            new EventType
            {
                TypeName = $"Active type A {suffix}",
                Description = "Active",
                IsActive = true,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            },
            new EventType
            {
                TypeName = $"Inactive type {suffix}",
                Description = "Inactive",
                IsActive = false,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            },
            new EventType
            {
                TypeName = $"Active type Z {suffix}",
                Description = "Active",
                IsActive = true,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            }
        };
        var venues = new[]
        {
            new Venue
            {
                RoomName = $"Active room A {suffix}",
                Capacity = 20,
                Location = "Main Building",
                ImagePath = "uploads/venues/test.jpg",
                IsActive = true,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            },
            new Venue
            {
                RoomName = $"Inactive room {suffix}",
                Capacity = 20,
                Location = "Main Building",
                ImagePath = "uploads/venues/test.jpg",
                IsActive = false,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            },
            new Venue
            {
                RoomName = $"Active room Z {suffix}",
                Capacity = 20,
                Location = "Main Building",
                ImagePath = "uploads/venues/test.jpg",
                IsActive = true,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            }
        };
        context.EventTypes.AddRange(eventTypes);
        context.Venues.AddRange(venues);
        await context.SaveChangesAsync(cancellationToken);

        var activeEventTypes = await eventTypeRepository.GetActiveEventTypesAsync(cancellationToken);
        Assert.Equal(new[] { eventTypes[0].TypeId, eventTypes[2].TypeId }, activeEventTypes.Select(type => type.TypeId));
        Assert.All(activeEventTypes, type => Assert.Equal(EntityState.Detached, context.Entry(type).State));

        var activeVenues = await venueRepository.GetActiveVenuesAsync(cancellationToken);
        Assert.Equal(new[] { venues[0].VenueId, venues[2].VenueId }, activeVenues.Select(venue => venue.VenueId));
        Assert.All(activeVenues, venue => Assert.Equal(EntityState.Detached, context.Entry(venue).State));

        var inactiveType = eventTypeRepository.GetEventType(eventTypes[1].TypeId);
        Assert.NotNull(inactiveType);
        Assert.False(inactiveType.IsActive);

        var inactiveVenue = venueRepository.GetVenue(venues[1].VenueId);
        Assert.NotNull(inactiveVenue);
        Assert.False(inactiveVenue.IsActive);

        eventTypes[0].IsActive = false;
        eventTypes[2].IsActive = false;
        venues[0].IsActive = false;
        venues[2].IsActive = false;
        await context.SaveChangesAsync(cancellationToken);

        Assert.Empty(await eventTypeRepository.GetActiveEventTypesAsync(cancellationToken));
        Assert.Empty(await venueRepository.GetActiveVenuesAsync(cancellationToken));
    }
}
