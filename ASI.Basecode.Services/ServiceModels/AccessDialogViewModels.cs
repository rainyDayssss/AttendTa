namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Account access dialog contract for the Administrator accounts list. The dialog body is filled
/// by the access surface ticket; this ticket only places the empty row slot.
/// </summary>
public sealed class AccountAccessViewModel
{
    public int AccountId { get; init; }
    public string FullName { get; init; }
    public bool IsActive { get; init; }

    public static AccountAccessViewModel Empty(int accountId = 0) => new() { AccountId = accountId };

    public static AccountAccessViewModel For(AccountSummaryViewModel account) => new()
    {
        AccountId = account.AccountId,
        FullName = account.FullName,
        IsActive = account.IsActive
    };
}

/// <summary>
/// Membership access dialog contract for the Management members list. The dialog body is filled
/// by the access surface ticket; this ticket only places the empty row slot.
/// </summary>
public sealed class MembershipAccessViewModel
{
    public int MemberId { get; init; }
    public string FullName { get; init; }
    public string StudentNumber { get; init; }
    public bool IsActive { get; init; }

    public static MembershipAccessViewModel Empty(int memberId = 0) => new() { MemberId = memberId };

    public static MembershipAccessViewModel For(MemberViewModel member) => new()
    {
        MemberId = member.MemberId,
        FullName = member.FullName,
        StudentNumber = member.StudentNumber,
        IsActive = member.Status == "active"
    };
}
