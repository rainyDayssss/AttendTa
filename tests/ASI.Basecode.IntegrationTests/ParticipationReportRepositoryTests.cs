using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Focused SQL smoke for the report's reads: Confirmed registrations without valid attendance are
/// no-shows only on a completed event, the distribution groups the signup snapshots even when the
/// member is now inactive, and the detail rows page by participant name then registration id with
/// stable boundaries.
/// </summary>
public sealed class ParticipationReportRepositoryTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task CompletedEventCountsNoShowsAndGroupsSnapshotsIncludingInactiveHistory()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        var suffix = Guid.NewGuid().ToString("N");

        await using var context = Context();
        var actor = new User
        {
            UserId = $"report-actor-{suffix}@attendta.test",
            Name = "Report Actor",
            Password = "test",
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        context.Users.Add(actor);
        await context.SaveChangesAsync(ct);

        var eventType = new EventType
        {
            TypeName = $"Report type {suffix}",
            Description = "Report test type",
            IsActive = true,
            CreatedBy = actor.Id,
            CreatedAt = now
        };
        var venue = new Venue
        {
            RoomName = $"Report room {suffix}",
            Capacity = 50,
            ImagePath = "uploads/venues/test.jpg",
            IsActive = true,
            CreatedBy = actor.Id,
            CreatedAt = now
        };
        context.EventTypes.Add(eventType);
        context.Venues.Add(venue);
        await context.SaveChangesAsync(ct);

        var completed = EventItem(completed: true, now, eventType.TypeId, venue.VenueId, actor.Id, suffix);
        var cancelled = EventItem(completed: false, now, eventType.TypeId, venue.VenueId, actor.Id, suffix);
        context.Events.AddRange(completed, cancelled);
        await context.SaveChangesAsync(ct);

        // One member per registration: the schema allows one registration per member/event.
        var activeMembers = new List<Member>();
        for (var index = 1; index <= 6; index++)
        {
            activeMembers.Add(await SeedMemberAsync($"report-active-{index}-{suffix}@attendta.test", isActive: true, MembershipStatusCode.Active, actor.Id, now, ct));
        }

        var inactiveMember = await SeedMemberAsync($"report-inactive-{suffix}@attendta.test", isActive: false, MembershipStatusCode.Inactive, actor.Id, now, ct);

        // Completed event: one valid attendee, one invalidated check-in, one never checked in, plus
        // pending, waitlisted and cancelled rows that must never count as no-shows.
        var attended = Registration(completed.EventId, activeMembers[0].MemberId, RegistrationStatusCode.Confirmed, "BSIT", 1, actor.Id, now);
        var invalidated = Registration(completed.EventId, activeMembers[1].MemberId, RegistrationStatusCode.Confirmed, "BSED", 2, actor.Id, now);
        var neverCheckedIn = Registration(completed.EventId, activeMembers[2].MemberId, RegistrationStatusCode.Confirmed, "BSIT", 3, actor.Id, now);
        var pending = Registration(completed.EventId, activeMembers[3].MemberId, RegistrationStatusCode.Pending, "BSIT", 1, actor.Id, now);
        var waitlisted = Registration(completed.EventId, activeMembers[4].MemberId, RegistrationStatusCode.Waitlisted, "BSED", 2, actor.Id, now);
        var cancelledRow = Registration(completed.EventId, activeMembers[5].MemberId, RegistrationStatusCode.Cancelled, "BSIT", 4, actor.Id, now);
        // The inactive member still attended, so the snapshot distribution must keep them.
        var inactiveAttended = Registration(completed.EventId, inactiveMember.MemberId, RegistrationStatusCode.Confirmed, "BSED", 2, actor.Id, now);
        context.Registrations.AddRange(attended, invalidated, neverCheckedIn, pending, waitlisted, cancelledRow, inactiveAttended);
        await context.SaveChangesAsync(ct);

        context.AttendanceRecords.AddRange(
            Attendance(attended.RegistrationId, actor.Id, now),
            Attendance(invalidated.RegistrationId, actor.Id, now, invalidatedAt: now),
            Attendance(inactiveAttended.RegistrationId, actor.Id, now));
        await context.SaveChangesAsync(ct);

        // Cancelled event: a confirmed row with and without valid attendance, still zero no-shows.
        var cancelledAttended = Registration(cancelled.EventId, activeMembers[0].MemberId, RegistrationStatusCode.Confirmed, "BSIT", 1, actor.Id, now);
        var cancelledNoShow = Registration(cancelled.EventId, inactiveMember.MemberId, RegistrationStatusCode.Confirmed, "BSED", 2, actor.Id, now);
        context.Registrations.AddRange(cancelledAttended, cancelledNoShow);
        await context.SaveChangesAsync(ct);
        context.AttendanceRecords.Add(Attendance(cancelledAttended.RegistrationId, actor.Id, now));
        await context.SaveChangesAsync(ct);

        var repository = new ParticipationReportRepository(new UnitOfWork(context));

        Assert.Equal(2, await repository.GetNoShowCountAsync(completed.EventId, ct));
        Assert.Equal(0, await repository.GetNoShowCountAsync(cancelled.EventId, ct));

        var distribution = await repository.GetAttendeeDistributionAsync(completed.EventId, ct);
        Assert.Equal(
            new[] { ("BSED", 1), ("BSIT", 1) },
            distribution.Courses.OrderBy(row => row.Course).Select(row => (row.Course, row.Count)));
        Assert.Equal(
            new[] { (1, 1), (2, 1) },
            distribution.Years.OrderBy(row => row.YearLevel).Select(row => (row.YearLevel, row.Count)));

        // The inactive member's valid attendance is the only BSED/year-2 attendee, so it still counts.
        Assert.Contains(distribution.Courses, row => row.Course == "BSED" && row.Count == 1);
        Assert.Contains(distribution.Years, row => row.YearLevel == 2 && row.Count == 1);
    }

    [Fact]
    public async Task DetailRowsPageByNameThenRegistrationIdWithStableBoundaries()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var suffix = Guid.NewGuid().ToString("N");

        await using var context = Context();
        var actor = new User
        {
            UserId = $"report-page-actor-{suffix}@attendta.test",
            Name = "Report Actor",
            Password = "test",
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        context.Users.Add(actor);
        await context.SaveChangesAsync(ct);

        var eventType = new EventType
        {
            TypeName = $"Report page type {suffix}",
            Description = "Report test type",
            IsActive = true,
            CreatedBy = actor.Id,
            CreatedAt = now
        };
        var venue = new Venue
        {
            RoomName = $"Report page room {suffix}",
            Capacity = 50,
            ImagePath = "uploads/venues/test.jpg",
            IsActive = true,
            CreatedBy = actor.Id,
            CreatedAt = now
        };
        context.EventTypes.Add(eventType);
        context.Venues.Add(venue);
        await context.SaveChangesAsync(ct);

        var completed = new Event
        {
            Title = $"Report paged {suffix}",
            Description = "Report test event",
            TypeId = eventType.TypeId,
            VenueId = venue.VenueId,
            StartsAt = now,
            RegistrationDeadline = now.AddHours(-1),
            ParticipantLimit = 40,
            EventStatusId = EventStatusCode.Completed,
            ImagePath = "uploads/events/11111111111111111111111111111111.jpg",
            CreatedBy = actor.Id,
            CreatedAt = now
        };
        context.Events.Add(completed);
        await context.SaveChangesAsync(ct);

        // Names descend as registration ids ascend, so a page ordered by name is not ordered by id.
        // The two lowest ids share the largest name, so the tie breaks on registration_id at the
        // boundary between page 1 and page 2.
        var members = new List<Member>();
        for (var index = 1; index <= 26; index++)
        {
            var name = index <= 2 ? "Participant 26" : $"Participant {27 - index:D2}";
            members.Add(await SeedMemberAsync(
                $"report-page-{index}-{suffix}@attendta.test",
                isActive: true,
                MembershipStatusCode.Active,
                actor.Id,
                now,
                ct,
                name));
        }

        var registrations = new List<Registration>();
        for (var index = 0; index < members.Count; index++)
        {
            registrations.Add(Registration(
                completed.EventId,
                members[index].MemberId,
                RegistrationStatusCode.Confirmed,
                index == 1 ? "BSED" : "BSIT",
                1 + (index % 4),
                actor.Id,
                now));
        }

        context.Registrations.AddRange(registrations);
        await context.SaveChangesAsync(ct);

        // One valid check-in and one invalidated check-in inside the ordered set; the rest are absent.
        context.AttendanceRecords.AddRange(
            Attendance(registrations[2].RegistrationId, actor.Id, now),
            Attendance(registrations[1].RegistrationId, actor.Id, now, invalidatedAt: now));
        await context.SaveChangesAsync(ct);

        var repository = new ParticipationReportRepository(new UnitOfWork(context));

        Assert.Equal(26, await repository.CountDetailRowsAsync(completed.EventId, ct));

        var first = await repository.GetDetailPageAsync(completed.EventId, 0, 25, ct);
        var second = await repository.GetDetailPageAsync(completed.EventId, 25, 25, ct);

        Assert.Equal(25, first.Count);
        var lastRow = Assert.Single(second);

        // Names ascend across the boundary; the shared name breaks on the lower id first.
        Assert.Equal("Participant 01", first[0].MemberName);
        Assert.Equal("Participant 24", first[23].MemberName);
        Assert.Equal("Participant 26", first[24].MemberName);
        Assert.Equal(registrations[0].RegistrationId, first[24].RegistrationId);
        Assert.Equal("Participant 26", lastRow.MemberName);
        Assert.Equal(registrations[1].RegistrationId, lastRow.RegistrationId);
        Assert.True(lastRow.RegistrationId > first[24].RegistrationId);

        // No row is repeated or skipped.
        var paged = first.Concat(second).ToList();
        Assert.Equal(26, paged.Count);
        Assert.Equal(26, paged.Select(row => row.RegistrationId).Distinct().Count());

        // The snapshot course and the linked member's student number come back on the row.
        Assert.Equal("BSED", lastRow.Course);
        Assert.Equal(members[1].StudentNumber, lastRow.StudentNumber);
        Assert.Equal(2, lastRow.YearLevel);

        // The invalidated row is flagged, a valid row is not, and an absent row has no check-in time.
        var invalidatedRow = paged.Single(row => row.RegistrationId == registrations[1].RegistrationId);
        Assert.True(invalidatedRow.IsAttendanceInvalidated);
        Assert.NotNull(invalidatedRow.CheckedInAt);
        var validRow = paged.Single(row => row.RegistrationId == registrations[2].RegistrationId);
        Assert.False(validRow.IsAttendanceInvalidated);
        Assert.NotNull(validRow.CheckedInAt);
        var absentRow = paged.Single(row => row.RegistrationId == registrations[5].RegistrationId);
        Assert.Null(absentRow.CheckedInAt);
        Assert.False(absentRow.IsAttendanceInvalidated);
    }

    private AsiBasecodeDBContext Context() => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    private async Task<Member> SeedMemberAsync(
        string email,
        bool isActive,
        MembershipStatusCode status,
        int actorId,
        DateTime now,
        CancellationToken cancellationToken,
        string name = "Report Member")
    {
        await using var context = Context();
        var user = new User
        {
            UserId = email,
            Name = name,
            Password = "test",
            RoleId = RoleCode.Member,
            IsActive = isActive
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        var member = new Member
        {
            UserId = user.Id,
            StudentNumber = $"RPT-{Guid.NewGuid():N}",
            Course = "BSIT",
            YearLevel = 1,
            Section = "A",
            MembershipStatusId = status,
            CreatedBy = actorId,
            CreatedAt = now
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(cancellationToken);
        return member;
    }

    private static Event EventItem(bool completed, DateTime now, int typeId, int venueId, int actorId, string suffix) => new()
    {
        Title = $"Report {(completed ? "completed" : "cancelled")} {suffix}",
        Description = "Report test event",
        TypeId = typeId,
        VenueId = venueId,
        StartsAt = now,
        RegistrationDeadline = now.AddHours(-1),
        ParticipantLimit = 20,
        EventStatusId = completed ? EventStatusCode.Completed : EventStatusCode.Cancelled,
        ImagePath = "uploads/events/11111111111111111111111111111111.jpg",
        CreatedBy = actorId,
        CreatedAt = now
    };

    private static Registration Registration(
        int eventId,
        int memberId,
        RegistrationStatusCode status,
        string courseSnapshot,
        int yearLevelSnapshot,
        int actorId,
        DateTime now) => new()
    {
        EventId = eventId,
        MemberId = memberId,
        RegistrationStatusId = status,
        CourseSnapshot = courseSnapshot,
        YearLevelSnapshot = yearLevelSnapshot,
        SectionSnapshot = "A",
        SubmittedAt = now,
        CreatedBy = actorId,
        CreatedAt = now
    };

    private static Attendance Attendance(int registrationId, int actorId, DateTime now, DateTime? invalidatedAt = null) => new()
    {
        RegistrationId = registrationId,
        CheckInMethodId = CheckInMethodCode.QrCode,
        RecordedBy = actorId,
        RecordedAt = now,
        InvalidatedBy = invalidatedAt is null ? null : actorId,
        InvalidatedAt = invalidatedAt,
        InvalidationReason = invalidatedAt is null ? null : "Test invalidation"
    };
}
