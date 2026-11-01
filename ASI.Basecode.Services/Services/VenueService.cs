using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class VenueService : IVenueService
    {
        private readonly IVenueRepository _repository;
        private readonly IMapper _mapper;

        public VenueService(IVenueRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public List<VenueViewModel> GetVenues()
        {
            var venues = _repository.GetVenues().OrderBy(v => v.RoomName).ToList();
            return _mapper.Map<List<VenueViewModel>>(venues);
        }

        public VenueViewModel GetVenue(int id)
        {
            var venue = _repository.GetVenue(id);
            return venue == null ? null : _mapper.Map<VenueViewModel>(venue);
        }

        public void AddVenue(VenueViewModel model)
        {
            var venue = new Venue();
            _mapper.Map(model, venue);
            _repository.AddVenue(venue);
        }

        public void UpdateVenue(VenueViewModel model)
        {
            var venue = _repository.GetVenue(model.VenueId);
            if (venue == null)
            {
                throw new InvalidDataException("Venue not found.");
            }

            _mapper.Map(model, venue);
            _repository.UpdateVenue(venue);
        }
    }
}
