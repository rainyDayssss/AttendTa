using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

public sealed class EventDossierViewModel
{
    public EventViewModel Event { get; set; }
    public PagedResult<RegistrationReviewRow> Roster { get; set; }
    public string Status { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int WaitlistedCount { get; set; }
    public bool HasStarted { get; set; }
    public bool CanReview => Event.EventStatusId == EventStatusCode.Published && !HasStarted;
}
