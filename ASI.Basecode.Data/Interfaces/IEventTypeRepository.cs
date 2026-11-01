using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEventTypeRepository
    {
        IQueryable<EventType> GetEventTypes();
        EventType GetEventType(int typeId);
        void AddEventType(EventType eventType);
        void UpdateEventType(EventType eventType);
        bool EventTypeExists(int typeId);
        bool TypeNameExists(string typeName, int? excludeId = null);
    }
}
