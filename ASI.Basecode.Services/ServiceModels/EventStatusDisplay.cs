using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// The single rule for the event lifecycle label and badge shared by the management list, the
/// member feed and the member event detail. Kept beside <see cref="RegistrationStatusDisplay"/> so
/// the same status never renders two ways.
/// </summary>
public static class EventStatusDisplay
{
    /// <summary>The visible lifecycle label for the event status.</summary>
    public static string Label(EventStatusCode status) => status switch
    {
        EventStatusCode.Draft => "Draft",
        EventStatusCode.Published => "Published",
        EventStatusCode.Cancelled => "Cancelled",
        EventStatusCode.Completed => "Completed",
        _ => status.ToString()
    };

    /// <summary>The compiled badge class for the same lifecycle label.</summary>
    public static string BadgeClass(EventStatusCode status) => status switch
    {
        EventStatusCode.Draft => "badge-closed",
        EventStatusCode.Published => "badge-open",
        EventStatusCode.Cancelled => "badge-inactive",
        EventStatusCode.Completed => "badge-completed",
        _ => "badge-member"
    };
}
