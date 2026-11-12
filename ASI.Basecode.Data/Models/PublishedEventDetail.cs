using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Read projection for a single member-visible event. The published-only query matches Published
/// rows; the member-visible query also matches Completed rows. Drafts, cancelled and other states
/// never match, and the private cover path is reduced to a boolean.
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
    public bool IsCheckInOpen { get; set; }
}
