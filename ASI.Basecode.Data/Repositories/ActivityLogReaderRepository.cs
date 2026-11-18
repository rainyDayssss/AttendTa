using ASI.Basecode.Data.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories;

/// <summary>Reads activity history without tracking or changing existing log records.</summary>
public sealed class ActivityLogReaderRepository(IUnitOfWork unitOfWork, ListReadOptions readOptions = null)
    : BaseRepository(unitOfWork), IActivityLogReaderRepository
{
    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        FilteredLogs()
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ActivityLogListItem>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));

        var logs = FilteredLogs();
        var users = GetDbSet<User>().AsNoTracking();

        return await (
            from log in logs
            join user in users on log.ActorUserId equals (int?)user.Id into actorUsers
            from actor in actorUsers.DefaultIfEmpty()
            orderby log.OccurredAt descending, log.LogId descending
            select new ActivityLogListItem
            {
                LogId = log.LogId,
                ActorType = log.ActorTypeId,
                ActorName = actor == null ? null : actor.Name,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Reason = log.Reason,
                OccurredAt = log.OccurredAt
            })
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
    private IQueryable<ActivityLog> FilteredLogs()
    {
        var query = GetDbSet<ActivityLog>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(readOptions?.Search)) {
            var term = readOptions.Search;
            var actors = GetDbSet<User>().Where(user => user.Name.Contains(term)).Select(user => (int?)user.Id);
            query = query.Where(row => row.Action.Contains(term) || row.EntityType.Contains(term) || (row.Reason != null && row.Reason.Contains(term)) || actors.Contains(row.ActorUserId));
        }
        if (!string.IsNullOrWhiteSpace(readOptions?.Filter)) query = query.Where(row => row.Action == readOptions.Filter);
        if (!string.IsNullOrWhiteSpace(readOptions?.Type)) query = query.Where(row => row.EntityType == readOptions.Type);
        if (readOptions?.From is DateTime from) query = query.Where(row => row.OccurredAt >= from);
        if (readOptions?.To is DateTime to) { var end = to.Date == DateTime.MaxValue.Date ? DateTime.MaxValue : to.AddDays(1); query = query.Where(row => row.OccurredAt < end); }
        return query;
    }
}
