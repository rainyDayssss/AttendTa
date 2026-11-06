using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Read projection for the participant-facing published event feed. The private cover path is
/// reduced to a boolean so views never receive a storage key.
/// </summary>
public sealed class PublishedEventListItem
{
    public int EventId { get; set; }
    public string Title { get; set; }
    public string TypeName { get; set; }
    public string VenueName { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int ParticipantLimit { get; set; }
    public int ConfirmedCount { get; set; }
    public bool HasCover { get; set; }
    public EventStatusCode EventStatusId { get; set; }
}
