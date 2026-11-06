using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Read projection for a single published event. Drafts and other states never match this query,
/// and the private cover path is reduced to a boolean.
/// </summary>
public sealed class PublishedEventDetail
{
    public int EventId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string TypeName { get; set; }
    public string VenueName { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int ParticipantLimit { get; set; }
    public int ConfirmedCount { get; set; }
    public bool HasCover { get; set; }
    public EventStatusCode EventStatusId { get; set; }
}
