using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Per-event participation report: shared selector, event header, summary values, registration
/// status counts, participant detail rows and the course/year breakdowns. It implements
/// <see cref="IPagedResult"/> so the shared pager renders it directly; the summary and breakdowns
/// cover the full dataset and never change with the detail page.
/// </summary>
public sealed class ParticipationReportViewModel : IPagedResult
{
    public EventSelectorViewModel Selector { get; init; } = EventSelectorViewModel.Empty();
    public int? EventId { get; init; }
    public string EventTitle { get; init; }
    public DateTime? StartsAt { get; init; }

    /// <summary>The event's lifecycle status from SQL; the report never overrides it.</summary>
    public EventStatusCode? EventStatusId { get; init; }

    public string EventStatusLabel { get; init; }
    public string EventStatusBadgeClass { get; init; }

    /// <summary>
    /// True only for a completed, non-cancelled event. Rate and no-shows are final in that state;
    /// the view marks them as not final everywhere else.
    /// </summary>
    public bool IsFinal { get; init; }

    public int PendingCount { get; init; }
    public int ConfirmedCount { get; init; }
    public int WaitlistedCount { get; init; }
    public int CancelledCount { get; init; }

    public int ValidAttendeeCount { get; init; }

    /// <summary>Null when the confirmed denominator is zero; the view renders N/A instead of 0%.</summary>
    public decimal? AttendanceRate { get; init; }

    public int NoShowCount { get; init; }
    public IReadOnlyList<ParticipationReportRowViewModel> Rows { get; init; } = Array.Empty<ParticipationReportRowViewModel>();
    public IReadOnlyList<ParticipationBreakdownItemViewModel> CourseBreakdown { get; init; } = Array.Empty<ParticipationBreakdownItemViewModel>();
    public IReadOnlyList<ParticipationBreakdownItemViewModel> YearBreakdown { get; init; } = Array.Empty<ParticipationBreakdownItemViewModel>();

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
    public int TotalCount { get; init; }
    public int TotalPages => TotalCount == 0 ? 0 : (int)(((long)TotalCount + PageSize - 1) / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int LastItemNumber => TotalCount == 0 ? 0 : (int)Math.Min((long)PageNumber * PageSize, TotalCount);

    public static ParticipationReportViewModel Empty() => new();
}

/// <summary>One participant row in the report detail table.</summary>
public sealed class ParticipationReportRowViewModel
{
    public int RegistrationId { get; init; }
    public string MemberName { get; init; }
    public string StudentNumber { get; init; }
    public string Course { get; init; }
    public int YearLevel { get; init; }
    public RegistrationStatusCode Status { get; init; }
    public string StatusLabel { get; init; }
    public string StatusBadgeClass { get; init; }
    public bool Attended { get; init; }
    public bool IsNoShow { get; init; }

    /// <summary>The recorded check-in time, including an invalidated one; null when never checked in.</summary>
    public DateTime? CheckedInAt { get; init; }

    public string AttendanceLabel { get; init; }

    /// <summary>Null for a registration that cannot carry attendance; the view renders plain text.</summary>
    public string AttendanceBadgeClass { get; init; }
}

/// <summary>One course or year-level breakdown entry.</summary>
public sealed class ParticipationBreakdownItemViewModel
{
    public string Label { get; init; }
    public int Count { get; init; }

    /// <summary>Whole-percent share of the valid attendees, used for the breakdown bar.</summary>
    public int Percent { get; init; }
}
