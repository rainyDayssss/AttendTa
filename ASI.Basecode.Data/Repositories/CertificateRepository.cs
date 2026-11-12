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

public sealed class CertificateRepository : BaseRepository, ICertificateRepository
{
    public CertificateRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public async Task<IReadOnlyList<int>> GetRegistrationsAwaitingReviewAsync(int eventId, CancellationToken ct = default)
    {
        var validAttendance = ParticipationFacts.ValidAttendances(GetDbSet<Attendance>().AsNoTracking());
        var certificates = GetDbSet<Certificate>().AsNoTracking();

        // Valid Attendance is the canonical fact, and only a Confirmed seat is a certificate
        // candidate: a cancelled or waitlisted registration with an old attendance row never gets a
        // review. A registration already carrying any certificate row is left untouched so the unique
        // registration index is never reached.
        return await (from registration in ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking())
                      join attendance in validAttendance on registration.RegistrationId equals attendance.RegistrationId
                      where registration.EventId == eventId
                            && !certificates.Any(certificate => certificate.RegistrationId == registration.RegistrationId)
                      select registration.RegistrationId)
            .Distinct()
            .OrderBy(registrationId => registrationId)
            .ToListAsync(ct);
    }

    public Task<Certificate> GetByRegistrationIdAsync(int registrationId, CancellationToken ct = default) =>
        GetDbSet<Certificate>().SingleOrDefaultAsync(row => row.RegistrationId == registrationId, ct);

    public Task<Certificate> GetByRegistrationIdNoTrackingAsync(int registrationId, CancellationToken ct = default) =>
        GetDbSet<Certificate>().AsNoTracking().SingleOrDefaultAsync(row => row.RegistrationId == registrationId, ct);

    public async Task<IReadOnlyList<CertificateReviewQueueRow>> GetReviewQueueAsync(int eventId, CancellationToken ct = default)
    {
        var attendance = GetDbSet<Attendance>().AsNoTracking();
        var certificates = GetDbSet<Certificate>().AsNoTracking();

        return await (from registration in ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking())
                      join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
                      join user in GetDbSet<User>().AsNoTracking() on member.UserId equals user.Id
                      join attendanceRow in attendance on registration.RegistrationId equals attendanceRow.RegistrationId into attendanceRows
                      from attendanceRow in attendanceRows.DefaultIfEmpty()
                      join certificate in certificates on registration.RegistrationId equals certificate.RegistrationId into certificateRows
                      from certificate in certificateRows.DefaultIfEmpty()
                      where registration.EventId == eventId
                      orderby user.Name, registration.RegistrationId
                      select new CertificateReviewQueueRow(
                          registration.RegistrationId,
                          member.MemberId,
                          user.Name,
                          member.StudentNumber,
                          attendanceRow == null ? (DateTime?)null : attendanceRow.RecordedAt,
                          certificate == null ? (int?)null : certificate.CertificateId,
                          certificate == null ? (CertificateStatusCode?)null : certificate.CertificateStatusId,
                          certificate == null ? null : certificate.ReviewNotes,
                          certificate == null ? null : certificate.RecipientNameSnapshot,
                          certificate == null ? null : certificate.EventTitleSnapshot,
                          certificate == null ? null : certificate.CertificateNumber,
                          certificate == null ? (DateTime?)null : certificate.IssuedAt))
            .ToListAsync(ct);
    }

    public Task AddRangeAsync(IReadOnlyList<Certificate> certificates, CancellationToken ct = default)
    {
        GetDbSet<Certificate>().AddRange(certificates);
        return Task.CompletedTask;
    }
}
