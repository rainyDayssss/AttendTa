using ASI.Basecode.Data.Queries;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

public sealed class AccountAdministrationService(IAccountAdministrationRepository accountAdministrationRepository, ListReadOptions readOptions = null) : IAccountAdministrationService
{
    public async Task<PagedResult<AccountSummaryViewModel>> GetPageAsync(
        string search,
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var totalCount = await accountAdministrationRepository.CountAsync(normalizedSearch, cancellationToken);
        var pageRequest = new PageRequest(pageNumber, readOptions?.PageSize);
        var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
        var records = await accountAdministrationRepository.GetPageAsync(
            normalizedSearch,
            pageRequest.GetOffset(normalizedPage),
            PageRequest.NormalizeSize(readOptions?.PageSize),
            cancellationToken);

        var items = records.Select(record => new AccountSummaryViewModel
        {
            AccountId = record.AccountId,
            FullName = record.FullName,
            Email = record.Email,
            RoleCode = record.RoleCode,
            RoleLabel = record.RoleLabel,
            IsActive = record.IsActive
        }).ToList();

        return new PagedResult<AccountSummaryViewModel>(items, normalizedPage, totalCount, PageRequest.NormalizeSize(readOptions?.PageSize));
    }
}
