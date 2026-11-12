using ASI.Basecode.Data.Models.Lookups;
using System.IO;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Management cover screen state. It intentionally contains no private storage key.</summary>
public sealed class EventCoverViewModel
{
    public int EventId { get; init; }
    public string Title { get; init; }
    public EventStatusCode EventStatusId { get; init; }
    public bool HasCover { get; init; }

    public string StatusLabel => EventStatusId switch
    {
        EventStatusCode.Draft => "Draft",
        EventStatusCode.Published => "Published",
        EventStatusCode.Cancelled => "Cancelled",
        EventStatusCode.Completed => "Completed",
        _ => EventStatusId.ToString()
    };

    public bool CanUpload => EventStatusId is EventStatusCode.Draft or EventStatusCode.Published;
    public bool CanRemove => EventStatusId == EventStatusCode.Draft && HasCover;
}

/// <summary>A stream and safe media type for an authorized event-cover response.</summary>
public sealed record EventCoverStream(Stream Content, string ContentType);
