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
/// provider then adds the still-open demo detail rows. Attendance is the real
/// <see cref="IAttendanceRepository.GetForEventAsync"/> read, with the store's seeded check-ins
/// filling only the registrations that have no real row, so a real invalidation always wins and the
/// seeded demo event stays populated. The event status comes from SQL; the store's completed
/// fallback is only a test fixture. It never writes.
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
    private readonly IAttendanceRepository _attendance;
    private readonly DemoStore _store;

    public DemoParticipationReportService(
        ParticipationReportService real,
        IRegistrationRepository registrations,
        IAttendanceRepository attendance,
        DemoStore store)
    {
        _real = real;
        _registrations = registrations;
        _attendance = attendance;
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

        // The store fills only registrations without a real row; a real row always wins (rule 12).
        var attendance = new Dictionary<int, AttendanceCell>();
        foreach (var row in registrations)
        {
            if (_store.TryGetAttendance(row.RegistrationId, out var record))
            {
                attendance[row.RegistrationId] = new AttendanceCell(record.RecordedAt, record.IsValid);
            }
        }

        foreach (var row in await _attendance.GetForEventAsync(selectedEventId, cancellationToken))
        {
            attendance[row.RegistrationId] = new AttendanceCell(row.RecordedAt, row.IsValid);
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

        // The merged attendance map drives the valid count, rate, no-shows and breakdowns whenever
        // it holds a row for the event; otherwise the real SQL facts stand unchanged.
        var hasAttendance = attendance.Count > 0;
        var validAttendeeCount = hasAttendance ? validAttendees.Count : report.ValidAttendeeCount;
        var attendanceRate = hasAttendance
            ? report.ConfirmedCount == 0 ? null : (decimal)validAttendees.Count / report.ConfirmedCount
            : report.AttendanceRate;
        // No-shows are a completed-event fact; a cancelled event shows no figure at all.
        var noShowCount = hasAttendance
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
            CourseBreakdown = hasAttendance ? CourseBreakdown(validAttendees) : report.CourseBreakdown,
            YearBreakdown = hasAttendance ? YearBreakdown(validAttendees) : report.YearBreakdown,
            PageNumber = normalizedPage,
            TotalCount = ordered.Count
        };
    }

    private static ParticipationReportRowViewModel ToRow(
        RegistrationReviewRow row,
        IReadOnlyDictionary<int, AttendanceCell> attendance,
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

    private static (string Label, string BadgeClass) Attendance(AttendanceCell record, bool isConfirmed, bool isNoShow)
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

    /// <summary>The attendance facts the report needs, from a real row or a seeded store row.</summary>
    private sealed record AttendanceCell(DateTime RecordedAt, bool IsValid);
}
