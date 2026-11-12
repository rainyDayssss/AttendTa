using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

public interface IAccountRequestQueueService
{
    Task<AccountRequestQueueViewModel> GetPageAsync(string status, int? pageNumber, string search = null, string sort = null, CancellationToken cancellationToken = default);
}
