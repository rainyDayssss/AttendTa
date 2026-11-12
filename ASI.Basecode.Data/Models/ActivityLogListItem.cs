using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

/// <summary>Read-only activity-log fields safe for the administrator history view.</summary>
public sealed class ActivityLogListItem
{
    public long LogId { get; set; }
    public ActorTypeCode ActorType { get; set; }
    public string ActorName { get; set; }
    public string Action { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public string Reason { get; set; }
    public DateTime OccurredAt { get; set; }
}
