using System;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Display-only activity history row.</summary>
public sealed class ActivityLogItemViewModel
{
    public long LogId { get; init; }
    public string ActorType { get; init; }
    public string ActorName { get; init; }
    public string Action { get; init; }
    public string EntityType { get; init; }
    public int EntityId { get; init; }
    public string Reason { get; init; }
    public DateTime OccurredAt { get; init; }
}
