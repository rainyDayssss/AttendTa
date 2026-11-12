using System;

namespace ASI.Basecode.Services.ServiceModels;

/// <summary>Describes a requested page using the application's shared fixed page size.</summary>
public sealed record PageRequest(int? PageNumber)
{
    public const int FixedPageSize = 25;

    /// <summary>Resolves an invalid page to page one and an excessive page to the last page.</summary>
    public int NormalizePageNumber(int totalCount)
    {
        if (PageNumber is null or <= 0 || totalCount <= 0)
        {
            return 1;
        }

        var totalPages = (int)(((long)totalCount + FixedPageSize - 1) / FixedPageSize);
        return Math.Min(PageNumber.Value, totalPages);
    }

    public int GetOffset(int normalizedPageNumber) => checked((normalizedPageNumber - 1) * FixedPageSize);
}
