namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Model for the shared check-in window control. 3.16 (event dossier) and 4.11 (door scanner)
/// reuse the same partial, so the control carries only the event, its current window state and
/// whether the current actor may change it.
/// </summary>
public sealed class CheckInControlViewModel
{
    public int EventId { get; set; }

    /// <summary>True when the check-in window is open for the event.</summary>
    public bool IsCheckInOpen { get; set; }

    /// <summary>True when the event is in a state that accepts an open or close command.</summary>
    public bool CanChange { get; set; }
}
