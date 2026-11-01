using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class VenueRepository : BaseRepository, IVenueRepository
    {
        public VenueRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<Venue> GetVenues()
        {
            return this.GetDbSet<Venue>();
        }

        public Venue GetVenue(int venueId)
        {
            return this.GetDbSet<Venue>().FirstOrDefault(v => v.VenueId == venueId);
        }

        public void AddVenue(Venue venue)
        {
            this.GetDbSet<Venue>().Add(venue);
            UnitOfWork.SaveChanges();
        }

        public void UpdateVenue(Venue venue)
        {
            this.SetEntityState(venue, EntityState.Modified);
            UnitOfWork.SaveChanges();
        }

        public bool VenueExists(int venueId)
        {
            return this.GetDbSet<Venue>().Any(v => v.VenueId == venueId);
        }
    }
}
