using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// The single rule for the member-facing registration badge. The "Not confirmed before cutoff"
/// label is derived at read time from the canonical Event Start Cutoff fact; the stored
/// registration status is never changed to produce it.
/// </summary>
public static class RegistrationStatusDisplay
{
    public const string NotConfirmedBeforeCutoff = "Not confirmed before cutoff";

    /// <summary>
    /// True when the row is still unresolved (Pending or Waitlisted) after the event started.
    /// Confirmed and Cancelled rows keep their own label at every time.
    /// </summary>
    public static bool IsNotConfirmedBeforeCutoff(RegistrationStatusCode? status, bool startCutoffPassed) =>
        startCutoffPassed && status is RegistrationStatusCode.Pending or RegistrationStatusCode.Waitlisted;

    /// <summary>The badge text for the status, or <c>null</c> when the actor has no registration.</summary>
    public static string Label(RegistrationStatusCode? status, bool startCutoffPassed) =>
        IsNotConfirmedBeforeCutoff(status, startCutoffPassed)
            ? NotConfirmedBeforeCutoff
            : status switch
            {
                RegistrationStatusCode.Pending => "Pending review",
                RegistrationStatusCode.Confirmed => "Confirmed",
                RegistrationStatusCode.Waitlisted => "Waitlisted",
                RegistrationStatusCode.Cancelled => "Cancelled",
                _ => null
            };

    /// <summary>
    /// The compiled badge class for the same rule. The cutoff label uses the neutral
    /// <c>badge-closed</c> treatment so it never reads as success. Returns <c>null</c> when the
    /// actor has no registration, matching <see cref="Label"/>.
    /// </summary>
    public static string BadgeClass(RegistrationStatusCode? status, bool startCutoffPassed) =>
        status is null
            ? null
            : IsNotConfirmedBeforeCutoff(status, startCutoffPassed)
                ? "badge-closed"
                : status switch
                {
                    RegistrationStatusCode.Pending => "badge-full",
                    RegistrationStatusCode.Confirmed => "badge-open",
                    RegistrationStatusCode.Waitlisted => "badge-member",
                    RegistrationStatusCode.Cancelled => "badge-inactive",
                    _ => "badge-member"
                };
}
