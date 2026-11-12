using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class AccountAdministrationServiceTests
{
    [Theory]
    [InlineData(null, 1, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(-3, 1, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(2, 2, 25)]
    [InlineData(int.MaxValue, 2, 25)]
    public async Task PageNumberIsNormalizedBeforeOneFixedSizePageIsFetched(int? requestedPage, int expectedPage, int expectedSkip)
    {
        var repository = new FakeRepository(totalCount: 26);
        var service = new AccountAdministrationService(repository);

        var result = await service.GetPageAsync(null, requestedPage, TestContext.Current.CancellationToken);

        Assert.Equal(expectedPage, result.PageNumber);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(26, result.TotalCount);
        Assert.Equal(expectedSkip, repository.LastSkip);
        Assert.Equal(PageRequest.FixedPageSize, repository.LastTake);
    }

    [Fact]
    public async Task EmptyResultUsesPageOneWithoutNavigation()
    {
        var repository = new FakeRepository(totalCount: 0);
        var service = new AccountAdministrationService(repository);

        var result = await service.GetPageAsync("nobody", 9, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
        Assert.Equal(0, repository.LastSkip);
    }

    [Theory]
    [InlineData("  Ana  ", "Ana")]
    [InlineData("reyes@example.com", "reyes@example.com")]
    public async Task SearchIsTrimmedAndAppliedToBothCountAndPage(string input, string expected)
    {
        var repository = new FakeRepository(totalCount: 0);
        var service = new AccountAdministrationService(repository);

        await service.GetPageAsync(input, 1, TestContext.Current.CancellationToken);

        Assert.Equal(expected, repository.CountSearch);
        Assert.Equal(expected, repository.PageSearch);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankSearchIsTreatedAsNoFilter(string? input)
    {
        var repository = new FakeRepository(totalCount: 0);
        var service = new AccountAdministrationService(repository);

        await service.GetPageAsync(input!, 1, TestContext.Current.CancellationToken);

        Assert.Null(repository.CountSearch);
        Assert.Null(repository.PageSearch);
    }

    [Fact]
    public async Task RecordsMapToDisplayFields()
    {
        var repository = new FakeRepository(totalCount: 1);
        repository.Records.Add(new AccountListItem
        {
            AccountId = 7,
            FullName = "Ana Reyes",
            Email = "ana.reyes@example.com",
            RoleCode = "officer",
            RoleLabel = "Officer",
            IsActive = false
        });
        var service = new AccountAdministrationService(repository);

        var result = await service.GetPageAsync(null, 1, TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal(7, item.AccountId);
        Assert.Equal("Ana Reyes", item.FullName);
        Assert.Equal("ana.reyes@example.com", item.Email);
        Assert.Equal("officer", item.RoleCode);
        Assert.Equal("Officer", item.RoleLabel);
        Assert.False(item.IsActive);
    }

    private sealed class FakeRepository(int totalCount) : IAccountAdministrationRepository
    {
        public List<AccountListItem> Records { get; } = new();
        public string? CountSearch { get; private set; }
        public string? PageSearch { get; private set; }
        public int LastSkip { get; private set; } = -1;
        public int LastTake { get; private set; }

        public Task<int> CountAsync(string search, CancellationToken cancellationToken = default)
        {
            CountSearch = search;
            return Task.FromResult(totalCount);
        }

        public Task<IReadOnlyList<AccountListItem>> GetPageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
        {
            PageSearch = search;
            LastSkip = skip;
            LastTake = take;
            return Task.FromResult<IReadOnlyList<AccountListItem>>(Records);
        }

        public Task<AccountListItem?> GetAccountAsync(int accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Records.FirstOrDefault(record => record.AccountId == accountId));

        public Task<IReadOnlyList<RoleOption>> GetRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoleOption>>([]);

        public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task AcquireLastAdministratorLockAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
