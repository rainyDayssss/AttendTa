using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories;

public sealed class AttendanceRepository : BaseRepository, IAttendanceRepository
{
    private const int RecentLogLimit = 25;

    public AttendanceRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    // Attendance writers must acquire this same row lock before deciding to insert or repeat.
    public async Task<Attendance> LockAttendanceAsync(int attendanceId, CancellationToken cancellationToken = default)
    {
        if (Context.Database.CurrentTransaction == null) throw new InvalidOperationException("A transaction is required to lock attendance.");
        var row = await GetDbSet<Attendance>().FromSqlInterpolated($"SELECT * FROM dbo.attendance WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE attendance_id = {attendanceId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var tracked = GetDbSet<Attendance>().Local.FirstOrDefault(attendance => attendance.AttendanceId == attendanceId);
        if (row == null) { if (tracked != null) Context.Entry(tracked).State = EntityState.Detached; return null; }
        if (tracked == null) { GetDbSet<Attendance>().Attach(row); return row; }
        Context.Entry(tracked).CurrentValues.SetValues(row);
        Context.Entry(tracked).OriginalValues.SetValues(row);
        Context.Entry(tracked).State = EntityState.Unchanged;
        return tracked;
    }

    public Task<Attendance> GetByRegistrationIdAsync(int registrationId, CancellationToken cancellationToken = default) =>
        GetDbSet<Attendance>().AsNoTracking()
            .SingleOrDefaultAsync(attendance => attendance.RegistrationId == registrationId, cancellationToken);

    public Task AddAsync(Attendance attendance, CancellationToken cancellationToken = default)
    {
        GetDbSet<Attendance>().Add(attendance);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<AttendanceLogRow>> GetRecentForEventAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var issued = GetDbSet<Certificate>().AsNoTracking()
            .Where(certificate => certificate.CertificateStatusId == CertificateStatusCode.Issued);

        return await (from attendance in GetDbSet<Attendance>().AsNoTracking()
                      join registration in GetDbSet<Registration>().AsNoTracking() on attendance.RegistrationId equals registration.RegistrationId
                      join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
                      join user in GetDbSet<User>().AsNoTracking() on member.UserId equals user.Id
                      where registration.EventId == eventId
                      orderby attendance.RecordedAt descending, attendance.AttendanceId descending
                      select new AttendanceLogRow(
                          attendance.AttendanceId,
                          attendance.RegistrationId,
                          user.Name,
                          member.StudentNumber,
                          attendance.RecordedAt,
                          attendance.InvalidatedAt,
                          attendance.InvalidationReason,
                          issued.Any(certificate => certificate.RegistrationId == attendance.RegistrationId)))
            .Take(RecentLogLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetValidAttendedRegistrationIdsAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var validAttendance = ParticipationFacts.ValidAttendances(GetDbSet<Attendance>().AsNoTracking());

        return await (from registration in GetDbSet<Registration>().AsNoTracking()
                      join attendance in validAttendance on registration.RegistrationId equals attendance.RegistrationId
                      where registration.EventId == eventId
                      select registration.RegistrationId)
            .Distinct()
            .OrderBy(registrationId => registrationId)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountMyAttendanceAsync(int userId, CancellationToken cancellationToken = default) =>
        (from attendance in GetDbSet<Attendance>().AsNoTracking()
         join registration in GetDbSet<Registration>().AsNoTracking() on attendance.RegistrationId equals registration.RegistrationId
         join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
         where member.UserId == userId
         select attendance.AttendanceId).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<MyAttendanceRow>> GetMyAttendanceAsync(int userId, int skip, int take, CancellationToken cancellationToken = default) =>
        await (from attendance in GetDbSet<Attendance>().AsNoTracking()
               join registration in GetDbSet<Registration>().AsNoTracking() on attendance.RegistrationId equals registration.RegistrationId
               join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
               join eventItem in GetDbSet<Event>().AsNoTracking() on registration.EventId equals eventItem.EventId
               where member.UserId == userId
               orderby attendance.RecordedAt descending, attendance.AttendanceId descending
               select new MyAttendanceRow(
                   attendance.AttendanceId,
                   eventItem.EventId,
                   eventItem.Title,
                   attendance.RecordedAt,
                   attendance.CheckInMethodId,
                   attendance.InvalidatedAt,
                   attendance.InvalidationReason))
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
}
