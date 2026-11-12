using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEventTypeService
    {
        Task<PagedResult<EventTypeViewModel>> GetPageAsync(string status, int? page, CancellationToken cancellationToken = default);
        EventTypeViewModel GetEventType(int id);
        Task AddEventTypeAsync(EventTypeViewModel model, CancellationToken cancellationToken = default);
        Task UpdateEventTypeAsync(EventTypeViewModel model, CancellationToken cancellationToken = default);
        Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default);
    }
}
