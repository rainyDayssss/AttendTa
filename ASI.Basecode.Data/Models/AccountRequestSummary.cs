namespace ASI.Basecode.Data.Models;

/// <summary>
/// Display-only identity for an account request that a bulk approval is about to decide.
/// Deliberately excludes the password hash and every other decision field: the result panel
/// names people, it never trusts a posted label.
/// </summary>
public sealed class AccountRequestSummary
{
    public int RequestId { get; set; }
    public string FullName { get; set; }
    public string StudentNumber { get; set; }
}
