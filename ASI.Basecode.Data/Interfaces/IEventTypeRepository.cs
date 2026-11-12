using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEventTypeRepository
    {
        Task<IReadOnlyList<EventType>> GetActiveEventTypesAsync(CancellationToken cancellationToken = default);
        Task<int> CountEventTypesAsync(string status, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EventType>> GetEventTypesPageAsync(string status, int offset, int pageSize, CancellationToken cancellationToken = default);
        EventType GetEventType(int typeId);
        Task<EventType> LockEventTypeAsync(int typeId, CancellationToken cancellationToken = default);
        Task<int> CountLinkedEventsAsync(int typeId, CancellationToken cancellationToken = default);
        Task AddEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default);
        Task UpdateEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default);
        bool EventTypeExists(int typeId);
        bool TypeNameExists(string typeName, int? excludeId = null);
    }
}
