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

/// <summary>
/// Read-only participation report repository. One query returns every registration of the event with
/// its member, signup snapshots and attendance, so the report's summary, no-shows, distribution and
/// detail rows are all computed from a single materialized snapshot rather than five independent
/// reads that can disagree.
/// </summary>
public sealed class ParticipationReportRepository : BaseRepository, IParticipationReportRepository
{
    public ParticipationReportRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public async Task<IReadOnlyList<ParticipationReportDetailRow>> GetSnapshotAsync(
        int eventId,
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
                InvalidatedAt = (DateTime?)attendance.InvalidatedAt
            })
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
                row.InvalidatedAt))
            .ToList();
    }
}
