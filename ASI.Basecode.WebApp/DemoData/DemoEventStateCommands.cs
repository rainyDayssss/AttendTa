using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo check-in window and completion commands (register M-04 and M-09). Every command reloads the
/// real event and applies the same refusal rules 4.2 and 3.12 will, then changes only the in-memory
/// store: no SQL row, no stored file and no Activity Log entry (register rules 4 and 8).
/// </summary>
public sealed class DemoEventStateCommands : IEventStateCommands
{
    private readonly IEventRepository _events;
    private readonly DemoStore _store;
    private readonly TimeProvider _timeProvider;

    public DemoEventStateCommands(IEventRepository events, DemoStore store, TimeProvider timeProvider)
    {
        _events = events;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task OpenCheckInAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var eventItem = await RequirePublishedAsync(eventId, cancellationToken);
        if (_store.IsCompleted(eventItem.EventId))
        {
            throw new DomainException(OutcomeCode.Invalid, "Check-in cannot be opened for a completed event.");
        }

        _store.SetCheckInOpen(eventItem.EventId, true);
    }

    public async Task CloseCheckInAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var eventItem = await RequirePublishedAsync(eventId, cancellationToken);
        if (_store.IsCompleted(eventItem.EventId))
        {
            throw new DomainException(OutcomeCode.Invalid, "Check-in is already closed because the event is completed.");
        }

        _store.SetCheckInOpen(eventItem.EventId, false);
    }

    public async Task CompleteAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var eventItem = await RequirePublishedAsync(eventId, cancellationToken);
        if (_store.IsCompleted(eventItem.EventId))
        {
            throw new DomainException(OutcomeCode.Invalid, "This event is already completed.");
        }

        if (_timeProvider.GetUtcNow().UtcDateTime < eventItem.StartsAt)
        {
            throw new DomainException(OutcomeCode.Invalid, "An event cannot be completed before it starts.");
        }

        _store.MarkCompleted(eventItem.EventId);
    }

    private async Task<Event> RequirePublishedAsync(int eventId, CancellationToken cancellationToken)
    {
        var eventItem = await _events.GetEventAsync(eventId, cancellationToken);
        if (eventItem == null)
        {
            throw new DomainException(OutcomeCode.NotFound, "Event not found.");
        }

        if (eventItem.EventStatusId != EventStatusCode.Published)
        {
            throw new DomainException(OutcomeCode.Invalid, "This action is only available for a published event.");
        }

        return eventItem;
    }
}
