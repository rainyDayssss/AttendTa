using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

public interface IAccountAdministrationService
{
    /// <summary>
    /// Returns one fixed-size page of accounts filtered by a name or email fragment.
    /// </summary>
    Task<PagedResult<AccountSummaryViewModel>> GetPageAsync(string search, int? pageNumber, CancellationToken cancellationToken = default);
}
