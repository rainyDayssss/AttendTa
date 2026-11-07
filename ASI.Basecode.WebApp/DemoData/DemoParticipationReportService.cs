using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo overlay for the per-event participation report (register M-13c). The real
/// <see cref="ParticipationReportService"/> answers the summary, no-shows and breakdowns first; this
/// provider then adds the still-open demo detail rows and keeps the attendance-derived valid count,
/// rate, no-shows and breakdowns while M-05 and M-07 remain open and the store holds attendance for
/// the event. The event status comes from SQL; the store's completed fallback is only a test
/// fixture. It never writes.
/// </summary>
public sealed class DemoParticipationReportService : IParticipationReportService
{
    private const int AllRows = int.MaxValue;

    private static readonly RegistrationStatusCode[] Statuses =
    {
        RegistrationStatusCode.Pending,
        RegistrationStatusCode.Confirmed,
        RegistrationStatusCode.Waitlisted,
        RegistrationStatusCode.Cancelled
    };

    private readonly ParticipationReportService _real;
    private readonly IRegistrationRepository _registrations;
    private readonly DemoStore _store;

    public DemoParticipationReportService(
        ParticipationReportService real,
        IRegistrationRepository registrations,
        DemoStore store)
    {
        _real = real;
        _registrations = registrations;
        _store = store;
    }

    public async Task<ParticipationReportViewModel> GetReportAsync(int? eventId, int? page, CancellationToken cancellationToken = default)
    {
        var report = await _real.GetReportAsync(eventId, page, cancellationToken);
        if (report.EventId is not int selectedEventId)
        {
            return report;
        }

        // One existing read per status so pending, waitlisted and cancelled rows are never dropped.
        var registrations = new List<RegistrationReviewRow>();
        foreach (var registrationStatus in Statuses)
        {
            registrations.AddRange(await _registrations.GetReviewQueueAsync(
                registrationStatus, selectedEventId, 0, AllRows, cancellationToken));
        }

        var attendance = new Dictionary<int, DemoAttendance>();
        foreach (var row in registrations)
        {
            if (_store.TryGetAttendance(row.RegistrationId, out var record))
            {
                attendance[row.RegistrationId] = record;
            }
        }

        var confirmed = registrations.Where(row => row.Status == RegistrationStatusCode.Confirmed).ToList();
        var validAttendees = confirmed
            .Where(row => attendance.TryGetValue(row.RegistrationId, out var record) && record.IsValid)
            .ToList();

        // A demo-completed event is still Published in SQL; only the effective status changes.
        var status = _store.IsCompleted(selectedEventId)
            ? EventStatusCode.Completed
            : report.EventStatusId!.Value;
        var isFinal = status == EventStatusCode.Completed;

        var ordered = registrations
            .OrderBy(row => row.MemberName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.RegistrationId)
            .ToList();
        var pageRequest = new PageRequest(page);
        var normalizedPage = pageRequest.NormalizePageNumber(ordered.Count);
        var pageRows = ordered
            .Skip(pageRequest.GetOffset(normalizedPage))
            .Take(PageRequest.FixedPageSize)
            .ToList();

        // Rule 12: while M-05 and M-07 are open, the demo store's attendance drives the valid count,
        // rate, no-shows and breakdowns whenever it holds attendance for the event; otherwise the
        // real SQL facts stand.
        var hasDemoAttendance = _store.GetAttendanceForEvent(selectedEventId).Count > 0;
        var validAttendeeCount = hasDemoAttendance ? validAttendees.Count : report.ValidAttendeeCount;
        var attendanceRate = hasDemoAttendance
            ? report.ConfirmedCount == 0 ? null : (decimal)validAttendees.Count / report.ConfirmedCount
            : report.AttendanceRate;
        // No-shows are a completed-event fact; a cancelled event shows no figure at all.
        var noShowCount = hasDemoAttendance
            ? isFinal ? report.ConfirmedCount - validAttendeeCount : 0
            : report.NoShowCount;

        return new ParticipationReportViewModel
        {
            Selector = report.Selector,
            EventId = report.EventId,
            EventTitle = report.EventTitle,
            StartsAt = report.StartsAt,
            EventStatusId = status,
            EventStatusLabel = EventStatusDisplay.Label(status),
            EventStatusBadgeClass = EventStatusDisplay.BadgeClass(status),
            IsFinal = isFinal,
            PendingCount = report.PendingCount,
            ConfirmedCount = report.ConfirmedCount,
            WaitlistedCount = report.WaitlistedCount,
            CancelledCount = report.CancelledCount,
            ValidAttendeeCount = validAttendeeCount,
            AttendanceRate = attendanceRate,
            NoShowCount = noShowCount,
            Rows = pageRows.Select(row => ToRow(row, attendance, isFinal)).ToList(),
            CourseBreakdown = hasDemoAttendance ? CourseBreakdown(validAttendees) : report.CourseBreakdown,
            YearBreakdown = hasDemoAttendance ? YearBreakdown(validAttendees) : report.YearBreakdown,
            PageNumber = normalizedPage,
            TotalCount = ordered.Count
        };
    }

    private static ParticipationReportRowViewModel ToRow(
        RegistrationReviewRow row,
        IReadOnlyDictionary<int, DemoAttendance> attendance,
        bool isFinal)
    {
        attendance.TryGetValue(row.RegistrationId, out var record);
        var isConfirmed = row.Status == RegistrationStatusCode.Confirmed;
        var hasValidAttendance = record is { IsValid: true };
        var isNoShow = isFinal && isConfirmed && !hasValidAttendance;

        var (attendanceLabel, attendanceBadge) = Attendance(record, isConfirmed, isNoShow);

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
            CheckedInAt = record?.RecordedAt,
            AttendanceLabel = attendanceLabel,
            AttendanceBadgeClass = attendanceBadge
        };
    }

    private static (string Label, string BadgeClass) Attendance(DemoAttendance record, bool isConfirmed, bool isNoShow)
    {
        if (record is null)
        {
            if (!isConfirmed)
            {
                // Only a confirmed registration is expected at the door; other statuses carry no result.
                return ("—", null);
            }

            return isNoShow ? ("No-show", "badge-inactive") : ("Not checked in", "badge-closed");
        }

        // An invalidated check-in is still shown as Invalidated; it is not a valid attendee.
        return record.IsValid
            ? ("Checked in", "badge-open")
            : ("Invalidated", "badge-inactive");
    }

    private static IReadOnlyList<ParticipationBreakdownItemViewModel> CourseBreakdown(IReadOnlyList<RegistrationReviewRow> attendees) =>
        attendees
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
                Percent = Share(item.Count, attendees.Count)
            })
            .ToList();

    private static IReadOnlyList<ParticipationBreakdownItemViewModel> YearBreakdown(IReadOnlyList<RegistrationReviewRow> attendees) =>
        attendees
            .GroupBy(row => row.YearLevel)
            .OrderBy(group => group.Key)
            .Select(group => new ParticipationBreakdownItemViewModel
            {
                Label = group.Key > 0 ? $"Year {group.Key}" : "Unspecified",
                Count = group.Count(),
                Percent = Share(group.Count(), attendees.Count)
            })
            .ToList();

    private static int Share(int count, int total) =>
        total == 0 ? 0 : (int)Math.Round(count * 100m / total, MidpointRounding.AwayFromZero);
}
