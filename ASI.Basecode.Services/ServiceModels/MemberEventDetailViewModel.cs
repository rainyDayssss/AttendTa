using System;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Participant-facing published or completed event detail and its signup state.</summary>
public sealed class MemberEventDetailViewModel
{
    public int EventId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string TypeName { get; set; }
    public string VenueName { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int ParticipantLimit { get; set; }
    public int ConfirmedCount { get; set; }
    public bool HasCover { get; set; }

    /// <summary>The event lifecycle status the read reports.</summary>
    public EventStatusCode EventStatusId { get; set; }

    /// <summary>The visible lifecycle label for <see cref="EventStatusId"/>.</summary>
    public string StatusLabel => EventStatusDisplay.Label(EventStatusId);

    /// <summary>True when the event's check-in window is open.</summary>
    public bool IsCheckInOpen { get; set; }

    /// <summary>True when the canonical Signup open fact holds at the time the detail was read.</summary>
    public bool IsSignupOpen { get; set; }

    /// <summary>True when confirmed seats have reached the participant limit. Signup stays open.</summary>
    public bool IsFull { get; set; }

    /// <summary>
    /// True when the canonical Event Start Cutoff fact held at the time the detail was read.
    /// The view never reads the wall clock; this flag is the only source for the derived badge.
    /// </summary>
    public bool IsStartCutoffPassed { get; set; }

    /// <summary>True when the actor already has a registration of any status for this event.</summary>
    public bool HasRegistration { get; set; }

    /// <summary>The actor's own registration id, or <c>null</c> when they have none for this event.</summary>
    public int? RegistrationId { get; set; }

    /// <summary>The actor's own registration status, or <c>null</c> when they have none for this event.</summary>
    public RegistrationStatusCode? RegistrationStatus { get; set; }

    /// <summary>
    /// True when the server would accept the actor's cancellation right now: an open registration
    /// on a published event strictly before the start with no valid attendance.
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>True when the actor is an eligible participant and signup is still open.</summary>
    public bool CanRegister { get; set; }
}
