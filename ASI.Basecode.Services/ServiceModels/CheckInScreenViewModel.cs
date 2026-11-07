using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// Door scanner screen: the shared event selector, the selected event's check-in state, the paged
/// manual lookup and the recorded check-in log.
/// </summary>
public sealed class CheckInScreenViewModel
{
    public EventSelectorViewModel Selector { get; init; } = EventSelectorViewModel.Empty();
    public int? EventId { get; init; }
    public bool HasEvent => EventId.HasValue;
    public string EventTitle { get; init; }
    public bool IsCheckInOpen { get; init; }
    public bool IsCompleted { get; init; }
    public bool CanChangeWindow { get; init; }

    /// <summary>Filled by the controller after the screen read when a lookup query is present.</summary>
    public CheckInLookupViewModel Lookup { get; set; } = CheckInLookupViewModel.Empty();

    public IReadOnlyList<CheckInResultViewModel> RecentResults { get; init; } = Array.Empty<CheckInResultViewModel>();

    public static CheckInScreenViewModel Empty() => new();
}

/// <summary>One recorded check-in shown in the scanner log.</summary>
public sealed class CheckInResultViewModel
{
    public int RegistrationId { get; init; }
    public string Outcome { get; init; }
    public string MemberName { get; init; }
    public string StudentNumber { get; init; }
    public DateTime RecordedAt { get; init; }
    public string Message { get; init; }
    public bool IsInvalidated { get; init; }
    public bool CanInvalidate { get; init; }
}

/// <summary>
/// The result of a scan, manual check-in or invalidation. <see cref="Outcome"/> is one of
/// <see cref="Success"/>, <see cref="Repeat"/>, <see cref="Invalid"/> or <see cref="Closed"/>; the
/// camera-unavailable outcome is rendered by the browser only.
/// </summary>
public sealed class CheckInOutcomeViewModel
{
    public const string Success = "success";
    public const string Repeat = "repeat";
    public const string Invalid = "invalid";
    public const string Closed = "closed";

    public string Outcome { get; init; }
    public string Message { get; init; }
    public int RegistrationId { get; init; }
    public string MemberName { get; init; }
    public string StudentNumber { get; init; }
    public DateTime? RecordedAt { get; init; }

    /// <summary>True when the outcome recorded or confirmed a check-in.</summary>
    public bool IsRecorded => Outcome is Success or Repeat;

    public static CheckInOutcomeViewModel InvalidResult(string message) => new()
    {
        Outcome = Invalid,
        Message = message
    };

    public static CheckInOutcomeViewModel ClosedResult(string message) => new()
    {
        Outcome = Closed,
        Message = message
    };
}

/// <summary>The paged confirmed-participant lookup, shaped for the shared result count and pager.</summary>
public sealed class CheckInLookupViewModel : IPagedResult
{
    public string Query { get; init; }
    public IReadOnlyList<CheckInLookupRowViewModel> Rows { get; init; } = Array.Empty<CheckInLookupRowViewModel>();
    public int PageNumber { get; init; } = 1;
    public int PageSize => PageRequest.FixedPageSize;
    public int TotalCount { get; init; }
    public int TotalPages => TotalCount == 0 ? 0 : (int)(((long)TotalCount + PageSize - 1) / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int LastItemNumber => TotalCount == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalCount);

    public bool HasQuery => !string.IsNullOrWhiteSpace(Query);

    public static CheckInLookupViewModel Empty() => new();
}

/// <summary>One confirmed participant in the manual lookup.</summary>
public sealed class CheckInLookupRowViewModel
{
    public int RegistrationId { get; init; }
    public string MemberName { get; init; }
    public string StudentNumber { get; init; }
    public bool IsCheckedIn { get; init; }
    public bool IsInvalidated { get; init; }

    /// <summary>Only an explicit Check in on a confirmed row without attendance may record.</summary>
    public bool CanCheckIn => !IsCheckedIn && !IsInvalidated;
}
