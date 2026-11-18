using ASI.Basecode.Data.Queries;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

public sealed class ActivityLogReaderService(IActivityLogReaderRepository activityLogRepository, ListReadOptions readOptions = null) : IActivityLogReaderService
{
    public async Task<PagedResult<ActivityLogItemViewModel>> GetPageAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await activityLogRepository.CountAsync(cancellationToken);
        var pageRequest = new PageRequest(pageNumber, readOptions?.PageSize);
        var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
        var records = await activityLogRepository.GetPageAsync(
            pageRequest.GetOffset(normalizedPage),
            PageRequest.NormalizeSize(readOptions?.PageSize),
            cancellationToken);

        var items = records.Select(record => new ActivityLogItemViewModel
        {
            LogId = record.LogId,
            ActorType = record.ActorType == ActorTypeCode.System ? "System" : "User",
            ActorName = record.ActorType == ActorTypeCode.System
                ? "System process"
                : string.IsNullOrWhiteSpace(record.ActorName) ? "Unknown user" : record.ActorName,
            Action = record.Action,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            Reason = record.Reason,
            OccurredAt = record.OccurredAt
        }).ToList();

        return new PagedResult<ActivityLogItemViewModel>(items, normalizedPage, totalCount, PageRequest.NormalizeSize(readOptions?.PageSize));
    }
}
