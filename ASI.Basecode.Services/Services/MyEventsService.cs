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
/// The member Participation History reads. Registrations and attendance each come from one
/// owner-scoped cross-event query, paged at the shared page size.
/// </summary>
public sealed class MyEventsService(
    IRegistrationRepository registrations,
    IAttendanceRepository attendance,
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

    public async Task<PagedResult<MyEventAttendanceRowViewModel>> GetAttendanceAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        // The member policy guards the route; the actor is the only source of ownership.
        var userId = actor.UserId;
        var totalCount = await attendance.CountMyAttendanceAsync(userId, cancellationToken);

        var pageRequest = new PageRequest(pageNumber);
        var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
        var rows = totalCount == 0
            ? Array.Empty<MyAttendanceRow>()
            : (await attendance.GetMyAttendanceAsync(
                userId,
                pageRequest.GetOffset(normalizedPage),
                PageRequest.FixedPageSize,
                cancellationToken)).ToArray();

        var records = new MyEventAttendanceRowViewModel[rows.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            records[index] = new MyEventAttendanceRowViewModel
            {
                AttendanceId = row.AttendanceId,
                EventId = row.EventId,
                EventTitle = row.EventTitle,
                RecordedAt = row.RecordedAt,
                CheckInMethodLabel = CheckInMethodDisplay.Label(row.CheckInMethod),
                IsInvalidated = row.InvalidatedAt is not null,
                InvalidationReason = row.InvalidationReason
            };
        }

        return new PagedResult<MyEventAttendanceRowViewModel>(records, normalizedPage, totalCount);
    }
}
