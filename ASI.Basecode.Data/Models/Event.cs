using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

public class Event
{
    public int EventId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public int TypeId { get; set; }
    public int VenueId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int ParticipantLimit { get; set; }
    public EventStatusCode EventStatusId { get; set; }
    public string ImagePath { get; set; }
    public int? PublishedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsCheckInOpen { get; set; }
    public int? CheckInOpenedBy { get; set; }
    public DateTime? CheckInOpenedAt { get; set; }
    public int? CheckInClosedBy { get; set; }
    public DateTime? CheckInClosedAt { get; set; }
    public int? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string CancellationReason { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
