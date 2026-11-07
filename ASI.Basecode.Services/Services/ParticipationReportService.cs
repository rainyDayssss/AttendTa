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
/// participation facts, and the paged detail rows come from the event's registrations joined to
/// their member and attendance. Summary values cover the full dataset; only the detail rows move
/// with the requested page.
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
        var isFinal = status == EventStatusCode.Completed;

        // The count is the pager's total, so it covers the full dataset and never depends on the page.
        var totalCount = await _reports.CountDetailRowsAsync(eventItem.EventId, cancellationToken);
        var pageRequest = new PageRequest(page);
        var pageNumber = pageRequest.NormalizePageNumber(totalCount);
        var rows = totalCount == 0
            ? Array.Empty<ParticipationReportDetailRow>()
            : await _reports.GetDetailPageAsync(
                eventItem.EventId,
                pageRequest.GetOffset(pageNumber),
                PageRequest.FixedPageSize,
                cancellationToken);

        return new ParticipationReportViewModel
        {
            Selector = selector,
            EventId = eventItem.EventId,
            EventTitle = eventItem.Title,
            StartsAt = eventItem.StartsAt,
            EventStatusId = status,
            EventStatusLabel = EventStatusDisplay.Label(status),
            EventStatusBadgeClass = EventStatusDisplay.BadgeClass(status),
            IsFinal = isFinal,
            PendingCount = facts.PendingCount,
            ConfirmedCount = facts.ConfirmedCount,
            WaitlistedCount = facts.WaitlistedCount,
            CancelledCount = facts.CancelledCount,
            ValidAttendeeCount = facts.ValidAttendeeCount,
            AttendanceRate = facts.AttendanceRate,
            NoShowCount = noShowCount,
            Rows = rows.Select(row => ToRow(row, isFinal)).ToList(),
            CourseBreakdown = CourseBreakdown(distribution.Courses),
            YearBreakdown = YearBreakdown(distribution.Years),
            PageNumber = pageNumber,
            TotalCount = totalCount
        };
    }

    private static ParticipationReportRowViewModel ToRow(ParticipationReportDetailRow row, bool isFinal)
    {
        var isConfirmed = row.Status == RegistrationStatusCode.Confirmed;
        var hasValidAttendance = row.CheckedInAt is not null && !row.IsAttendanceInvalidated;
        var isNoShow = isFinal && isConfirmed && !hasValidAttendance;
        var (attendanceLabel, attendanceBadge) = Attendance(row, isConfirmed, isNoShow);

        return new ParticipationReportRowViewModel
        {
            RegistrationId = row.RegistrationId,
            MemberName = row.MemberName,
            StudentNumber = row.StudentNumber,
            Course = row.Course,
            YearLevel = row.YearLevel,
            Status = row.Status,
            StatusLabel = RegistrationStatusDisplay.Label(row.Status, false),
            StatusBadgeClass = RegistrationStatusDisplay.BadgeClass(row.Status, false),
            Attended = hasValidAttendance,
            IsNoShow = isNoShow,
            CheckedInAt = row.CheckedInAt,
            AttendanceLabel = attendanceLabel,
            AttendanceBadgeClass = attendanceBadge
        };
    }

    private static (string Label, string BadgeClass) Attendance(
        ParticipationReportDetailRow row,
        bool isConfirmed,
        bool isNoShow)
    {
        if (row.CheckedInAt is null)
        {
            if (!isConfirmed)
            {
                // Only a confirmed registration is expected at the door; other statuses carry no result.
                return ("—", null);
            }

            return isNoShow ? ("No-show", "badge-inactive") : ("Not checked in", "badge-closed");
        }

        // An invalidated check-in is still shown as Invalidated; it is not a valid attendee.
        return row.IsAttendanceInvalidated
            ? ("Invalidated", "badge-inactive")
            : ("Checked in", "badge-open");
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
