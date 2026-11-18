using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.Models;

/// <summary>
/// The one mapping from registration approval outcomes to the shared result panel, so the review
/// queue and the event dossier report a batch identically. Confirmed and Waitlisted are separate
/// words: a successful approval is not a promise of a seat.
/// </summary>
public static class RegistrationBulkResult
{
    public static BulkResultViewModel From(IReadOnlyList<RegistrationApprovalOutcome> outcomes) =>
        new(
            "Approval results",
            (outcomes ?? []).Select(outcome => new BulkResultItem(
                Label(outcome),
                outcome.Result != RegistrationApprovalResult.Refused,
                outcome.Message,
                Status(outcome.Result),
                Tone(outcome.Result))).ToList(),
            "Approved",
            "Not approved");

    private static string Label(RegistrationApprovalOutcome outcome) =>
        string.IsNullOrWhiteSpace(outcome.StudentNumber)
            ? outcome.MemberName
            : $"{outcome.MemberName} ({outcome.StudentNumber})";

    private static string Status(RegistrationApprovalResult result) => result switch
    {
        RegistrationApprovalResult.Confirmed => "Confirmed",
        RegistrationApprovalResult.Waitlisted => "Waitlisted",
        _ => "Not approved"
    };

    private static string Tone(RegistrationApprovalResult result) => result switch
    {
        RegistrationApprovalResult.Confirmed => "badge-approved",
        RegistrationApprovalResult.Waitlisted => "badge-pending",
        _ => "badge-rejected"
    };
}
