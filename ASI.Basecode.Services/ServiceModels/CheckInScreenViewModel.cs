using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Door scanner screen: shared event selector plus the recorded check-in results.</summary>
public sealed class CheckInScreenViewModel
{
    public EventSelectorViewModel Selector { get; init; } = EventSelectorViewModel.Empty();
    public int? EventId { get; init; }
    public string EventTitle { get; init; }
    public bool IsCheckInOpen { get; init; }
    public IReadOnlyList<CheckInResultViewModel> RecentResults { get; init; } = Array.Empty<CheckInResultViewModel>();

    public static CheckInScreenViewModel Empty() => new();
}

/// <summary>One recorded check-in outcome shown in the scanner log.</summary>
public sealed class CheckInResultViewModel
{
    public string Outcome { get; init; }
    public string MemberName { get; init; }
    public string StudentNumber { get; init; }
    public DateTime? RecordedAt { get; init; }
    public string Message { get; init; }
}
