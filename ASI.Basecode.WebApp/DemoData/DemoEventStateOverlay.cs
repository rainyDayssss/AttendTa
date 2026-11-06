using System.Linq;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// The demo overlay (register M-04 and M-09). Applied after the real read in the controller, never
/// as a repository decorator, because the demo-completed event is still a real Published row and
/// only its label and check-in flag change (register rule 7).
/// </summary>
public sealed class DemoEventStateOverlay : IEventStateOverlay
{
    private readonly DemoStore _store;

    public DemoEventStateOverlay(DemoStore store)
    {
        _store = store;
    }

    public EventViewModel Apply(EventViewModel model)
    {
        if (model == null)
        {
            return null;
        }

        if (_store.IsCheckInOpen(model.EventId))
        {
            model.IsCheckInOpen = true;
        }

        if (_store.IsCompleted(model.EventId))
        {
            model.EventStatusId = EventStatusCode.Completed;
        }

        return model;
    }

    public MemberEventListItemViewModel Apply(MemberEventListItemViewModel model)
    {
        if (model == null)
        {
            return null;
        }

        if (_store.IsCheckInOpen(model.EventId))
        {
            model.IsCheckInOpen = true;
        }

        if (_store.IsCompleted(model.EventId))
        {
            model.EventStatusId = EventStatusCode.Completed;
        }

        return model;
    }

    public MemberEventDetailViewModel Apply(MemberEventDetailViewModel model)
    {
        if (model == null)
        {
            return null;
        }

        if (_store.IsCheckInOpen(model.EventId))
        {
            model.IsCheckInOpen = true;
        }

        if (_store.IsCompleted(model.EventId))
        {
            model.EventStatusId = EventStatusCode.Completed;
        }

        return model;
    }

    public PagedResult<EventViewModel> Apply(PagedResult<EventViewModel> page) =>
        page == null
            ? null
            : new PagedResult<EventViewModel>(page.Items.Select(Apply).ToList(), page.PageNumber, page.TotalCount);

    public PagedResult<MemberEventListItemViewModel> Apply(PagedResult<MemberEventListItemViewModel> page) =>
        page == null
            ? null
            : new PagedResult<MemberEventListItemViewModel>(page.Items.Select(Apply).ToList(), page.PageNumber, page.TotalCount);
}
