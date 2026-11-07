using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// Real per-event participation report read. The selector and event header come from the event
/// repository and the summary comes from the canonical participation facts; the detail rows,
/// breakdowns and no-shows stay with their open demo rows until 7.2 and 7.3 replace them.
/// </summary>
public sealed class ParticipationReportService : IParticipationReportService
{
    private readonly IEventRepository _events;
    private readonly IParticipationReportRepository _reports;

    public ParticipationReportService(IEventRepository events, IParticipationReportRepository reports)
    {
        _events = events;
        _reports = reports;
    }

    public async Task<ParticipationReportViewModel> GetReportAsync(int? eventId, int? page, CancellationToken cancellationToken = default)
    {
        var totalEvents = await _events.CountAsync(cancellationToken);
        var events = totalEvents == 0
            ? Array.Empty<EventListItem>()
            : await _events.GetPageAsync(0, totalEvents, cancellationToken);

        var eventItem = eventId is int selectedId
            ? await _events.GetEventAsync(selectedId, cancellationToken)
            : null;

        var selector = new EventSelectorViewModel
        {
            SelectedEventId = eventItem?.EventId,
            Options = events.Select(row => new EventSelectorOption(row.EventId, row.Title)).ToList()
        };

        if (eventItem == null)
        {
            return new ParticipationReportViewModel { Selector = selector };
        }

        var facts = await _reports.GetSummaryFactsAsync(eventItem.EventId, cancellationToken);
        var status = eventItem.EventStatusId;

        return new ParticipationReportViewModel
        {
            Selector = selector,
            EventId = eventItem.EventId,
            EventTitle = eventItem.Title,
            StartsAt = eventItem.StartsAt,
            EventStatusId = status,
            EventStatusLabel = EventStatusDisplay.Label(status),
            EventStatusBadgeClass = EventStatusDisplay.BadgeClass(status),
            IsFinal = status == EventStatusCode.Completed,
            PendingCount = facts.PendingCount,
            ConfirmedCount = facts.ConfirmedCount,
            WaitlistedCount = facts.WaitlistedCount,
            CancelledCount = facts.CancelledCount,
            ValidAttendeeCount = facts.ValidAttendeeCount,
            AttendanceRate = facts.AttendanceRate,
            NoShowCount = 0,
            Rows = Array.Empty<ParticipationReportRowViewModel>(),
            CourseBreakdown = Array.Empty<ParticipationBreakdownItemViewModel>(),
            YearBreakdown = Array.Empty<ParticipationBreakdownItemViewModel>(),
            PageNumber = 1,
            TotalCount = 0
        };
    }
}
