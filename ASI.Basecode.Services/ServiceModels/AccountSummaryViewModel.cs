namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Display-only account data for the Administrator accounts list.</summary>
public sealed class AccountSummaryViewModel
{
    public int AccountId { get; init; }
    public string FullName { get; init; }
    public string Email { get; init; }
    public string RoleCode { get; init; }
    public string RoleLabel { get; init; }

    /// <summary>Settable so the demo overlay can show the state a command changed (register rule 7).</summary>
    public bool IsActive { get; set; }
}
