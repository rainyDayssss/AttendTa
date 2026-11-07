using System.Linq;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The demo access overlay (register M-14b, M-15a and M-15b). Applied after the real read in
/// the controller, never as a repository decorator, because demo state is not a SQL row (register
/// rule 7). Only the account and membership status change.
/// </summary>
public sealed class DemoAccessStateOverlay : IAccessStateOverlay
{
    private readonly DemoStore _store;

    public DemoAccessStateOverlay(DemoStore store)
    {
        _store = store;
    }

    public AccountAccessViewModel Apply(AccountAccessViewModel model)
    {
        if (model == null)
        {
            return null;
        }

        if (_store.TryGetAccountStatus(model.UserId, out var status))
        {
            model.IsActive = status.IsActive;
        }

        return model;
    }

    public MembershipAccessViewModel Apply(MembershipAccessViewModel model)
    {
        if (model == null)
        {
            return null;
        }

        if (_store.TryGetMembershipStatus(model.MemberId, out var status))
        {
            model.IsActive = status.Status == MembershipStatusCode.Active;
        }

        return model;
    }

    public PagedResult<AccountSummaryViewModel> Apply(PagedResult<AccountSummaryViewModel> page) =>
        page == null
            ? null
            : new PagedResult<AccountSummaryViewModel>(page.Items.Select(ApplyAccount).ToList(), page.PageNumber, page.TotalCount);

    public PagedResult<MemberViewModel> Apply(PagedResult<MemberViewModel> page) =>
        page == null
            ? null
            : new PagedResult<MemberViewModel>(page.Items.Select(ApplyMember).ToList(), page.PageNumber, page.TotalCount);

    private AccountSummaryViewModel ApplyAccount(AccountSummaryViewModel model)
    {
        if (model == null)
        {
            return null;
        }

        if (_store.TryGetAccountStatus(model.AccountId, out var status))
        {
            model.IsActive = status.IsActive;
        }

        return model;
    }

    private MemberViewModel ApplyMember(MemberViewModel model)
    {
        if (model == null)
        {
            return null;
        }

        if (_store.TryGetMembershipStatus(model.MemberId, out var status))
        {
            model.Status = status.Status == MembershipStatusCode.Active ? "active" : "inactive";
        }

        return model;
    }
}
