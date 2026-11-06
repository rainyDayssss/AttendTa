using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class EventRepository : BaseRepository, IEventRepository
    {
        public EventRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            GetDbSet<Event>().AsNoTracking().CountAsync(cancellationToken);

        public async Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
            if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));

            var events = GetDbSet<Event>().AsNoTracking();
            var eventTypes = GetDbSet<EventType>().AsNoTracking();
            var venues = GetDbSet<Venue>().AsNoTracking();

            return await (
                from eventItem in events
                join eventType in eventTypes on eventItem.TypeId equals eventType.TypeId
                join venue in venues on eventItem.VenueId equals venue.VenueId
                orderby eventItem.StartsAt ascending, eventItem.EventId ascending
                select new EventListItem
                {
                    EventId = eventItem.EventId,
                    Title = eventItem.Title,
                    TypeName = eventType.TypeName,
                    VenueName = venue.RoomName,
                    StartsAt = eventItem.StartsAt,
                    RegistrationDeadline = eventItem.RegistrationDeadline,
                    ParticipantLimit = eventItem.ParticipantLimit,
                    EventStatusId = eventItem.EventStatusId
                })
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<Event> GetEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            GetDbSet<Event>().AsNoTracking().SingleOrDefaultAsync(eventItem => eventItem.EventId == eventId, cancellationToken);

        public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) =>
            GetDbSet<Event>().AsNoTracking()
                .CountAsync(eventItem => eventItem.EventStatusId == EventStatusCode.Published, cancellationToken);

        public async Task<IReadOnlyList<PublishedEventListItem>> GetPublishedPageAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
            if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));

            var events = GetDbSet<Event>().AsNoTracking();
            var eventTypes = GetDbSet<EventType>().AsNoTracking();
            var venues = GetDbSet<Venue>().AsNoTracking();
            var seatHolders = ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking());

            // Filters apply before paging; ordering ends with the unique event id tiebreak.
            return await (
                from eventItem in events
                join eventType in eventTypes on eventItem.TypeId equals eventType.TypeId
                join venue in venues on eventItem.VenueId equals venue.VenueId
                where eventItem.EventStatusId == EventStatusCode.Published
                orderby eventItem.StartsAt ascending, eventItem.EventId ascending
                select new PublishedEventListItem
                {
                    EventId = eventItem.EventId,
                    Title = eventItem.Title,
                    TypeName = eventType.TypeName,
                    VenueName = venue.RoomName,
                    StartsAt = eventItem.StartsAt,
                    RegistrationDeadline = eventItem.RegistrationDeadline,
                    ParticipantLimit = eventItem.ParticipantLimit,
                    ConfirmedCount = seatHolders.Count(registration => registration.EventId == eventItem.EventId),
                    HasCover = eventItem.ImagePath != null && eventItem.ImagePath.StartsWith("uploads/events/"),
                    EventStatusId = eventItem.EventStatusId
                })
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<PublishedEventDetail> GetPublishedEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var events = GetDbSet<Event>().AsNoTracking();
            var eventTypes = GetDbSet<EventType>().AsNoTracking();
            var venues = GetDbSet<Venue>().AsNoTracking();
            var seatHolders = ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking());

            return await (
                from eventItem in events
                join eventType in eventTypes on eventItem.TypeId equals eventType.TypeId
                join venue in venues on eventItem.VenueId equals venue.VenueId
                where eventItem.EventId == eventId && eventItem.EventStatusId == EventStatusCode.Published
                select new PublishedEventDetail
                {
                    EventId = eventItem.EventId,
                    Title = eventItem.Title,
                    Description = eventItem.Description,
                    TypeName = eventType.TypeName,
                    VenueName = venue.RoomName,
                    StartsAt = eventItem.StartsAt,
                    RegistrationDeadline = eventItem.RegistrationDeadline,
                    ParticipantLimit = eventItem.ParticipantLimit,
                    ConfirmedCount = seatHolders.Count(registration => registration.EventId == eventItem.EventId),
                    HasCover = eventItem.ImagePath != null && eventItem.ImagePath.StartsWith("uploads/events/"),
                    EventStatusId = eventItem.EventStatusId
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        // Event writers must acquire this same event lock before changing competing state.
        public async Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            if (Context.Database.CurrentTransaction == null) throw new InvalidOperationException("A transaction is required to lock an event.");
            var row = await GetDbSet<Event>().FromSqlInterpolated($"SELECT * FROM dbo.events WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE event_id = {eventId}")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var tracked = GetDbSet<Event>().Local.FirstOrDefault(e => e.EventId == eventId);
            if (row == null) { if (tracked != null) Context.Entry(tracked).State = EntityState.Detached; return null; }
            if (tracked == null) { GetDbSet<Event>().Attach(row); return row; }
            Context.Entry(tracked).CurrentValues.SetValues(row);
            Context.Entry(tracked).OriginalValues.SetValues(row);
            Context.Entry(tracked).State = EntityState.Unchanged;
            return tracked;
        }

        public Task AddEventAsync(Event eventItem, CancellationToken cancellationToken = default)
        {
            GetDbSet<Event>().Add(eventItem);
            return Task.CompletedTask;
        }

        public Task UpdateEventAsync(Event eventItem, CancellationToken cancellationToken = default)
        {
            SetEntityState(eventItem, EntityState.Modified);
            return Task.CompletedTask;
        }
    }
}
