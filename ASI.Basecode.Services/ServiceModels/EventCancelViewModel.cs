using ASI.Basecode.Data.Models.Lookups;
using System;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Management cancellation confirmation. It carries only what the confirmation screen shows; the
/// actor and the cancellation metadata are read and written by the service.
/// </summary>
public sealed class EventCancelViewModel
{
    public int EventId { get; set; }

    public string Title { get; set; }

    public EventStatusCode EventStatusId { get; set; }

    public DateTime StartsAt { get; set; }

    /// <summary>Trimmed before validation; the service also rejects a blank value inside the transaction.</summary>
    [Required(ErrorMessage = "A cancellation reason is required.")]
    public string Reason { get; set; }

    public string StatusLabel => EventStatusId switch
    {
        EventStatusCode.Draft => "Draft",
        EventStatusCode.Published => "Published",
        EventStatusCode.Cancelled => "Cancelled",
        EventStatusCode.Completed => "Completed",
        _ => EventStatusId.ToString()
    };
}
