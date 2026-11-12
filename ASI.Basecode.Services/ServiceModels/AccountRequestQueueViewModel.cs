using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>
/// One page of the Management account-request queue plus the per-status totals shown on its filters.
/// Implements <see cref="IPagedResult"/> so the shared pager can render it directly.
/// </summary>
public sealed class AccountRequestQueueViewModel : IPagedResult
{
    public AccountRequestQueueViewModel(
        PagedResult<AccountRequestQueueItemViewModel> page,
        string status,
        int pendingCount,
        int approvedCount,
        int rejectedCount,
        string search = null,
        string sort = "newest")
    {
        Page = page ?? throw new ArgumentNullException(nameof(page));
        Status = status;
        PendingCount = pendingCount;
        ApprovedCount = approvedCount;
        RejectedCount = rejectedCount;
        Search = search;
        Sort = string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase) ? "oldest" : "newest";
    }

    public PagedResult<AccountRequestQueueItemViewModel> Page { get; }
    public IReadOnlyList<AccountRequestQueueItemViewModel> Items => Page.Items;
    public string Status { get; }
    public string Search { get; }
    public string Sort { get; }
    public string NextSort => Sort == "oldest" ? "newest" : "oldest";
    public int PendingCount { get; }
    public int ApprovedCount { get; }
    public int RejectedCount { get; }
    public int AllCount => PendingCount + ApprovedCount + RejectedCount;

    public int PageNumber => Page.PageNumber;
    public int PageSize => Page.PageSize;
    public int TotalCount => Page.TotalCount;
    public int TotalPages => Page.TotalPages;
    public bool HasPreviousPage => Page.HasPreviousPage;
    public bool HasNextPage => Page.HasNextPage;
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int LastItemNumber => TotalCount == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalCount);
}
