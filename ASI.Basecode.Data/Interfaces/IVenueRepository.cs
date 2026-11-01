using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IVenueRepository
    {
        IQueryable<Venue> GetVenues();
        Venue GetVenue(int venueId);
        void AddVenue(Venue venue);
        void UpdateVenue(Venue venue);
        bool VenueExists(int venueId);
    }
}
