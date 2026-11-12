using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// Real per-event participation report read. The selector and event header come from the event
/// repository; the summary, no-shows, attendee distributions and the paged detail rows all come from
/// one materialized snapshot of the event's registrations, so every panel agrees even while
/// attendance changes. Summary values cover the full dataset; only the detail rows move with the
/// requested page.
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

        // One snapshot read: every registration of the event with its member, signup snapshots and
        // attendance. The summary, no-shows, distribution and the page slice are all computed from
        // these rows, so the panels cannot disagree while attendance changes.
        var snapshot = await _reports.GetSnapshotAsync(eventItem.EventId, cancellationToken);
        var status = eventItem.EventStatusId;
        var isFinal = status == EventStatusCode.Completed;

        var confirmedCount = snapshot.Count(row => ParticipationFacts.HoldsSeat(row.Status));
        var attendees = snapshot.Where(IsValidAttendee).ToList();

        // The count is the pager's total, so it covers the full dataset and never depends on the page.
        var pageRequest = new PageRequest(page);
        var pageNumber = pageRequest.NormalizePageNumber(snapshot.Count);
        var rows = snapshot
            .Skip(pageRequest.GetOffset(pageNumber))
            .Take(PageRequest.FixedPageSize)
            .ToList();

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
            PendingCount = snapshot.Count(row => row.Status == RegistrationStatusCode.Pending),
            ConfirmedCount = confirmedCount,
            WaitlistedCount = snapshot.Count(row => row.Status == RegistrationStatusCode.Waitlisted),
            CancelledCount = snapshot.Count(row => row.Status == RegistrationStatusCode.Cancelled),
            ValidAttendeeCount = attendees.Count,
            AttendanceRate = confirmedCount == 0 ? null : (decimal)attendees.Count / confirmedCount,
            // A no-show is a Confirmed seat with no valid attendance, and only a completed event has
            // a final answer.
            NoShowCount = isFinal ? confirmedCount - attendees.Count : 0,
            Rows = rows.Select(row => ToRow(row, isFinal)).ToList(),
            CourseBreakdown = CourseBreakdown(attendees),
            YearBreakdown = YearBreakdown(attendees),
            PageNumber = pageNumber,
            TotalCount = snapshot.Count
        };
    }

    /// <summary>
    /// A valid attendee holds a Confirmed seat and has a non-invalidated attendance row. Both facts
    /// come from the canonical <see cref="ParticipationFacts"/> predicates.
    /// </summary>
    private static bool IsValidAttendee(ParticipationReportDetailRow row) =>
        ParticipationFacts.HoldsSeat(row.Status)
        && row.CheckedInAt is not null
        && ParticipationFacts.IsValidAttendance(row.InvalidatedAt);

    private static ParticipationReportRowViewModel ToRow(ParticipationReportDetailRow row, bool isFinal)
    {
        var isConfirmed = ParticipationFacts.HoldsSeat(row.Status);
        var hasValidAttendance = row.CheckedInAt is not null && ParticipationFacts.IsValidAttendance(row.InvalidatedAt);
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
        return !ParticipationFacts.IsValidAttendance(row.InvalidatedAt)
            ? ("Invalidated", "badge-inactive")
            : ("Checked in", "badge-open");
    }

    private static IReadOnlyList<ParticipationBreakdownItemViewModel> CourseBreakdown(IReadOnlyList<ParticipationReportDetailRow> attendees)
    {
        var total = attendees.Count;

        return attendees
            .GroupBy(
                row => string.IsNullOrWhiteSpace(row.Course) ? "Unspecified" : row.Course.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Label = group.Key, Count = group.Count() })
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

    private static IReadOnlyList<ParticipationBreakdownItemViewModel> YearBreakdown(IReadOnlyList<ParticipationReportDetailRow> attendees)
    {
        var total = attendees.Count;

        return attendees
            .GroupBy(row => row.YearLevel)
            .OrderBy(group => group.Key == 0 ? int.MaxValue : group.Key)
            .Select(group =>
            {
                var count = group.Count();
                return new ParticipationBreakdownItemViewModel
                {
                    // A year level of 0 means the signup did not record one; the label says so rather
                    // than printing "Year 0".
                    Label = group.Key == 0 ? "Unspecified" : $"Year {group.Key}",
                    Count = count,
                    Percent = Share(count, total)
                };
            })
            .ToList();
    }

    private static int Share(int count, int total) =>
        total == 0 ? 0 : (int)Math.Round(count * 100m / total, MidpointRounding.AwayFromZero);
}
