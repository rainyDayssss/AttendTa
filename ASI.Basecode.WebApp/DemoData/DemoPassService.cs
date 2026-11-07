using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo door passes (register M-03). No owner-scoped cross-event query exists yet, so the provider
/// composes the existing owner-scoped reads (register rule 1): it discovers the actor's open event
/// ids, reads the actor's own registration per event, and reads each published event for its title,
/// type, venue and start. Only a Confirmed registration on a published event yields a pass, and the
/// QR token is the rule-10 token from <see cref="DemoStore"/>. Nothing here writes SQL, a stored
/// file or the Activity Log.
/// </summary>
public sealed class DemoPassService : IPassService
{
    // Event accents from the design register; the accent is deterministic from the event id and
    // visual only (no business meaning).
    private static readonly string[] Accents =
    {
        "pass-accent-moss",
        "pass-accent-indigo",
        "pass-accent-teal",
        "pass-accent-violet",
        "pass-accent-crimson",
        "pass-accent-orange",
        "pass-accent-slate"
    };

    private readonly IRegistrationRepository _registrations;
    private readonly IEventRepository _events;
    private readonly IMemberRepository _members;
    private readonly ICurrentActor _actor;
    private readonly DemoStore _store;
    private readonly TimeProvider _timeProvider;

    public DemoPassService(
        IRegistrationRepository registrations,
        IEventRepository events,
        IMemberRepository members,
        ICurrentActor actor,
        DemoStore store,
        TimeProvider timeProvider)
    {
        _registrations = registrations;
        _events = events;
        _members = members;
        _actor = actor;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<PassesViewModel> GetMyPassesAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireAuthenticatedActor();

        var member = await _members.GetMemberRecordByUserIdAsync(userId, cancellationToken);
        if (member == null)
        {
            return PassesViewModel.Empty();
        }

        var eventIds = await _registrations.DiscoverOpenEventIdsForUserAsync(userId, cancellationToken);
        if (eventIds.Count == 0)
        {
            return PassesViewModel.Empty();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var active = new List<PassViewModel>();
        var past = new List<PassViewModel>();

        foreach (var eventId in eventIds)
        {
            // Ownership is resolved through members.user_id, so another member's row never matches.
            var registration = await _registrations.GetMemberRegistrationAsync(eventId, userId, cancellationToken);
            if (registration == null || registration.Status != RegistrationStatusCode.Confirmed)
            {
                continue;
            }

            // A cancelled or unpublished event returns null here and yields no card.
            var eventItem = await _events.GetPublishedEventAsync(eventId, cancellationToken);
            if (eventItem == null)
            {
                continue;
            }

            var pass = new PassViewModel
            {
                RegistrationId = registration.RegistrationId,
                EventId = eventItem.EventId,
                EventTitle = eventItem.Title,
                EventTypeName = eventItem.TypeName,
                VenueName = eventItem.VenueName,
                StartsAt = eventItem.StartsAt,
                HolderName = member.FullName,
                StudentNumber = member.StudentNumber,
                StatusLabel = RegistrationStatusDisplay.Label(RegistrationStatusCode.Confirmed, false),
                AccentClass = Accent(eventItem.EventId),
                QrToken = _store.GetPassToken(registration.RegistrationId),
                DownloadFileName = $"door-pass-{registration.RegistrationId}.png",
                HasPass = true
            };

            if (eventItem.StartsAt <= now)
            {
                past.Add(pass);
            }
            else
            {
                active.Add(pass);
            }
        }

        return new PassesViewModel
        {
            Active = active
                .OrderBy(pass => pass.StartsAt)
                .ThenBy(pass => pass.RegistrationId)
                .ToList(),
            Past = past
                .OrderByDescending(pass => pass.StartsAt)
                .ThenBy(pass => pass.RegistrationId)
                .ToList()
        };
    }

    private static string Accent(int eventId) =>
        Accents[((eventId % Accents.Length) + Accents.Length) % Accents.Length];

    private int RequireAuthenticatedActor()
    {
        if (!_actor.IsAuthenticated)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to view passes.");
        }

        int userId;
        try
        {
            userId = _actor.UserId;
        }
        catch (InvalidOperationException)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to view passes.");
        }

        if (userId <= 0)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to view passes.");
        }

        return userId;
    }
}
