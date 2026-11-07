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
/// Read-only participation report repository. Each read composes the canonical facts; it filters
/// before counting, so the values cover the full event dataset rather than a page.
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

        var validAttendeeCount = await (ValidAttendeeIds(seatHolders, validAttendance, eventId))
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

    public async Task<int> GetNoShowCountAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var registrations = GetDbSet<Registration>().AsNoTracking();
        var events = GetDbSet<Event>().AsNoTracking();
        var attendance = GetDbSet<Attendance>().AsNoTracking();

        return await ParticipationFacts
            .NoShows(registrations, events, attendance)
            .CountAsync(registration => registration.EventId == eventId, cancellationToken);
    }

    public async Task<ParticipationReportDistributionFacts> GetAttendeeDistributionAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var registrations = GetDbSet<Registration>().AsNoTracking();
        var attendance = GetDbSet<Attendance>().AsNoTracking();

        var seatHolders = ParticipationFacts.SeatHolders(registrations);
        var validAttendance = ParticipationFacts.ValidAttendances(attendance);
        var validAttendeeIds = ValidAttendeeIds(seatHolders, validAttendance, eventId);

        // Group the signup snapshots directly: no users/members join, so an attendee whose account or
        // membership is now inactive still counts (SPEC §15).
        var attendees = seatHolders.Where(registration => validAttendeeIds.Contains(registration.RegistrationId));

        var courses = await attendees
            .GroupBy(registration => registration.CourseSnapshot)
            .Select(group => new { Course = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var years = await attendees
            .GroupBy(registration => registration.YearLevelSnapshot)
            .Select(group => new { YearLevel = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return new ParticipationReportDistributionFacts(
            courses.Select(row => new CourseDistributionRow(row.Course, row.Count)).ToList(),
            years.Select(row => new YearDistributionRow(row.YearLevel, row.Count)).ToList());
    }

    private static IQueryable<int> ValidAttendeeIds(
        IQueryable<Registration> seatHolders,
        IQueryable<Attendance> validAttendance,
        int eventId) =>
        (from registration in seatHolders
         join row in validAttendance on registration.RegistrationId equals row.RegistrationId
         where registration.EventId == eventId
         select registration.RegistrationId)
        .Distinct();

    private static int Count(IReadOnlyDictionary<RegistrationStatusCode, int> counts, RegistrationStatusCode status) =>
        counts.TryGetValue(status, out var count) ? count : 0;
}
