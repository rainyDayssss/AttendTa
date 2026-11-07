using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered when <c>DemoData:Enabled</c> is off so the controllers keep the same shape and the
/// switch-off path is untouched (register rule 2). (Register M-14b, M-15a, M-15b.)
/// </summary>
public sealed class NoOpAccessStateOverlay : IAccessStateOverlay
{
    public PagedResult<AccountSummaryViewModel> Apply(PagedResult<AccountSummaryViewModel> page) => page;

    public PagedResult<MemberViewModel> Apply(PagedResult<MemberViewModel> page) => page;

    public AccountAccessViewModel Apply(AccountAccessViewModel model) => model;

    public MembershipAccessViewModel Apply(MembershipAccessViewModel model) => model;
}
