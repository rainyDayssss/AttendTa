using ASI.Basecode.Data.Models.Lookups;
using System;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>Form and list model for the Management event screens.</summary>
    public class EventViewModel
    {
        public int EventId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters.")]
        public string Title { get; set; }

        public string Description { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select an event type.")]
        public int TypeId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a venue.")]
        public int VenueId { get; set; }

        public DateTime StartsAt { get; set; }

        public DateTime RegistrationDeadline { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Participant limit must be greater than 0.")]
        public int ParticipantLimit { get; set; }

        public EventStatusCode EventStatusId { get; set; }

        /// <summary>True when the event's check-in window is open.</summary>
        public bool IsCheckInOpen { get; set; }

        public string CheckInOpenedByName { get; set; }

        public DateTime? CheckInOpenedAt { get; set; }

        public string CheckInClosedByName { get; set; }

        public DateTime? CheckInClosedAt { get; set; }

        public string TypeName { get; set; }

        public string VenueName { get; set; }

        /// <summary>Active Event Types and Venues offered by the form. Populated by the controller.</summary>
        public EventFormOptions FormOptions { get; set; } = new();

        public string StatusLabel => EventStatusDisplay.Label(EventStatusId);
    }
}
