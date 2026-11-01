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
    }
}
