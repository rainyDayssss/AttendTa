using ASI.Basecode.Services.ServiceModels;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class PagingTests
{
    [Theory]
    [InlineData(null, 26, 1)]
    [InlineData(0, 26, 1)]
    [InlineData(-10, 26, 1)]
    [InlineData(1, 26, 1)]
    [InlineData(2, 26, 2)]
    [InlineData(int.MaxValue, 26, 2)]
    [InlineData(50, 0, 1)]
    public void PageRequestNormalizesInvalidAndOutOfRangePages(int? requested, int totalCount, int expected)
    {
        Assert.Equal(expected, new PageRequest(requested).NormalizePageNumber(totalCount));
    }

    [Fact]
    public void PagedResultUsesFixedSizeAndReportsNavigationForTwentySixRows()
    {
        var firstPage = new PagedResult<int>(Enumerable.Range(1, 25).ToArray(), 1, 26);
        var lastPage = new PagedResult<int>(new[] { 26 }, 2, 26);

        Assert.Equal(25, firstPage.PageSize);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.True(firstPage.HasNextPage);
        Assert.False(firstPage.HasPreviousPage);
        Assert.Equal(1, firstPage.FirstItemNumber);
        Assert.Equal(25, firstPage.LastItemNumber);
        Assert.True(lastPage.HasPreviousPage);
        Assert.False(lastPage.HasNextPage);
        Assert.Equal(26, lastPage.FirstItemNumber);
        Assert.Equal(26, lastPage.LastItemNumber);
    }

    [Fact]
    public void EmptyPagedResultHasOneNormalizedPageAndNoNavigation()
    {
        var empty = new PagedResult<string>(Array.Empty<string>(), int.MaxValue, 0);

        Assert.Equal(1, empty.PageNumber);
        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasPreviousPage);
        Assert.False(empty.HasNextPage);
        Assert.Equal(0, empty.FirstItemNumber);
        Assert.Equal(0, empty.LastItemNumber);
    }

    [Fact]
    public void ExtremeCountsKeepPageAndOffsetArithmeticInRange()
    {
        var request = new PageRequest(int.MaxValue);
        var normalizedPage = request.NormalizePageNumber(int.MaxValue);

        Assert.Equal(85_899_346, normalizedPage);
        Assert.Equal(2_147_483_625, request.GetOffset(normalizedPage));
    }
}
