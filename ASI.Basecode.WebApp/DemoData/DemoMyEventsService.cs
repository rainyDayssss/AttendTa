using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo My Events registrations read (register M-02). No cross-event owner read exists yet, so it
/// composes existing reads: the actor's open event ids, each event's own registration row, and one
/// event page for title, type, venue, start and status. It never writes, and 3.13 replaces this
/// provider with the real query behind the same contract.
/// </summary>
public sealed class DemoMyEventsService : IMyEventsService
{
    private readonly IRegistrationRepository _registrations;
    private readonly IEventRepository _events;
    private readonly ICurrentActor _actor;
    private readonly TimeProvider _timeProvider;

    public DemoMyEventsService(
        IRegistrationRepository registrations,
        IEventRepository events,
        ICurrentActor actor,
        TimeProvider timeProvider)
    {
        _registrations = registrations;
        _events = events;
        _actor = actor;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResult<MyEventRegistrationCardViewModel>> GetRegistrationsAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        // The member policy guards the route; the actor is the only source of ownership.
        var userId = _actor.UserId;

        var eventIds = await _registrations.DiscoverOpenEventIdsForUserAsync(userId, cancellationToken);
        var totalEvents = await _events.CountAsync(cancellationToken);
        if (eventIds.Count == 0 || totalEvents == 0)
        {
            return EmptyPage();
        }

        // One read supplies title, type, venue, start and status for every registered event.
        var eventRows = await _events.GetPageAsync(0, totalEvents, cancellationToken);
        var eventsById = eventRows.ToDictionary(row => row.EventId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cards = new List<MyEventRegistrationCardViewModel>(eventIds.Count);
        foreach (var eventId in eventIds)
        {
            if (!eventsById.TryGetValue(eventId, out var eventRow))
            {
                continue;
            }

            var registration = await _registrations.GetMemberRegistrationAsync(eventId, userId, cancellationToken);
            if (registration == null)
            {
                continue;
            }

            var startCutoffPassed = now >= eventRow.StartsAt;
            cards.Add(new MyEventRegistrationCardViewModel
            {
                RegistrationId = registration.RegistrationId,
                EventId = eventRow.EventId,
                EventTitle = eventRow.Title,
                TypeName = eventRow.TypeName,
                VenueName = eventRow.VenueName,
                StartsAt = eventRow.StartsAt,
                StatusLabel = RegistrationStatusDisplay.Label(registration.Status, startCutoffPassed),
                StatusBadgeClass = RegistrationStatusDisplay.BadgeClass(registration.Status, startCutoffPassed),
                CanViewEvent = eventRow.EventStatusId == EventStatusCode.Published
            });
        }

        var ordered = cards
            .OrderByDescending(card => card.StartsAt)
            .ThenByDescending(card => card.RegistrationId)
            .ToList();

        var pageRequest = new PageRequest(pageNumber);
        var normalizedPage = pageRequest.NormalizePageNumber(ordered.Count);
        var pageItems = ordered
            .Skip(pageRequest.GetOffset(normalizedPage))
            .Take(PageRequest.FixedPageSize)
            .ToList();

        return new PagedResult<MyEventRegistrationCardViewModel>(pageItems, normalizedPage, ordered.Count);
    }

    private static PagedResult<MyEventRegistrationCardViewModel> EmptyPage() =>
        new(Array.Empty<MyEventRegistrationCardViewModel>(), 1, 0);
}
