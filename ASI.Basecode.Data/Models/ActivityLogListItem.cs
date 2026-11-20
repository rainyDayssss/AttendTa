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

    /// <summary>Recorded audit snapshot, read only to build allowlisted display changes.</summary>
    public string OldValues { get; set; }

    /// <summary>Recorded audit snapshot, read only to build allowlisted display changes.</summary>
    public string NewValues { get; set; }

    /// <summary>
    /// Current name of the affected record, resolved from the live relation. Null when the
    /// relation is gone or the entity type is unknown; callers must fall back to the raw id.
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// Current name of the related record (for example the event of a registration). Null when
    /// the row has no related record or the relation cannot be resolved.
    /// </summary>
    public string TargetContext { get; set; }
}
