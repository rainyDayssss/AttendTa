using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Data.Models;

public class ActivityLog
{
    public long LogId { get; set; }
    public ActorTypeCode ActorTypeId { get; set; }
    public int? ActorUserId { get; set; }
    public string Action { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public string Description { get; set; }
    public string Reason { get; set; }
    public string OldValues { get; set; }
    public string NewValues { get; set; }
    public DateTime OccurredAt { get; set; }
}
