using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The member Participation History reads. Registrations come from one owner-scoped cross-event
/// query; attendance stays an empty page until 4.7 supplies the real owner-scoped query.
/// </summary>
public sealed class MyEventsService(
    IRegistrationRepository registrations,
    ICurrentActor actor,
    TimeProvider clock) : IMyEventsService
{
    public async Task<PagedResult<MyEventRegistrationCardViewModel>> GetRegistrationsAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        // The member policy guards the route; the actor is the only source of ownership.
        var userId = actor.UserId;
        var totalCount = await registrations.CountMyRegistrationsAsync(userId, cancellationToken);

        var pageRequest = new PageRequest(pageNumber);
        var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
        var rows = totalCount == 0
            ? Array.Empty<MyEventRegistrationRow>()
            : (await registrations.GetMyRegistrationsAsync(
                userId,
                pageRequest.GetOffset(normalizedPage),
                PageRequest.FixedPageSize,
                cancellationToken)).ToArray();

        var startCutoffPassed = ParticipationFacts.StartCutoffPassed(clock.GetUtcNow().UtcDateTime).Compile();
        var cards = new MyEventRegistrationCardViewModel[rows.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            // The label is derived from the canonical cutoff fact; the stored status is unchanged.
            var cutoffPassed = startCutoffPassed(new Event { StartsAt = row.StartsAt });
            cards[index] = new MyEventRegistrationCardViewModel
            {
                RegistrationId = row.RegistrationId,
                EventId = row.EventId,
                EventTitle = row.EventTitle,
                TypeName = row.TypeName,
                VenueName = row.VenueName,
                StartsAt = row.StartsAt,
                StatusLabel = RegistrationStatusDisplay.Label(row.Status, cutoffPassed),
                StatusBadgeClass = RegistrationStatusDisplay.BadgeClass(row.Status, cutoffPassed),
                CanViewEvent = row.EventStatusId is EventStatusCode.Published or EventStatusCode.Completed
            };
        }

        return new PagedResult<MyEventRegistrationCardViewModel>(cards, normalizedPage, totalCount);
    }

    // 4.7 replaces this method with the real owner-scoped attendance query.
    public Task<PagedResult<MyEventAttendanceRowViewModel>> GetAttendanceAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<MyEventAttendanceRowViewModel>(
            Array.Empty<MyEventAttendanceRowViewModel>(),
            1,
            0));
}
