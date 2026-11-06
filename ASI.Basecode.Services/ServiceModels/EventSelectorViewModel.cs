using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Shared labelled GET event selector used by the scanner, certificate ledger and participation
/// report. It renders nothing until a surface supplies options, so the shell stays empty.
/// </summary>
public sealed class EventSelectorViewModel
{
    public string Label { get; init; } = "Event";
    public string Name { get; init; } = "eventId";

    /// <summary>Optional GET target; when empty the selector posts back to the current path.</summary>
    public string FormAction { get; init; }

    public int? SelectedEventId { get; init; }
    public IReadOnlyList<EventSelectorOption> Options { get; init; } = Array.Empty<EventSelectorOption>();

    /// <summary>Extra query values the selector must carry, rendered as hidden fields.</summary>
    public IReadOnlyDictionary<string, string> HiddenValues { get; init; } = new Dictionary<string, string>();

    /// <summary>True only when there is at least one selectable event.</summary>
    public bool HasOptions => Options is { Count: > 0 };

    public static EventSelectorViewModel Empty() => new();
}

/// <summary>One selectable event in the shared selector.</summary>
public sealed record EventSelectorOption(int EventId, string Title);
