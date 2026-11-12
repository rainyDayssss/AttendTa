using System;

namespace ASI.Basecode.Resources;

public enum OutcomeCode { Invalid, Forbidden, NotFound, Stale, Duplicate, CapacityUnavailable, CutoffPassed, FileFailure }

public sealed class DomainException : Exception
{
    public OutcomeCode Outcome { get; }
    public string? Field { get; }

    public DomainException(OutcomeCode outcome, string message, string? field = null) : base(message)
    {
        Outcome = outcome;
        Field = field;
    }
}
