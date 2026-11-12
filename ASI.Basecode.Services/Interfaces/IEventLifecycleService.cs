using ASI.Basecode.Services.ServiceModels;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Management event lifecycle: the event list, draft creation, draft editing, the title and
    /// description edits a published event still allows, publish, cancel, complete, the check-in
    /// window, and cover management.
    /// </summary>
    public interface IEventLifecycleService
    {
        Task<PagedResult<EventViewModel>> GetPageAsync(int? pageNumber, CancellationToken cancellationToken = default);
        Task<EventViewModel> GetEventAsync(int id, CancellationToken cancellationToken = default);
        Task<EventFormOptions> GetFormOptionsAsync(CancellationToken cancellationToken = default);
        Task<int> CreateDraftAsync(EventViewModel model, CancellationToken cancellationToken = default);
        Task UpdateDraftAsync(EventViewModel model, CancellationToken cancellationToken = default);
        Task PublishAsync(int eventId, CancellationToken cancellationToken = default);
        Task CancelAsync(int eventId, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes a published event at or after its start, closing check-in, recording the
        /// completion metadata and invoking every <see cref="IEventCompletionHandler"/> in the same
        /// transaction.
        /// </summary>
        Task CompleteAsync(int eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens or closes the check-in window of a published event, recording the actor and time
        /// and appending one audit entry in the same transaction.
        /// </summary>
        Task SetCheckInWindowAsync(int eventId, bool open, CancellationToken cancellationToken = default);

        Task<EventCoverViewModel> GetCoverManagementAsync(int eventId, CancellationToken cancellationToken = default);
        Task<EventCoverStream> OpenCoverAsync(int eventId, CancellationToken cancellationToken = default);
        Task UploadCoverAsync(int eventId, Stream image, string extension, CancellationToken cancellationToken = default);
        Task RemoveDraftCoverAsync(int eventId, CancellationToken cancellationToken = default);
    }
}
