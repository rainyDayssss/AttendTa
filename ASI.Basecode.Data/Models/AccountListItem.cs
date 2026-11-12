namespace ASI.Basecode.Data.Models;

/// <summary>Safe account fields for the Administrator accounts list. Deliberately excludes password hashes.</summary>
public sealed class AccountListItem
{
    public int AccountId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string RoleCode { get; set; }
    public string RoleLabel { get; set; }
    public bool IsActive { get; set; }
}
