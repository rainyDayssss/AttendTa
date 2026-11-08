using System;
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
/// Read-only participation report repository. The summary, no-show and distribution reads compose
/// the canonical facts; the detail reads join the event's registrations to their member and
/// attendance. Each read filters before counting, so the values cover the full event dataset rather
/// than a page.
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
        // membership is now inactive still counts (SPEC §15). One grouped query keeps both breakdowns
        // on the same attendee set and scans the valid-attendee set once.
        var attendees = seatHolders.Where(registration => validAttendeeIds.Contains(registration.RegistrationId));

        var groups = await attendees
            .GroupBy(registration => new { registration.CourseSnapshot, registration.YearLevelSnapshot })
            .Select(group => new
            {
                group.Key.CourseSnapshot,
                group.Key.YearLevelSnapshot,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var courses = groups
            .GroupBy(row => row.CourseSnapshot)
            .Select(group => new CourseDistributionRow(group.Key, group.Sum(row => row.Count)))
            .ToList();

        var years = groups
            .GroupBy(row => row.YearLevelSnapshot)
            .Select(group => new YearDistributionRow(group.Key, group.Sum(row => row.Count)))
            .ToList();

        return new ParticipationReportDistributionFacts(courses, years);
    }

    public Task<int> CountDetailRowsAsync(int eventId, CancellationToken cancellationToken = default) =>
        GetDbSet<Registration>()
            .AsNoTracking()
            .CountAsync(registration => registration.EventId == eventId, cancellationToken);

    public async Task<IReadOnlyList<ParticipationReportDetailRow>> GetDetailPageAsync(
        int eventId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // Every registration of the event, all four statuses, joined to the member's name and student
        // number. Attendance is left-joined because attendance.registration_id is UNIQUE, so it can
        // never multiply a row; a null CheckedInAt is the only "no attendance row" signal.
        var rows = await (
            from registration in GetDbSet<Registration>().AsNoTracking()
            join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
            join user in GetDbSet<User>().AsNoTracking() on member.UserId equals user.Id
            join attendance in GetDbSet<Attendance>().AsNoTracking()
                on registration.RegistrationId equals attendance.RegistrationId into attendanceRows
            from attendance in attendanceRows.DefaultIfEmpty()
            where registration.EventId == eventId
            orderby user.Name, registration.RegistrationId
            select new
            {
                registration.RegistrationId,
                MemberName = user.Name,
                member.StudentNumber,
                Course = registration.CourseSnapshot,
                YearLevel = registration.YearLevelSnapshot,
                Status = registration.RegistrationStatusId,
                CheckedInAt = (DateTime?)attendance.RecordedAt,
                IsAttendanceInvalidated = attendance != null && attendance.InvalidatedAt != null
            })
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        // Materialize the anonymous projection first so EF never has to construct the record.
        return rows
            .Select(row => new ParticipationReportDetailRow(
                row.RegistrationId,
                row.MemberName,
                row.StudentNumber,
                row.Course,
                row.YearLevel,
                row.Status,
                row.CheckedInAt,
                row.IsAttendanceInvalidated))
            .ToList();
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
