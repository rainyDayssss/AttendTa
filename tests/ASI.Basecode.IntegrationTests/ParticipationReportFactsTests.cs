using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class ParticipationReportFactsTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task FullDatasetAggregatesExcludeInvalidatedAttendanceCountInactiveHistoryAndReturnNullForZeroConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, actorId, capacity: 40, ct);
        var now = new DateTime(2040, 2, 1, 12, 0, 0, DateTimeKind.Utc);
        var data = await SeedAsync(actorId, typeId, venueId, now, ct);

        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);

        var seatHolderIds = await ParticipationFacts.SeatHolders(context.Registrations)
            .Where(registration => registration.EventId == data.CompletedEventId)
            .Select(registration => registration.RegistrationId)
            .ToArrayAsync(ct);
        Assert.Equal(26, seatHolderIds.Length);

        var validAttendanceIds = await ParticipationFacts.ValidAttendances(context.AttendanceRecords)
            .Select(attendance => attendance.RegistrationId)
            .ToArrayAsync(ct);
        Assert.Equal(13, validAttendanceIds.Intersect(data.ConfirmedRegistrationIds).Count());

        var completedRate = await ParticipationFacts.AttendanceRates(
            context.Events.Where(eventItem => eventItem.EventId == data.CompletedEventId),
            context.Registrations,
            context.AttendanceRecords).SingleAsync(ct);
        Assert.Equal(0.5m, completedRate!.Value);

        var zeroConfirmedRate = await ParticipationFacts.AttendanceRates(
            context.Events.Where(eventItem => eventItem.EventId == data.ZeroConfirmedEventId),
            context.Registrations,
            context.AttendanceRecords).SingleAsync(ct);
        Assert.Null(zeroConfirmedRate);

        var noShowIds = await ParticipationFacts.NoShows(
            context.Registrations,
            context.Events,
            context.AttendanceRecords)
            .Where(registration => registration.EventId == data.CompletedEventId)
            .Select(registration => registration.RegistrationId)
            .ToArrayAsync(ct);
        Assert.Equal(13, noShowIds.Length);
        Assert.All(noShowIds, registrationId => Assert.Contains(registrationId, data.ConfirmedRegistrationIds));
        Assert.DoesNotContain(data.NonConfirmedRegistrationIds, registrationId => noShowIds.Contains(registrationId));

        var eligibleMemberIds = await ParticipationFacts.EligibleParticipantMemberIds(context.Users, context.Members)
            .ToArrayAsync(ct);
        Assert.DoesNotContain(data.InactiveMemberId, eligibleMemberIds);
        Assert.Contains(data.InactiveRegistrationId, seatHolderIds);
        Assert.Contains(data.InactiveRegistrationId, validAttendanceIds);
    }

    private async Task<ReportData> SeedAsync(int actorId, int typeId, int venueId, DateTime now, CancellationToken cancellationToken)
    {
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);

        var users = Enumerable.Range(1, 29).Select(index => new User
        {
            Name = $"Report user {index:D2}",
            UserId = $"report-user-{index:D2}-{Guid.NewGuid():N}@attendta.test",
            Password = "test",
            RoleId = RoleCode.Member,
            IsActive = index != 1
        }).ToArray();
        context.Users.AddRange(users);
        await context.SaveChangesAsync(cancellationToken);

        var members = users.Select((user, index) => new Member
        {
            UserId = user.Id,
            StudentNumber = $"REPORT-{index + 1:D2}-{Guid.NewGuid():N}",
            Course = "BSIT",
            YearLevel = 1,
            Section = "A",
            MembershipStatusId = index == 0 ? MembershipStatusCode.Inactive : MembershipStatusCode.Active,
            CreatedBy = actorId,
            CreatedAt = now
        }).ToArray();
        context.Members.AddRange(members);
        await context.SaveChangesAsync(cancellationToken);

        var completed = ReportEvent("completed", typeId, venueId, actorId, now);
        var zeroConfirmed = ReportEvent("zero-confirmed", typeId, venueId, actorId, now);
        context.Events.AddRange(completed, zeroConfirmed);
        await context.SaveChangesAsync(cancellationToken);

        var confirmed = members.Take(26).Select(member => ReportRegistration(
            member.MemberId, completed.EventId, actorId, now, RegistrationStatusCode.Confirmed)).ToArray();
        context.Registrations.AddRange(confirmed);

        var completedNonConfirmed = new[]
        {
            ReportRegistration(members[26].MemberId, completed.EventId, actorId, now, RegistrationStatusCode.Pending),
            ReportRegistration(members[27].MemberId, completed.EventId, actorId, now, RegistrationStatusCode.Waitlisted),
            ReportRegistration(members[28].MemberId, completed.EventId, actorId, now, RegistrationStatusCode.Cancelled)
        };
        context.Registrations.AddRange(completedNonConfirmed);

        var nonConfirmed = new[]
        {
            ReportRegistration(members[1].MemberId, zeroConfirmed.EventId, actorId, now, RegistrationStatusCode.Pending),
            ReportRegistration(members[2].MemberId, zeroConfirmed.EventId, actorId, now, RegistrationStatusCode.Waitlisted),
            ReportRegistration(members[3].MemberId, zeroConfirmed.EventId, actorId, now, RegistrationStatusCode.Cancelled)
        };
        context.Registrations.AddRange(nonConfirmed);
        await context.SaveChangesAsync(cancellationToken);

        var attendance = new List<Attendance>();
        for (var index = 0; index < 13; index++)
        {
            attendance.Add(ReportAttendance(confirmed[index].RegistrationId, actorId, now));
        }

        for (var index = 13; index < 18; index++)
        {
            attendance.Add(new Attendance
            {
                RegistrationId = confirmed[index].RegistrationId,
                CheckInMethodId = CheckInMethodCode.ManualSearch,
                RecordedBy = actorId,
                RecordedAt = now,
                InvalidatedBy = actorId,
                InvalidatedAt = now,
                InvalidationReason = "mistaken scan"
            });
        }

        context.AttendanceRecords.AddRange(attendance);
        await context.SaveChangesAsync(cancellationToken);

        return new ReportData(
            completed.EventId,
            zeroConfirmed.EventId,
            members[0].MemberId,
            confirmed[0].RegistrationId,
            confirmed.Select(registration => registration.RegistrationId).ToArray(),
            completedNonConfirmed.Concat(nonConfirmed)
                .Select(registration => registration.RegistrationId)
                .ToArray());
    }

    private static Event ReportEvent(string name, int typeId, int venueId, int actorId, DateTime now) => new()
    {
        Title = $"Report {name} {Guid.NewGuid():N}",
        TypeId = typeId,
        VenueId = venueId,
        StartsAt = now,
        RegistrationDeadline = now.AddDays(-1),
        ParticipantLimit = 40,
        EventStatusId = EventStatusCode.Completed,
        ImagePath = "uploads/events/33333333333333333333333333333333.jpg",
        CreatedBy = actorId,
        CreatedAt = now.AddDays(-7)
    };

    private static Registration ReportRegistration(
        int memberId,
        int eventId,
        int actorId,
        DateTime now,
        RegistrationStatusCode status) => new()
    {
        MemberId = memberId,
        EventId = eventId,
        RegistrationStatusId = status,
        CourseSnapshot = "BSIT",
        YearLevelSnapshot = 1,
        SectionSnapshot = "A",
        SubmittedAt = now.AddDays(-2),
        ReviewedAt = status == RegistrationStatusCode.Pending ? null : now.AddDays(-1),
        CreatedBy = actorId,
        CreatedAt = now.AddDays(-2)
    };

    private static Attendance ReportAttendance(int registrationId, int actorId, DateTime now) => new()
    {
        RegistrationId = registrationId,
        CheckInMethodId = CheckInMethodCode.ManualSearch,
        RecordedBy = actorId,
        RecordedAt = now
    };

    private sealed record ReportData(
        int CompletedEventId,
        int ZeroConfirmedEventId,
        int InactiveMemberId,
        int InactiveRegistrationId,
        int[] ConfirmedRegistrationIds,
        int[] NonConfirmedRegistrationIds);
}
