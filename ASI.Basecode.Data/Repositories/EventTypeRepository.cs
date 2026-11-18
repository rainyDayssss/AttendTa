using ASI.Basecode.Data.Queries;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class EventTypeRepository : BaseRepository, IEventTypeRepository
    {
        private readonly ListReadOptions _readOptions;
        public EventTypeRepository(IUnitOfWork unitOfWork, ListReadOptions readOptions = null) : base(unitOfWork)
        {
            _readOptions = readOptions;
        }

        public async Task<IReadOnlyList<EventType>> GetActiveEventTypesAsync(CancellationToken cancellationToken = default)
        {
            return await this.GetDbSet<EventType>()
                .AsNoTracking()
                .Where(eventType => eventType.IsActive)
                .OrderBy(eventType => eventType.TypeName)
                .ThenBy(eventType => eventType.TypeId)
                .ToListAsync(cancellationToken);
        }

        public Task<int> CountEventTypesAsync(string status, CancellationToken cancellationToken = default)
        {
            return FilterEventTypes(status).CountAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<EventType>> GetEventTypesPageAsync(string status, int offset, int pageSize, CancellationToken cancellationToken = default)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

            return await FilterEventTypes(status)
                .OrderBy(eventType => eventType.TypeName)
                .ThenBy(eventType => eventType.TypeId)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        private IQueryable<EventType> FilterEventTypes(string status)
        {
            var query = this.GetDbSet<EventType>().AsNoTracking();
            if (_readOptions?.IsSurface("EventType") == true && !string.IsNullOrWhiteSpace(_readOptions.Search)) {
                var term = _readOptions.Search;
                query = query.Where(row => row.TypeName.Contains(term) || (row.Description != null && row.Description.Contains(term)));
            }
            return status switch
            {
                "active" => query.Where(eventType => eventType.IsActive),
                "inactive" => query.Where(eventType => !eventType.IsActive),
                _ => query
            };
        }

        public EventType GetEventType(int typeId)
        {
            return this.GetDbSet<EventType>().FirstOrDefault(t => t.TypeId == typeId);
        }

        // Event writers must acquire this same event type lock before changing competing references.
        public async Task<EventType> LockEventTypeAsync(int typeId, CancellationToken cancellationToken = default)
        {
            if (Context.Database.CurrentTransaction == null) throw new InvalidOperationException("A transaction is required to lock an event type.");
            var row = await GetDbSet<EventType>().FromSqlInterpolated($"SELECT * FROM dbo.event_types WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE type_id = {typeId}")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var tracked = GetDbSet<EventType>().Local.FirstOrDefault(t => t.TypeId == typeId);
            if (row == null) { if (tracked != null) Context.Entry(tracked).State = EntityState.Detached; return null; }
            if (tracked == null) { GetDbSet<EventType>().Attach(row); return row; }
            Context.Entry(tracked).CurrentValues.SetValues(row);
            Context.Entry(tracked).OriginalValues.SetValues(row);
            Context.Entry(tracked).State = EntityState.Unchanged;
            return tracked;
        }

        public Task<int> CountLinkedEventsAsync(int typeId, CancellationToken cancellationToken = default)
        {
            return this.GetDbSet<Event>().CountAsync(e => e.TypeId == typeId, cancellationToken);
        }

        public Task AddEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default)
        {
            this.GetDbSet<EventType>().Add(eventType);
            return Task.CompletedTask;
        }

        public Task UpdateEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default)
        {
            this.SetEntityState(eventType, EntityState.Modified);
            return Task.CompletedTask;
        }

        public bool EventTypeExists(int typeId)
        {
            return this.GetDbSet<EventType>().Any(t => t.TypeId == typeId);
        }

        public bool TypeNameExists(string typeName, int? excludeId = null)
        {
            return this.GetDbSet<EventType>().Any(t => t.TypeName == typeName &&
                                                        (!excludeId.HasValue || t.TypeId != excludeId.Value));
        }
    }
}
