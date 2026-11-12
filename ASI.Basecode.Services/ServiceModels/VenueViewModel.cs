using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    public class VenueViewModel
    {
        public int VenueId { get; set; }

        [Required(ErrorMessage = "Room name is required.")]
        [StringLength(150, ErrorMessage = "Room name cannot exceed 150 characters.")]
        public string RoomName { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than 0.")]
        public int Capacity { get; set; }

        [StringLength(255)]
        public string Location { get; set; }

        public string AvailabilityNotes { get; set; }

        [StringLength(150)]
        public string ContactPerson { get; set; }

        public bool IsActive { get; set; } = true;

        public string ImagePath { get; set; }
    }
}
