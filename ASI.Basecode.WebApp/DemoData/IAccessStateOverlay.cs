using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Overlays demo account and membership status onto the two existing list reads and the two access
/// dialog models after the real read has run (register rule 7). Only the account and membership
/// status change, and only for records a demo command changed. Nothing else from the real read is
/// altered. (Register M-14b, M-15a, M-15b.)
/// </summary>
public interface IAccessStateOverlay
{
    PagedResult<AccountSummaryViewModel> Apply(PagedResult<AccountSummaryViewModel> page);

    PagedResult<MemberViewModel> Apply(PagedResult<MemberViewModel> page);

    AccountAccessViewModel Apply(AccountAccessViewModel model);

    MembershipAccessViewModel Apply(MembershipAccessViewModel model);
}
