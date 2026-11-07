using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEventRepository
    {
        Task<int> CountAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default);
        Task<Event> GetEventAsync(int eventId, CancellationToken cancellationToken = default);
        Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default);
        Task AddEventAsync(Event eventItem, CancellationToken cancellationToken = default);
        Task UpdateEventAsync(Event eventItem, CancellationToken cancellationToken = default);

        /// <summary>Counts only published events; draft and other states are excluded before paging.</summary>
        Task<int> CountPublishedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns one published-event page ordered by start then event id. Confirmed seats come from
        /// the canonical seat fact and the private cover path is not projected.
        /// </summary>
        Task<IReadOnlyList<PublishedEventListItem>> GetPublishedPageAsync(int skip, int take, CancellationToken cancellationToken = default);

        /// <summary>Returns a published event, or <c>null</c> when it does not exist or is not published.</summary>
        Task<PublishedEventDetail> GetPublishedEventAsync(int eventId, CancellationToken cancellationToken = default);

        /// <summary>Counts the events a member may see: published or completed.</summary>
        Task<int> CountMemberVisibleAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns one member-visible event page (published or completed) ordered by start then event
        /// id. The published-only reads stay in place for check-in.
        /// </summary>
        Task<IReadOnlyList<PublishedEventListItem>> GetMemberVisiblePageAsync(int skip, int take, CancellationToken cancellationToken = default);

        /// <summary>Returns a published or completed event, or <c>null</c> when it is not member-visible.</summary>
        Task<PublishedEventDetail> GetMemberVisibleEventAsync(int eventId, CancellationToken cancellationToken = default);
    }
}
