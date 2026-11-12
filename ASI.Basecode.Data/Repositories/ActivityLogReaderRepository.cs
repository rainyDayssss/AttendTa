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
public sealed class ActivityLogReaderRepository(IUnitOfWork unitOfWork)
    : BaseRepository(unitOfWork), IActivityLogReaderRepository
{
    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        GetDbSet<ActivityLog>()
            .AsNoTracking()
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ActivityLogListItem>> GetPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));

        var logs = GetDbSet<ActivityLog>().AsNoTracking();
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
}
