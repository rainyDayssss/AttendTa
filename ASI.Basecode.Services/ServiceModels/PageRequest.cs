using System;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Describes a requested page using a validated, bounded page size.</summary>
public sealed record PageRequest(int? PageNumber, int? RequestedSize = null)
{
    public const int DefaultPageSize = 5;
    public int PageSize => NormalizeSize(RequestedSize);
    public static int NormalizeSize(int? size) => Math.Clamp(size ?? DefaultPageSize, 1, 25);

    /// <summary>Resolves an invalid page to page one and an excessive page to the last page.</summary>
    public int NormalizePageNumber(int totalCount)
    {
        if (PageNumber is null or <= 0 || totalCount <= 0)
        {
            return 1;
        }

        var totalPages = (int)(((long)totalCount + PageSize - 1) / PageSize);
        return Math.Min(PageNumber.Value, totalPages);
    }

    public int GetOffset(int normalizedPageNumber) => checked((normalizedPageNumber - 1) * PageSize);
}
