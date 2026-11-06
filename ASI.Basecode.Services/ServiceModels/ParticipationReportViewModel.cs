using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models.Lookups;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Per-event participation report: shared selector, summary values, participant detail rows and
/// the course/year breakdowns.
/// </summary>
public sealed class ParticipationReportViewModel
{
    public EventSelectorViewModel Selector { get; init; } = EventSelectorViewModel.Empty();
    public int? EventId { get; init; }
    public string EventTitle { get; init; }
    public DateTime? StartsAt { get; init; }
    public int ConfirmedCount { get; init; }
    public int ValidAttendeeCount { get; init; }

    /// <summary>Null when the confirmed denominator is zero; the view renders N/A instead of 0%.</summary>
    public decimal? AttendanceRate { get; init; }

    public int NoShowCount { get; init; }
    public IReadOnlyList<ParticipationReportRowViewModel> Rows { get; init; } = Array.Empty<ParticipationReportRowViewModel>();
    public IReadOnlyList<ParticipationBreakdownItemViewModel> CourseBreakdown { get; init; } = Array.Empty<ParticipationBreakdownItemViewModel>();
    public IReadOnlyList<ParticipationBreakdownItemViewModel> YearBreakdown { get; init; } = Array.Empty<ParticipationBreakdownItemViewModel>();

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
    public bool Attended { get; init; }
    public bool IsNoShow { get; init; }
}

/// <summary>One course or year-level breakdown entry.</summary>
public sealed class ParticipationBreakdownItemViewModel
{
    public string Label { get; init; }
    public int Count { get; init; }
}
