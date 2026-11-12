using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The member Participation History reads. Registrations and attendance each come from one
/// owner-scoped cross-event query, paged at the shared page size; the row-to-card mapping lives in
/// the AutoMapper profile so the service only supplies the per-row cutoff fact.
/// </summary>
public sealed class MyEventsService(
    IRegistrationRepository registrations,
    IAttendanceRepository attendance,
    ICurrentActor actor,
    TimeProvider clock,
    IMapper mapper) : IMyEventsService
{
    public async Task<PagedResult<MyEventRegistrationCardViewModel>> GetRegistrationsAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        // The member policy guards the route; the actor is the only source of ownership.
        var userId = actor.UserId;
        var (rows, page, totalCount) = await ReadPageAsync(
            token => registrations.CountMyRegistrationsAsync(userId, token),
            (skip, take, token) => registrations.GetMyRegistrationsAsync(userId, skip, take, token),
            pageNumber,
            cancellationToken);

        var startCutoffPassed = ParticipationFacts.StartCutoffPassed(clock.GetUtcNow().UtcDateTime).Compile();
        var cards = rows
            .Select(row => mapper.Map<MyEventRegistrationCardViewModel>(
                row,
                options => options.Items[MyEventsMappingItems.CutoffPassed] =
                    startCutoffPassed(new Event { StartsAt = row.StartsAt })))
            .ToList();

        return new PagedResult<MyEventRegistrationCardViewModel>(cards, page, totalCount);
    }

    public async Task<PagedResult<MyEventAttendanceRowViewModel>> GetAttendanceAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        // The member policy guards the route; the actor is the only source of ownership.
        var userId = actor.UserId;
        var (rows, page, totalCount) = await ReadPageAsync(
            token => attendance.CountMyAttendanceAsync(userId, token),
            (skip, take, token) => attendance.GetMyAttendanceAsync(userId, skip, take, token),
            pageNumber,
            cancellationToken);

        var records = rows
            .Select(row => mapper.Map<MyEventAttendanceRowViewModel>(row))
            .ToList();

        return new PagedResult<MyEventAttendanceRowViewModel>(records, page, totalCount);
    }

    /// <summary>
    /// The one paging shape both tabs share: count, normalize the requested page, and read only that
    /// page. A tab with no records skips the page read and reports page one of zero.
    /// </summary>
    private static async Task<(IReadOnlyList<TRow> Rows, int PageNumber, int TotalCount)> ReadPageAsync<TRow>(
        Func<CancellationToken, Task<int>> countAsync,
        Func<int, int, CancellationToken, Task<IReadOnlyList<TRow>>> readPageAsync,
        int? pageNumber,
        CancellationToken cancellationToken)
    {
        var totalCount = await countAsync(cancellationToken);
        var request = new PageRequest(pageNumber);
        var page = request.NormalizePageNumber(totalCount);
        var rows = totalCount == 0
            ? Array.Empty<TRow>()
            : (await readPageAsync(request.GetOffset(page), PageRequest.FixedPageSize, cancellationToken)).ToArray();

        return (rows, page, totalCount);
    }
}
