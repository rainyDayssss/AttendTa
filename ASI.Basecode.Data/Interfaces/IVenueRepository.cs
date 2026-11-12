using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IVenueRepository
    {
        Task<IReadOnlyList<Venue>> GetActiveVenuesAsync(CancellationToken cancellationToken = default);
        Task<int> CountAsync(string status, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Venue>> GetPageAsync(string status, int skip, int take, CancellationToken cancellationToken = default);
        Venue GetVenue(int venueId);
        Task<Venue> LockVenueAsync(int venueId, CancellationToken cancellationToken = default);
        Task<int> CountLinkedEventsAsync(int venueId, CancellationToken cancellationToken = default);
        Task<bool> HasEventAboveCapacityAsync(int venueId, int capacity, CancellationToken cancellationToken = default);
        Task AddVenueAsync(Venue venue, CancellationToken cancellationToken = default);
        Task UpdateVenueAsync(Venue venue, CancellationToken cancellationToken = default);
        bool VenueExists(int venueId);
    }
}
