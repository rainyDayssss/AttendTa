using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>An active Event Type offered by the draft event form.</summary>
public sealed record EventTypeOption(int TypeId, string TypeName);

/// <summary>An active Venue offered by the draft event form, with the capacity the limit is checked against.</summary>
public sealed record VenueOption(int VenueId, string RoomName, int Capacity);

/// <summary>The active references a draft event may lock and select.</summary>
public sealed class EventFormOptions
{
    public IReadOnlyList<EventTypeOption> EventTypes { get; init; } = [];

    public IReadOnlyList<VenueOption> Venues { get; init; } = [];
}
