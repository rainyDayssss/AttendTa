using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Queries;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
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

        // Valid Attendance is the canonical fact; a registration already carrying any certificate row
        // is left untouched so the unique registration index is never reached.
        return await (from registration in GetDbSet<Registration>().AsNoTracking()
                      join attendance in validAttendance on registration.RegistrationId equals attendance.RegistrationId
                      where registration.EventId == eventId
                            && !certificates.Any(certificate => certificate.RegistrationId == registration.RegistrationId)
                      select registration.RegistrationId)
            .Distinct()
            .OrderBy(registrationId => registrationId)
            .ToListAsync(ct);
    }

    public Task AddRangeAsync(IReadOnlyList<Certificate> certificates, CancellationToken ct = default)
    {
        GetDbSet<Certificate>().AddRange(certificates);
        return Task.CompletedTask;
    }
}
