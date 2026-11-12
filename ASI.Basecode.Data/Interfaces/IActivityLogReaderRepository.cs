using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces;

/// <summary>Read-only queries for the append-only activity history.</summary>
public interface IActivityLogReaderRepository
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityLogListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default);
}
