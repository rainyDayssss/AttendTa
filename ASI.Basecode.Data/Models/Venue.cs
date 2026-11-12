using System;

namespace ASI.Basecode.Data.Models
{
    public partial class Venue
    {
        public int VenueId { get; set; }

        public string RoomName { get; set; }

        public int Capacity { get; set; }

        public string Location { get; set; }

        public string AvailabilityNotes { get; set; }

        public string ContactPerson { get; set; }

        public bool IsActive { get; set; }

        public string ImagePath { get; set; }

        public int CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
