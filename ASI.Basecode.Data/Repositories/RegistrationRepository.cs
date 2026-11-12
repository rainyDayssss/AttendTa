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
        ParticipationFacts.SeatHolders(GetDbSet<Registration>())
            .AnyAsync(registration => registration.RegistrationId == registrationId, cancellationToken);

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

    public async Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken cancellationToken = default) =>
        await (from registration in ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking())
               join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
               join user in GetDbSet<User>().AsNoTracking() on member.UserId equals user.Id
               join eventItem in GetDbSet<Event>().AsNoTracking() on registration.EventId equals eventItem.EventId
               join eventType in GetDbSet<EventType>().AsNoTracking() on eventItem.TypeId equals eventType.TypeId
               join venue in GetDbSet<Venue>().AsNoTracking() on eventItem.VenueId equals venue.VenueId
               where member.UserId == userId
                   && eventItem.EventStatusId == EventStatusCode.Published
                   && registration.QrToken != null
               orderby eventItem.StartsAt, registration.RegistrationId
               select new MyPassRow(
                   registration.RegistrationId,
                   eventItem.EventId,
                   eventItem.Title,
                   eventType.TypeName,
                   venue.RoomName,
                   eventItem.StartsAt,
                   user.Name,
                   member.StudentNumber,
                   registration.QrToken))
            .ToListAsync(cancellationToken);

    public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken cancellationToken = default) =>
        (from registration in GetDbSet<Registration>().AsNoTracking()
         join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
         join eventItem in GetDbSet<Event>().AsNoTracking() on registration.EventId equals eventItem.EventId
         where registration.RegistrationId == registrationId && member.UserId == userId
         select new OwnedPassRow(registration.RegistrationId, registration.EventId, registration.RegistrationStatusId, eventItem.EventStatusId, registration.QrToken))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult<int?>(null);
        }

        // The canonical token is 64 lowercase hex characters; anything else cannot match a stored row.
        var normalized = token.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || !normalized.All(Uri.IsHexDigit))
        {
            return Task.FromResult<int?>(null);
        }

        return ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking())
            .Where(registration => registration.EventId == eventId && registration.QrToken == normalized)
            .Select(registration => (int?)registration.RegistrationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) =>
        ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking())
            .CountAsync(registration => registration.EventId == eventId, cancellationToken);

    public Task<int> CountConfirmedLookupAsync(int eventId, string query, CancellationToken cancellationToken = default)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Task.FromResult(0);
        }

        return ConfirmedLookup(eventId, trimmed).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(
        int eventId,
        string query,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Array.Empty<ConfirmedLookupRow>();
        }

        return await ConfirmedLookup(eventId, trimmed)
            .OrderBy(row => row.User.Name)
            .ThenBy(row => row.Registration.RegistrationId)
            .Skip(skip)
            .Take(take)
            .Select(row => new ConfirmedLookupRow(
                row.Registration.RegistrationId,
                row.User.Name,
                row.Member.StudentNumber,
                row.Attendance != null,
                row.Attendance != null ? row.Attendance.InvalidatedAt : (DateTime?)null))
            .ToListAsync(cancellationToken);
    }

    public Task<string> GetConfirmedStudentNumberAsync(int eventId, int registrationId, CancellationToken cancellationToken = default) =>
        (from registration in ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking())
         join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
         where registration.RegistrationId == registrationId
             && registration.EventId == eventId
         select member.StudentNumber)
        .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The manual check-in lookup: the event's Confirmed registrations matching a name or student
    /// number substring, left joined to their attendance row. <c>Contains</c> is the house search
    /// pattern and relies on the SQL Server case-insensitive collation. Kept apart from
    /// <see cref="ReviewQueue"/>, which orders by <c>SubmittedAt</c> for the review screens.
    /// </summary>
    private IQueryable<ConfirmedLookupCandidate> ConfirmedLookup(int eventId, string query) =>
        from registration in ParticipationFacts.SeatHolders(GetDbSet<Registration>().AsNoTracking())
        join member in GetDbSet<Member>().AsNoTracking() on registration.MemberId equals member.MemberId
        join user in GetDbSet<User>().AsNoTracking() on member.UserId equals user.Id
        join attendance in GetDbSet<Attendance>().AsNoTracking()
            on registration.RegistrationId equals attendance.RegistrationId into attendances
        from attendance in attendances.DefaultIfEmpty()
        where registration.EventId == eventId
            && (user.Name.Contains(query) || member.StudentNumber.Contains(query))
        select new ConfirmedLookupCandidate { Registration = registration, Member = member, User = user, Attendance = attendance };

    private sealed class ConfirmedLookupCandidate
    {
        public Registration Registration { get; init; }
        public Member Member { get; init; }
        public User User { get; init; }
        public Attendance Attendance { get; init; }
    }

    public Task AddAsync(Registration registration, CancellationToken cancellationToken = default)
    {
        GetDbSet<Registration>().Add(registration);
        return Task.CompletedTask;
    }

    public Task<RegistrationIdentity> DiscoverIdentityAsync(int id, CancellationToken ct = default) =>
        (from r in GetDbSet<Registration>().AsNoTracking()
         join m in GetDbSet<Member>().AsNoTracking() on r.MemberId equals m.MemberId
         where r.RegistrationId == id
         select new RegistrationIdentity(m.UserId, m.MemberId, r.EventId)).SingleOrDefaultAsync(ct);

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

    public async Task<Registration> GetForUpdateAsync(int registrationId, CancellationToken cancellationToken = default)
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
        // The status filter runs in SQL, so every returned row is already open; no in-memory re-filter
        // is needed after the tracked refresh.
        var ids = await GetDbSet<Registration>().AsNoTracking()
            .Where(ParticipationFacts.OpenRegistration)
            .Where(registration => registration.EventId == eventId)
            .OrderBy(registration => registration.RegistrationId)
            .Select(registration => registration.RegistrationId)
            .ToListAsync(cancellationToken);
        var result = new List<Registration>(ids.Count);
        foreach (var id in ids)
        {
            var row = await GetForUpdateAsync(id, cancellationToken);
            if (row != null) result.Add(row);
        }

        return result;
    }

    public async Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await (from r in GetDbSet<Registration>().AsNoTracking().Where(ParticipationFacts.OpenRegistration)
               join m in GetDbSet<Member>().AsNoTracking() on r.MemberId equals m.MemberId
               where m.UserId == userId
               select r.EventId).Distinct().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int userId, DateTime nowUtc, int? eventId = null, CancellationToken cancellationToken = default)
    {
        var validAttendance = ParticipationFacts.ValidAttendances(GetDbSet<Attendance>().AsNoTracking());
        var rows = await (from r in GetDbSet<Registration>().AsNoTracking().Where(ParticipationFacts.OpenRegistration)
                          join m in GetDbSet<Member>().AsNoTracking() on r.MemberId equals m.MemberId
                          join e in GetDbSet<Event>().AsNoTracking() on r.EventId equals e.EventId
                          where m.UserId == userId && (!eventId.HasValue || e.EventId == eventId.Value)
                            && !validAttendance.Any(a => a.RegistrationId == r.RegistrationId)
                          select new UpcomingRegistrationRow(r.RegistrationId, e.EventId, e.Title, e.StartsAt)).ToListAsync(cancellationToken);
        // Compare in memory: SQL datetime2(3) parameter coercion must not round away a tick.
        var cutoff = ParticipationFacts.StartCutoffPassed(nowUtc).Compile();
        return rows.Where(r => !cutoff(new Event { StartsAt = r.StartsAt })).OrderBy(r => r.StartsAt).ThenBy(r => r.EventId).ThenBy(r => r.RegistrationId).ToList();
    }
}
