using ASI.Basecode.Data.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories;

/// <summary>Reads activity history without tracking or changing existing log records.</summary>
public sealed class ActivityLogReaderRepository(IUnitOfWork unitOfWork, ListReadOptions readOptions = null)
    : BaseRepository(unitOfWork), IActivityLogReaderRepository
{
    private const string UsersType = "users";
    private const string AccountRequestsType = "account_requests";
    private const string MembersType = "members";
    private const string EventsType = "events";
    private const string EventTypesType = "event_types";
    private const string VenuesType = "venues";
    private const string RegistrationsType = "registrations";
    private const string AttendanceType = "attendance";
    private const string CertificatesType = "certificates";

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

        var rows = await (
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
                OccurredAt = log.OccurredAt,
                OldValues = log.OldValues,
                NewValues = log.NewValues
            })
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
        {
            await ResolveTargetsAsync(rows, cancellationToken);
        }

        return rows;
    }

    /// <summary>
    /// Resolves the current display names for the paged rows in a bounded set of batched
    /// queries. Entity id spaces stay separate because each type is keyed by its own table.
    /// </summary>
    private async Task ResolveTargetsAsync(IReadOnlyList<ActivityLogListItem> rows, CancellationToken cancellationToken)
    {
        var idsByType = rows
            .GroupBy(row => NormalizeType(row.EntityType), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.EntityId).Distinct().ToList(),
                StringComparer.Ordinal);

        var attendanceIds = IdsFor(idsByType, AttendanceType);
        var certificateIds = IdsFor(idsByType, CertificatesType);

        var attendanceLinks = attendanceIds.Count == 0
            ? new Dictionary<int, int>()
            : await GetDbSet<Attendance>()
                .AsNoTracking()
                .Where(row => attendanceIds.Contains(row.AttendanceId))
                .Select(row => new { row.AttendanceId, row.RegistrationId })
                .ToDictionaryAsync(row => row.AttendanceId, row => row.RegistrationId, cancellationToken);

        var certificateLinks = certificateIds.Count == 0
            ? new Dictionary<int, int>()
            : await GetDbSet<Certificate>()
                .AsNoTracking()
                .Where(row => certificateIds.Contains(row.CertificateId))
                .Select(row => new { row.CertificateId, row.RegistrationId })
                .ToDictionaryAsync(row => row.CertificateId, row => row.RegistrationId, cancellationToken);

        var registrationIds = new HashSet<int>(IdsFor(idsByType, RegistrationsType));
        registrationIds.UnionWith(attendanceLinks.Values);
        registrationIds.UnionWith(certificateLinks.Values);

        var registrations = registrationIds.Count == 0
            ? new Dictionary<int, RegistrationLink>()
            : (await GetDbSet<Registration>()
                    .AsNoTracking()
                    .Where(row => registrationIds.Contains(row.RegistrationId))
                    .Select(row => new { row.RegistrationId, row.MemberId, row.EventId })
                    .ToListAsync(cancellationToken))
                .ToDictionary(
                    row => row.RegistrationId,
                    row => new RegistrationLink(row.MemberId, row.EventId));

        var memberIds = new HashSet<int>(IdsFor(idsByType, MembersType));
        memberIds.UnionWith(registrations.Values.Select(link => link.MemberId));

        var memberUserIds = memberIds.Count == 0
            ? new Dictionary<int, int>()
            : await GetDbSet<Member>()
                .AsNoTracking()
                .Where(row => memberIds.Contains(row.MemberId))
                .Select(row => new { row.MemberId, row.UserId })
                .ToDictionaryAsync(row => row.MemberId, row => row.UserId, cancellationToken);

        var userIds = new HashSet<int>(IdsFor(idsByType, UsersType));
        userIds.UnionWith(memberUserIds.Values);
        var userNames = userIds.Count == 0
            ? new Dictionary<int, string>()
            : await GetDbSet<User>()
                .AsNoTracking()
                .Where(row => userIds.Contains(row.Id))
                .Select(row => new { row.Id, row.Name })
                .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);

        var eventIds = new HashSet<int>(IdsFor(idsByType, EventsType));
        eventIds.UnionWith(registrations.Values.Select(link => link.EventId));
        var eventTitles = eventIds.Count == 0
            ? new Dictionary<int, string>()
            : await GetDbSet<Event>()
                .AsNoTracking()
                .Where(row => eventIds.Contains(row.EventId))
                .Select(row => new { row.EventId, row.Title })
                .ToDictionaryAsync(row => row.EventId, row => row.Title, cancellationToken);

        var requestIds = IdsFor(idsByType, AccountRequestsType);
        var requestNames = requestIds.Count == 0
            ? new Dictionary<int, string>()
            : await GetDbSet<AccountRequest>()
                .AsNoTracking()
                .Where(row => requestIds.Contains(row.RequestId))
                .Select(row => new { row.RequestId, row.FullName })
                .ToDictionaryAsync(row => row.RequestId, row => row.FullName, cancellationToken);

        var typeIds = IdsFor(idsByType, EventTypesType);
        var typeNames = typeIds.Count == 0
            ? new Dictionary<int, string>()
            : await GetDbSet<EventType>()
                .AsNoTracking()
                .Where(row => typeIds.Contains(row.TypeId))
                .Select(row => new { row.TypeId, row.TypeName })
                .ToDictionaryAsync(row => row.TypeId, row => row.TypeName, cancellationToken);

        var venueIds = IdsFor(idsByType, VenuesType);
        var venueNames = venueIds.Count == 0
            ? new Dictionary<int, string>()
            : await GetDbSet<Venue>()
                .AsNoTracking()
                .Where(row => venueIds.Contains(row.VenueId))
                .Select(row => new { row.VenueId, row.RoomName })
                .ToDictionaryAsync(row => row.VenueId, row => row.RoomName, cancellationToken);

        foreach (var row in rows)
        {
            switch (NormalizeType(row.EntityType))
            {
                case UsersType:
                    row.TargetName = ValueOrNull(userNames, row.EntityId);
                    break;
                case AccountRequestsType:
                    row.TargetName = ValueOrNull(requestNames, row.EntityId);
                    break;
                case MembersType:
                    row.TargetName = memberUserIds.TryGetValue(row.EntityId, out var memberUserId)
                        ? ValueOrNull(userNames, memberUserId)
                        : null;
                    break;
                case EventsType:
                    row.TargetName = ValueOrNull(eventTitles, row.EntityId);
                    break;
                case EventTypesType:
                    row.TargetName = ValueOrNull(typeNames, row.EntityId);
                    break;
                case VenuesType:
                    row.TargetName = ValueOrNull(venueNames, row.EntityId);
                    break;
                case RegistrationsType:
                    AssignRegistrationTarget(row, row.EntityId, registrations, memberUserIds, userNames, eventTitles);
                    break;
                case AttendanceType:
                    if (attendanceLinks.TryGetValue(row.EntityId, out var attendanceRegistrationId))
                    {
                        AssignRegistrationTarget(row, attendanceRegistrationId, registrations, memberUserIds, userNames, eventTitles);
                    }
                    break;
                case CertificatesType:
                    if (certificateLinks.TryGetValue(row.EntityId, out var certificateRegistrationId))
                    {
                        AssignRegistrationTarget(row, certificateRegistrationId, registrations, memberUserIds, userNames, eventTitles);
                    }
                    break;
            }
        }
    }

    private static void AssignRegistrationTarget(
        ActivityLogListItem row,
        int registrationId,
        IReadOnlyDictionary<int, RegistrationLink> registrations,
        IReadOnlyDictionary<int, int> memberUserIds,
        IReadOnlyDictionary<int, string> userNames,
        IReadOnlyDictionary<int, string> eventTitles)
    {
        if (!registrations.TryGetValue(registrationId, out var link))
        {
            return;
        }

        row.TargetName = memberUserIds.TryGetValue(link.MemberId, out var userId)
            ? ValueOrNull(userNames, userId)
            : null;
        row.TargetContext = ValueOrNull(eventTitles, link.EventId);
    }

    private static string ValueOrNull(IReadOnlyDictionary<int, string> values, int key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static List<int> IdsFor(IReadOnlyDictionary<string, List<int>> idsByType, string entityType) =>
        idsByType.TryGetValue(entityType, out var ids) ? ids : new List<int>();

    private static string NormalizeType(string entityType) =>
        (entityType ?? string.Empty).Trim().ToLowerInvariant();

    // Count and page reads share these conditions so paging totals match the selected filters.
    private IQueryable<ActivityLog> FilteredLogs()
    {
        var query = GetDbSet<ActivityLog>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(readOptions?.Search)) {
            var term = readOptions.Search;
            var matchingActions = ActivityActionLabels.MatchingCodes(term);
            var actors = GetDbSet<User>().Where(user => user.Name.Contains(term)).Select(user => (int?)user.Id);
            query = query.Where(row => matchingActions.Contains(row.Action) || row.Action.Contains(term) || row.EntityType.Contains(term) || (row.Reason != null && row.Reason.Contains(term)) || actors.Contains(row.ActorUserId));
        }
        if (!string.IsNullOrWhiteSpace(readOptions?.Filter)) query = query.Where(row => row.Action == readOptions.Filter);
        if (!string.IsNullOrWhiteSpace(readOptions?.Type)) query = query.Where(row => row.EntityType == readOptions.Type);
        if (readOptions?.From is DateTime from) query = query.Where(row => row.OccurredAt >= from);
        if (readOptions?.To is DateTime to) { var end = to.Date == DateTime.MaxValue.Date ? DateTime.MaxValue : to.AddDays(1); query = query.Where(row => row.OccurredAt < end); }
        return query;
    }

    private readonly record struct RegistrationLink(int MemberId, int EventId);
}
