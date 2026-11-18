namespace ASI.Basecode.Resources.Constants;

/// <summary>
/// One bound for every bulk approval surface. The service and the controller read these members
/// so the posted form, the enforced limit and the message a reader sees cannot drift apart.
/// </summary>
public static class BulkApproval
{
    /// <summary>The largest number of records one bulk approval may decide.</summary>
    public const int MaxBatchSize = 25;

    /// <summary>The notice shown when an account-request bulk approval carries no ids.</summary>
    public const string EmptySelectionMessage = "Select at least one account request to approve.";

    /// <summary>The notice shown when an account-request bulk approval carries more ids than the bound allows.</summary>
    public static string OverBoundMessage => $"You can approve up to {MaxBatchSize} account requests at a time.";

    /// <summary>
    /// The notice shown when a bulk approval surface that names its own record type carries no ids.
    /// The noun is singular ("review"), so the reader is never told to select another surface's records.
    /// </summary>
    public static string EmptySelectionMessageFor(string noun) => $"Select at least one {noun} to approve.";

    /// <summary>
    /// The notice shown when a bulk approval surface that names its own record type carries more ids
    /// than the bound allows. The bound comes from <see cref="MaxBatchSize"/>, so the wording cannot
    /// drift from the enforced limit.
    /// </summary>
    public static string OverBoundMessageFor(string noun) => $"You can approve up to {MaxBatchSize} {noun}s at a time.";

    /// <summary>
    /// The notice shown when a registration bulk approval carries no ids. Registration surfaces
    /// keep their own wording so a reader is never told to select an account request.
    /// </summary>
    public static string RegistrationEmptySelectionMessage => EmptySelectionMessageFor("registration");

    /// <summary>The notice shown when a registration bulk approval carries more ids than the bound allows.</summary>
    public static string RegistrationOverBoundMessage => OverBoundMessageFor("registration");
}
