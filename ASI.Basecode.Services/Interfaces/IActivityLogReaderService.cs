using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

public interface IActivityLogReaderService
{
    Task<PagedResult<ActivityLogItemViewModel>> GetPageAsync(int? pageNumber, CancellationToken cancellationToken = default);
}
