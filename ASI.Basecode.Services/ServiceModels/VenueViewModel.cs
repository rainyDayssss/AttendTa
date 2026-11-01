using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class VenueViewModel
    {
        public int VenueId { get; set; }

        [Required(ErrorMessage = "Room name is required.")]
        public string RoomName { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Capacity must be at least 0.")]
        public int Capacity { get; set; }

        public string Location { get; set; }

        public string AvailabilityNotes { get; set; }

        public string ContactPerson { get; set; }
    }
}
