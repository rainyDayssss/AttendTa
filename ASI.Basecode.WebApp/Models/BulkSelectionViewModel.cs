using System.Collections.Generic;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.WebApp.Models;

/// <summary>
/// One selection surface for a list: the bar, the confirmation dialog and the POST target.
/// Shared by every bulk approval so no surface invents its own selection contract.
/// </summary>
public sealed class BulkSelectionViewModel
{
    /// <summary>Label on the bar's action button.</summary>
    public string ActionLabel { get; init; } = "Approve selected";

    /// <summary>Id of the confirmation dialog this bar opens.</summary>
    public string DialogId { get; init; } = "bulk-action-dialog";

    /// <summary>Dialog heading.</summary>
    public string DialogTitle { get; init; } = "Approve selected";

    /// <summary>The consequence sentence shown in the dialog.</summary>
    public string Consequence { get; init; }

    /// <summary>Label on the dialog's submit button.</summary>
    public string SubmitLabel { get; init; } = "Approve selected";

    /// <summary>Label the submit button carries while the POST is in flight.</summary>
    public string SubmittingLabel { get; init; } = "Working…";

    /// <summary>
    /// Accessible name for the selected-record list inside the confirmation dialog. A surface keeps
    /// its own noun so a reader is never told they are approving another surface's records.
    /// </summary>
    public string SelectionListLabel { get; init; } = "Selected requests";

    /// <summary>The singular noun the dialog uses for one selected item in its bound sentence.</summary>
    public string RecordNoun { get; init; } = "record";

    /// <summary>Controller that owns the bulk POST.</summary>
    public string Controller { get; init; }

    /// <summary>Action that owns the bulk POST.</summary>
    public string Action { get; init; }

    /// <summary>Route values carried into the POST so the queue returns to its current view.</summary>
    public IDictionary<string, string> RouteValues { get; init; } = new Dictionary<string, string>();

    /// <summary>The enforced batch bound, shown to the reader.</summary>
    public int MaxBatchSize { get; init; } = BulkApproval.MaxBatchSize;
}
