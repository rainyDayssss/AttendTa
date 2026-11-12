using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The member's door passes over the real owner-scoped registration query. Only a Confirmed
/// registration on a Published event with a stored 64-character token yields a pass, and only the
/// actor's own rows are ever read. The service authorizes and returns the token; the WebApp
/// renderer turns it into PNG bytes, so this layer never depends on an imaging library.
/// </summary>
public sealed class PassService(
    IRegistrationRepository registrations,
    ICurrentActor actor,
    TimeProvider clock) : IPassService
{
    /// <summary>The one refusal every unavailable pass request receives.</summary>
    public const string UnavailableMessage = "This pass is unavailable.";

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

    public async Task<PassesViewModel> GetMyPassesAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireAuthenticatedActor();

        var rows = await registrations.GetMyPassRowsAsync(userId, cancellationToken);
        if (rows.Count == 0)
        {
            return PassesViewModel.Empty();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var active = new List<PassViewModel>();
        var past = new List<PassViewModel>();

        foreach (var row in rows)
        {
            if (!IsCanonicalToken(row.QrToken))
            {
                continue;
            }

            var pass = new PassViewModel
            {
                RegistrationId = row.RegistrationId,
                EventId = row.EventId,
                EventTitle = row.EventTitle,
                EventTypeName = row.EventTypeName,
                VenueName = row.VenueName,
                StartsAt = row.StartsAt,
                HolderName = row.HolderName,
                StudentNumber = row.StudentNumber,
                StatusLabel = RegistrationStatusDisplay.Label(RegistrationStatusCode.Confirmed, false),
                AccentClass = Accent(row.EventId),
                HasPass = true
            };

            if (row.StartsAt <= now)
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

    public async Task<PassDownload> GetPassDownloadAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        var userId = RequireAuthenticatedActor();

        // Ownership is resolved inside SQL, so another member's id and an unknown id read the same.
        var owned = registrationId <= 0
            ? null
            : await registrations.GetOwnedPassAsync(registrationId, userId, cancellationToken);

        if (owned is null
            || !ParticipationFacts.HoldsSeat(owned.Status)
            || owned.EventStatus != EventStatusCode.Published
            || !IsCanonicalToken(owned.QrToken))
        {
            throw new DomainException(OutcomeCode.Forbidden, UnavailableMessage);
        }

        return new PassDownload(owned.QrToken, $"door-pass-{owned.RegistrationId}.png");
    }

    /// <summary>
    /// The canonical QR token is 32 random bytes as 64 lowercase hex characters. Anything else is
    /// treated as no token, so a malformed value never renders or downloads.
    /// </summary>
    private static bool IsCanonicalToken(string token) =>
        token is { Length: 64 } && token.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Accent(int eventId) =>
        Accents[((eventId % Accents.Length) + Accents.Length) % Accents.Length];

    private int RequireAuthenticatedActor()
    {
        if (!actor.IsAuthenticated)
        {
            throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to view passes.");
        }

        int userId;
        try
        {
            userId = actor.UserId;
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
