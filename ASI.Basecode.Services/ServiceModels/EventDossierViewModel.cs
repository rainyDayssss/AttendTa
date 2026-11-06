using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Officer event dossier: the real event header plus an empty roster slot that the dossier
/// surface ticket fills.
/// </summary>
public sealed class EventDossierViewModel
{
    public int EventId { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }
    public string TypeName { get; init; }
    public string VenueName { get; init; }
    public DateTime StartsAt { get; init; }
    public DateTime RegistrationDeadline { get; init; }
    public int ParticipantLimit { get; init; }
    public EventStatusCode EventStatusId { get; init; }
    public string StatusLabel { get; init; }
    public IReadOnlyList<EventDossierRosterRowViewModel> Roster { get; init; } = Array.Empty<EventDossierRosterRowViewModel>();

    public static EventDossierViewModel Empty() => new();

    /// <summary>Projects the real event header and leaves the roster empty.</summary>
    public static EventDossierViewModel FromEvent(EventViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new EventDossierViewModel
        {
            EventId = model.EventId,
            Title = model.Title,
            Description = model.Description,
            TypeName = model.TypeName,
            VenueName = model.VenueName,
            StartsAt = model.StartsAt,
            RegistrationDeadline = model.RegistrationDeadline,
            ParticipantLimit = model.ParticipantLimit,
            EventStatusId = model.EventStatusId,
            StatusLabel = model.StatusLabel
        };
    }
}

/// <summary>One registration row in the dossier roster.</summary>
public sealed class EventDossierRosterRowViewModel
{
    public int RegistrationId { get; init; }
    public int MemberId { get; init; }
    public string MemberName { get; init; }
    public string StudentNumber { get; init; }
    public RegistrationStatusCode Status { get; init; }
    public string StatusLabel { get; init; }
    public string StatusBadgeClass { get; init; }
}
