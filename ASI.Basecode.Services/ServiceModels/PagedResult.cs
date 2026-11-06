using System.Collections.Generic;
using System;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>A fixed-size page of records and its navigation metadata.</summary>
public sealed class PagedResult<T> : IPagedResult
{
    public PagedResult(IReadOnlyList<T> items, int pageNumber, int totalCount)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        if (totalCount < 0) throw new ArgumentOutOfRangeException(nameof(totalCount));

        PageSize = PageRequest.FixedPageSize;
        TotalCount = totalCount;
        TotalPages = totalCount == 0 ? 0 : (int)(((long)totalCount + PageSize - 1) / PageSize);
        PageNumber = TotalPages == 0 ? 1 : Math.Clamp(pageNumber, 1, TotalPages);
    }

    public IReadOnlyList<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages { get; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int LastItemNumber => TotalCount == 0 ? 0 : Math.Min(PageNumber * PageSize, TotalCount);
}
