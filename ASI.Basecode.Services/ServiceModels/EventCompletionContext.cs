using System;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// The facts a completion hook receives: the completed event, the Management actor who completed
/// it and the completion time persisted as <c>events.completed_at</c> (AD-2, AD-11).
/// </summary>
public sealed record EventCompletionContext(int EventId, int CompletedBy, DateTime CompletedAt);
