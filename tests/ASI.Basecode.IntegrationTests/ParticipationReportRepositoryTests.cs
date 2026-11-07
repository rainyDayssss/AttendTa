using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Focused SQL smoke for the report's no-show and signup-snapshot distribution reads: Confirmed
/// registrations without valid attendance are no-shows only on a completed event, and the
/// distribution groups the signup snapshots even when the member is now inactive.
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

    private AsiBasecodeDBContext Context() => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    private async Task<Member> SeedMemberAsync(
        string email,
        bool isActive,
        MembershipStatusCode status,
        int actorId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var context = Context();
        var user = new User
        {
            UserId = email,
            Name = "Report Member",
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
