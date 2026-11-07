using System;
using System.Collections.Generic;
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
/// repository; the summary, no-shows and attendee distributions come from the canonical
/// participation facts. The paged detail rows stay with their open demo row until 7.3 replaces them.
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
        var noShowCount = await _reports.GetNoShowCountAsync(eventItem.EventId, cancellationToken);
        var distribution = await _reports.GetAttendeeDistributionAsync(eventItem.EventId, cancellationToken);
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
            NoShowCount = noShowCount,
            Rows = Array.Empty<ParticipationReportRowViewModel>(),
            CourseBreakdown = CourseBreakdown(distribution.Courses),
            YearBreakdown = YearBreakdown(distribution.Years),
            PageNumber = 1,
            TotalCount = 0
        };
    }

    private static IReadOnlyList<ParticipationBreakdownItemViewModel> CourseBreakdown(IReadOnlyList<CourseDistributionRow> rows)
    {
        var total = rows.Sum(row => row.Count);

        return rows
            .GroupBy(
                row => string.IsNullOrWhiteSpace(row.Course) ? "Unspecified" : row.Course.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Label = group.Key, Count = group.Sum(row => row.Count) })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ParticipationBreakdownItemViewModel
            {
                Label = item.Label,
                Count = item.Count,
                Percent = Share(item.Count, total)
            })
            .ToList();
    }

    private static IReadOnlyList<ParticipationBreakdownItemViewModel> YearBreakdown(IReadOnlyList<YearDistributionRow> rows)
    {
        var total = rows.Sum(row => row.Count);

        return rows
            .GroupBy(row => row.YearLevel)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var count = group.Sum(row => row.Count);
                return new ParticipationBreakdownItemViewModel
                {
                    Label = group.Key > 0 ? $"Year {group.Key}" : "Unspecified",
                    Count = count,
                    Percent = Share(count, total)
                };
            })
            .ToList();
    }

    private static int Share(int count, int total) =>
        total == 0 ? 0 : (int)Math.Round(count * 100m / total, MidpointRounding.AwayFromZero);
}
