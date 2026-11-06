using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered when <c>DemoData:Enabled</c> is off so the controllers have the same shape and the
/// switch-off path is untouched (register rule 2). (Register M-04, M-09.)
/// </summary>
public sealed class NoOpEventStateOverlay : IEventStateOverlay
{
    public EventViewModel Apply(EventViewModel model) => model;

    public MemberEventListItemViewModel Apply(MemberEventListItemViewModel model) => model;

    public MemberEventDetailViewModel Apply(MemberEventDetailViewModel model) => model;

    public PagedResult<EventViewModel> Apply(PagedResult<EventViewModel> page) => page;

    public PagedResult<MemberEventListItemViewModel> Apply(PagedResult<MemberEventListItemViewModel> page) => page;
}
