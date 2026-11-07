using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories;

public sealed class RegistrationRepository : BaseRepository, IRegistrationRepository
{
    public RegistrationRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public Task<bool> IsConfirmedAsync(int registrationId, CancellationToken cancellationToken = default) =>
        GetDbSet<Registration>().AnyAsync(
            registration => registration.RegistrationId == registrationId
                && registration.RegistrationStatusId == RegistrationStatusCode.Confirmed,
            cancellationToken);

    public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken cancellationToken = default) =>
        GetDbSet<Registration>().AsNoTracking().AnyAsync(
            registration => registration.EventId == eventId && registration.MemberId == memberId,
            cancellationToken);

    public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default) =>
        (from registration in GetDbSet<Registration>().AsNoTracking()
         join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
         where registration.EventId == eventId && member.UserId == userId
         select new MemberRegistrationRow(registration.RegistrationId, member.MemberId, registration.EventId, registration.RegistrationStatusId))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken cancellationToken = default) =>
        (from registration in GetDbSet<Registration>().AsNoTracking()
         join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
         where member.UserId == userId
         select registration.RegistrationId).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken cancellationToken = default) =>
        await (from registration in GetDbSet<Registration>().AsNoTracking()
               join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
               join eventItem in GetDbSet<Event>().AsNoTracking() on registration.EventId equals eventItem.EventId
               join eventType in GetDbSet<EventType>().AsNoTracking() on eventItem.TypeId equals eventType.TypeId
               join venue in GetDbSet<Venue>().AsNoTracking() on eventItem.VenueId equals venue.VenueId
               where member.UserId == userId
               orderby eventItem.StartsAt descending, registration.RegistrationId descending
               select new MyEventRegistrationRow(
                   registration.RegistrationId,
                   eventItem.EventId,
                   eventItem.Title,
                   eventType.TypeName,
                   venue.RoomName,
                   eventItem.StartsAt,
                   eventItem.EventStatusId,
                   registration.RegistrationStatusId))
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) =>
        ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking())
            .CountAsync(registration => registration.EventId == eventId, cancellationToken);

    public Task AddAsync(Registration registration, CancellationToken cancellationToken = default)
    {
        GetDbSet<Registration>().Add(registration);
        return Task.CompletedTask;
    }

    public Task<RegistrationReviewIdentity> DiscoverReviewAsync(int id, CancellationToken ct = default) =>
        (from r in GetDbSet<Registration>().AsNoTracking()
         join m in GetDbSet<Member>().AsNoTracking() on r.MemberId equals m.MemberId
         where r.RegistrationId == id
         select new RegistrationReviewIdentity(m.UserId, m.MemberId, r.EventId)).SingleOrDefaultAsync(ct);

    public Task<bool> HasEligibleWaitlistAsync(int eventId, CancellationToken ct = default)
    {
        var eligible = ParticipationFacts.EligibleParticipantMemberIds(GetDbSet<User>().AsNoTracking(), GetDbSet<Member>().AsNoTracking());
        return ParticipationFacts.WaitlistPriority(GetDbSet<Registration>().AsNoTracking(), eventId).AnyAsync(r => eligible.Contains(r.MemberId), ct);
    }

    public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default)
    {
        var eligible = ParticipationFacts.EligibleParticipantMemberIds(GetDbSet<User>().AsNoTracking(), GetDbSet<Member>().AsNoTracking());
        var queue = from r in ParticipationFacts.WaitlistPriority(GetDbSet<Registration>().AsNoTracking(), eventId)
                    join m in GetDbSet<Member>().AsNoTracking() on r.MemberId equals m.MemberId
                    where eligible.Contains(r.MemberId)
                    select new RegistrationPromotionCandidate(r.RegistrationId, m.UserId, m.MemberId, r.EventId, r.ReviewedAt);
        return queue.FirstOrDefaultAsync(ct);
    }

    public Task<bool> HasValidAttendanceAsync(int id, CancellationToken ct = default) =>
        ParticipationFacts.ValidAttendances(GetDbSet<Attendance>().AsNoTracking()).AnyAsync(a => a.RegistrationId == id, ct);

    public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken ct = default)
    {
        var validAttendance = ParticipationFacts.ValidAttendances(GetDbSet<Attendance>().AsNoTracking());
        return (from registration in GetDbSet<Registration>().AsNoTracking()
                join attendance in validAttendance on registration.RegistrationId equals attendance.RegistrationId
                where registration.EventId == eventId
                select attendance.AttendanceId).AnyAsync(ct);
    }

    public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken ct = default) =>
        (from registration in GetDbSet<Registration>().AsNoTracking()
         join certificate in GetDbSet<Certificate>().AsNoTracking() on registration.RegistrationId equals certificate.RegistrationId
         where registration.EventId == eventId && certificate.CertificateStatusId == CertificateStatusCode.Issued
         select certificate.CertificateId).AnyAsync(ct);

    private IQueryable<RegistrationReviewRow> ReviewQueue(RegistrationStatusCode status, int? eventId)
    {
        var rows = from r in GetDbSet<Registration>().AsNoTracking()
                   join m in GetDbSet<Member>().AsNoTracking() on r.MemberId equals m.MemberId
                   join u in GetDbSet<User>().AsNoTracking() on m.UserId equals u.Id
                   join e in GetDbSet<Event>().AsNoTracking() on r.EventId equals e.EventId
                   where r.RegistrationStatusId == status && (!eventId.HasValue || r.EventId == eventId.Value)
                   select new { r, m, u, e };
        // The waitlist screen must show the same order the promotion command uses.
        var ordered = status == RegistrationStatusCode.Waitlisted
            ? rows.OrderBy(row => row.r.ReviewedAt).ThenBy(row => row.r.RegistrationId)
            : rows.OrderBy(row => row.r.SubmittedAt).ThenBy(row => row.r.RegistrationId);
        return ordered.Select(row => new RegistrationReviewRow(row.r.RegistrationId, row.e.EventId, row.e.Title, row.u.Name, row.m.StudentNumber, row.r.SubmittedAt, row.r.RegistrationStatusId)
        {
            Course = row.r.CourseSnapshot,
            YearLevel = row.r.YearLevelSnapshot
        });
    }

    public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken ct = default) => ReviewQueue(status, eventId).CountAsync(ct);
    public async Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken ct = default) =>
        await ReviewQueue(status, eventId).Skip(offset).Take(limit).ToListAsync(ct);

    private static bool Open(RegistrationStatusCode status) => status is RegistrationStatusCode.Pending or RegistrationStatusCode.Confirmed or RegistrationStatusCode.Waitlisted;

    public async Task<Registration> GetForCancellationAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetDbSet<Registration>().AsNoTracking().SingleOrDefaultAsync(r => r.RegistrationId == registrationId, cancellationToken);
        if (snapshot == null) return null;
        var tracked = GetDbSet<Registration>().Local.FirstOrDefault(r => r.RegistrationId == registrationId);
        if (tracked == null) { GetDbSet<Registration>().Attach(snapshot); return snapshot; }
        var entry = Context.Entry(tracked);
        Context.ChangeTracker.DetectChanges();
        var fresh = Context.Entry(snapshot).CurrentValues;
        foreach (var property in entry.Properties)
        {
            var value = fresh[property.Metadata.Name];
            if (!property.IsModified) property.CurrentValue = value;
            property.OriginalValue = value;
        }
        // The persisted status is authoritative for idempotence, even if a caller tracked it earlier.
        entry.Property(r => r.RegistrationStatusId).CurrentValue = snapshot.RegistrationStatusId;
        entry.Property(r => r.RegistrationStatusId).IsModified = false;
        return tracked;
    }

    public async Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int eventId, CancellationToken cancellationToken = default)
    {
        var ids = await GetDbSet<Registration>().AsNoTracking().Where(r => r.EventId == eventId && r.RegistrationStatusId != RegistrationStatusCode.Cancelled).OrderBy(r => r.RegistrationId).Select(r => r.RegistrationId).ToListAsync(cancellationToken);
        var result = new List<Registration>();
        foreach (var id in ids) { var row = await GetForCancellationAsync(id, cancellationToken); if (row != null && Open(row.RegistrationStatusId)) result.Add(row); }
        return result;
    }

    public async Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await (from r in GetDbSet<Registration>().AsNoTracking()
               join m in GetDbSet<Member>().AsNoTracking() on r.MemberId equals m.MemberId
               where m.UserId == userId && r.RegistrationStatusId != RegistrationStatusCode.Cancelled
               select r.EventId).Distinct().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int userId, DateTime nowUtc, int? eventId = null, CancellationToken cancellationToken = default)
    {
        var validAttendance = ParticipationFacts.ValidAttendances(GetDbSet<Attendance>().AsNoTracking());
        var rows = await (from r in GetDbSet<Registration>().AsNoTracking()
                          join m in GetDbSet<Member>().AsNoTracking() on r.MemberId equals m.MemberId
                          join e in GetDbSet<Event>().AsNoTracking() on r.EventId equals e.EventId
                          where m.UserId == userId && (!eventId.HasValue || e.EventId == eventId.Value)
                            && (r.RegistrationStatusId == RegistrationStatusCode.Pending || r.RegistrationStatusId == RegistrationStatusCode.Confirmed || r.RegistrationStatusId == RegistrationStatusCode.Waitlisted)
                            && !validAttendance.Any(a => a.RegistrationId == r.RegistrationId)
                          select new UpcomingRegistrationRow(r.RegistrationId, e.EventId, e.Title, e.StartsAt)).ToListAsync(cancellationToken);
        // Compare in memory: SQL datetime2(3) parameter coercion must not round away a tick.
        var cutoff = ParticipationFacts.StartCutoffPassed(nowUtc).Compile();
        return rows.Where(r => !cutoff(new Event { StartsAt = r.StartsAt })).OrderBy(r => r.StartsAt).ThenBy(r => r.EventId).ThenBy(r => r.RegistrationId).ToList();
    }
}
