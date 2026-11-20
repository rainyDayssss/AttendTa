using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Display-only activity history row.</summary>
public sealed class ActivityLogItemViewModel
{
    public long LogId { get; init; }

    /// <summary>Current actor display name, or the system-process label.</summary>
    public string ActorName { get; init; }

    /// <summary>Friendly action name; the raw code is kept in the details.</summary>
    public string ActionLabel { get; init; }

    /// <summary>
    /// Current name of the affected record. Null when the record cannot be resolved; the view
    /// then shows an unavailable fallback with <see cref="EntityId"/>.
    /// </summary>
    public string AffectedRecord { get; init; }

    /// <summary>Current name of the related record, for example the event of a registration.</summary>
    public string AffectedRecordContext { get; init; }

    public int EntityId { get; init; }

    public DateTime OccurredAt { get; init; }

    /// <summary>Ordered, allowlisted detail rows for the shared Show details dialog.</summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } = new Dictionary<string, string>();
}
