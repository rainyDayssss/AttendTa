using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Advisory display only; cancellation commands recheck all facts transactionally.</summary>
public static class RegistrationCancellationAvailability
{
    public static string Reason(EventStatusCode status, DateTime startsAt, bool validAttendance, DateTime nowUtc)
    {
        var blockers = new List<string>();
        if (status != EventStatusCode.Published)
            blockers.Add($"Event {status.ToString().ToLowerInvariant()}");
        if (ParticipationFacts.HasStartCutoffPassed(startsAt, nowUtc))
            blockers.Add("event has started");
        if (validAttendance)
            blockers.Add("valid attendance recorded");
        return blockers.Count == 0 ? null : $"Cancellation unavailable: {string.Join("; ", blockers)}.";
    }
}
