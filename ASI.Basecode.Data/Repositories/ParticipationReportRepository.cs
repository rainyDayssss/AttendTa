using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories;

/// <summary>
/// Read-only participation report repository. One summary read composes the canonical facts; it
/// filters before counting, so the values cover the full event dataset rather than a page.
/// </summary>
public sealed class ParticipationReportRepository : BaseRepository, IParticipationReportRepository
{
    public ParticipationReportRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public async Task<ParticipationReportSummaryFacts> GetSummaryFactsAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var registrations = GetDbSet<Registration>().AsNoTracking();
        var events = GetDbSet<Event>().AsNoTracking().Where(eventItem => eventItem.EventId == eventId);
        var attendance = GetDbSet<Attendance>().AsNoTracking();

        var seatHolders = ParticipationFacts.SeatHolders(registrations);
        var validAttendance = ParticipationFacts.ValidAttendances(attendance);

        var statusCounts = await registrations
            .Where(registration => registration.EventId == eventId)
            .GroupBy(registration => registration.RegistrationStatusId)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Status, row => row.Count, cancellationToken);

        var confirmedCount = await seatHolders
            .CountAsync(registration => registration.EventId == eventId, cancellationToken);

        var validAttendeeCount = await (
                from registration in seatHolders
                join row in validAttendance on registration.RegistrationId equals row.RegistrationId
                where registration.EventId == eventId
                select registration.RegistrationId)
            .Distinct()
            .CountAsync(cancellationToken);

        var attendanceRate = await ParticipationFacts
            .AttendanceRates(events, registrations, attendance)
            .SingleOrDefaultAsync(cancellationToken);

        return new ParticipationReportSummaryFacts(
            Count(statusCounts, RegistrationStatusCode.Pending),
            confirmedCount,
            Count(statusCounts, RegistrationStatusCode.Waitlisted),
            Count(statusCounts, RegistrationStatusCode.Cancelled),
            validAttendeeCount,
            attendanceRate);
    }

    private static int Count(IReadOnlyDictionary<RegistrationStatusCode, int> counts, RegistrationStatusCode status) =>
        counts.TryGetValue(status, out var count) ? count : 0;
}
