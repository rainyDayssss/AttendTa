using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Overlays demo event state onto the three existing event reads after the real read has run
/// (register rule 7). Only the check-in open flag changes, and only for records a demo command
/// changed. Nothing else from the real read is altered. (Register M-04.)
/// </summary>
public interface IEventStateOverlay
{
    EventViewModel Apply(EventViewModel model);

    MemberEventListItemViewModel Apply(MemberEventListItemViewModel model);

    MemberEventDetailViewModel Apply(MemberEventDetailViewModel model);

    PagedResult<EventViewModel> Apply(PagedResult<EventViewModel> page);

    PagedResult<MemberEventListItemViewModel> Apply(PagedResult<MemberEventListItemViewModel> page);
}
