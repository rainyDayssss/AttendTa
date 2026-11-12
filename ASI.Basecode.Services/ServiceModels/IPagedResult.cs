namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Non-generic paging metadata shared by the Razor pager.</summary>
public interface IPagedResult
{
    int PageNumber { get; }
    int PageSize { get; }
    int TotalCount { get; }
    int TotalPages { get; }
    bool HasPreviousPage { get; }
    bool HasNextPage { get; }
    int FirstItemNumber { get; }
    int LastItemNumber { get; }
}
