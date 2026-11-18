using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Focused smoke over real SQL for the 4.5 manual lookup and manual check-in: the server-paged
/// confirmed search, its read-only guarantee, the <c>manual_search</c>/<c>student_number</c> method
/// choice, the repeat, every refusal and the Management-only guard. The search pages in SQL, so the
/// tests seed 30 confirmed rows and assert the page boundaries.
/// </summary>
public sealed class CheckInLookupFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    private const string NamePrefix = "Lookup Member";

    [Fact]
    public async Task SearchMatchesNameAndStudentNumberWithinThisEventsConfirmedRowsOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Officer);

        var byName = await lookup.SearchAsync(setup.EventId, "Member 03", null, ct);
        Assert.Equal(1, byName.TotalCount);
        var row = Assert.Single(byName.Rows);
        Assert.Equal(setup.RegistrationIds[3], row.RegistrationId);
        Assert.Equal($"{NamePrefix} 03", row.MemberName);
        Assert.False(row.IsCheckedIn);
        Assert.True(row.CanCheckIn);

        var byNumber = await lookup.SearchAsync(setup.EventId, setup.StudentNumbers[7], null, ct);
        Assert.Equal(1, byNumber.TotalCount);
        Assert.Equal(setup.RegistrationIds[7], Assert.Single(byNumber.Rows).RegistrationId);

        // Case-insensitive through the database collation, and a fragment matches many rows.
        var caseInsensitive = await lookup.SearchAsync(setup.EventId, "lookup member 2", null, ct);
        Assert.Equal(10, caseInsensitive.TotalCount);

        // Another event's confirmed row and this event's cancelled row never match.
        Assert.Equal(0, (await lookup.SearchAsync(setup.EventId, "Other Event Member", null, ct)).TotalCount);
        Assert.Equal(0, (await lookup.SearchAsync(setup.EventId, setup.CancelledStudentNumber, null, ct)).TotalCount);
        Assert.Equal(0, (await lookup.SearchAsync(setup.EventId, setup.OtherEventStudentNumber, null, ct)).TotalCount);
    }

    [Fact]
    public async Task BlankAndNoMatchQueriesReturnTheEmptyState()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Officer);

        foreach (var query in new[] { null, "", "   ", "no such participant" })
        {
            var result = await lookup.SearchAsync(setup.EventId, query, null, ct);
            Assert.Equal(0, result.TotalCount);
            Assert.Empty(result.Rows);
            Assert.Equal(1, result.PageNumber);
        }
    }

    [Fact]
    public async Task SearchPagesAtFiveAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Officer);
        var attendanceBefore = await AttendanceCountAsync();
        var auditBefore = await AuditCountAsync();

        var first = await lookup.SearchAsync(setup.EventId, NamePrefix, 1, ct);
        Assert.Equal(30, first.TotalCount);
        Assert.Equal(5, first.Rows.Count);
        Assert.Equal(6, first.TotalPages);
        Assert.Equal(1, first.PageNumber);
        Assert.True(first.HasNextPage);
        Assert.Equal(setup.RegistrationIds[0], first.Rows[0].RegistrationId);
        Assert.Equal(setup.RegistrationIds[4], first.Rows[4].RegistrationId);

        var second = await lookup.SearchAsync(setup.EventId, NamePrefix, 2, ct);
        Assert.Equal(30, second.TotalCount);
        Assert.Equal(5, second.Rows.Count);
        Assert.Equal(6, second.TotalPages);
        Assert.Equal(2, second.PageNumber);
        Assert.True(second.HasNextPage);
        Assert.Equal(setup.RegistrationIds[5], second.Rows[0].RegistrationId);
        Assert.Equal(setup.RegistrationIds[9], second.Rows[4].RegistrationId);

        // A page past the end resolves to the last page; an invalid page resolves to page one.
        Assert.Equal(6, (await lookup.SearchAsync(setup.EventId, NamePrefix, 99, ct)).PageNumber);
        Assert.Equal(1, (await lookup.SearchAsync(setup.EventId, NamePrefix, 0, ct)).PageNumber);

        Assert.Equal(attendanceBefore, await AttendanceCountAsync());
        Assert.Equal(auditBefore, await AuditCountAsync());
    }

    [Fact]
    public async Task SearchShowsCheckedInAndInvalidatedStateFromRealAttendance()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            writer.AttendanceRecords.Add(new Attendance
            {
                RegistrationId = setup.RegistrationIds[0],
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = setup.OfficerId,
                RecordedAt = Now.AddMinutes(-10)
            });
            writer.AttendanceRecords.Add(new Attendance
            {
                RegistrationId = setup.RegistrationIds[1],
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = setup.OfficerId,
                RecordedAt = Now.AddMinutes(-9),
                InvalidatedBy = setup.OfficerId,
                InvalidatedAt = Now.AddMinutes(-5),
                InvalidationReason = "Correction."
            });
            await writer.SaveChangesAsync(ct);
        }

        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Officer);

        var result = await lookup.SearchAsync(setup.EventId, NamePrefix, 1, ct);

        Assert.True(result.Rows[0].IsCheckedIn);
        Assert.False(result.Rows[0].IsInvalidated);
        Assert.False(result.Rows[0].CanCheckIn);
        Assert.False(result.Rows[1].IsCheckedIn);
        Assert.True(result.Rows[1].IsInvalidated);
        Assert.False(result.Rows[1].CanCheckIn);
        Assert.True(result.Rows[2].CanCheckIn);
    }

    [Fact]
    public async Task ManualSearchCheckInWritesOneAttendanceAndOneAuditRowAndRepeatKeepsTheOriginalTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Officer);
        var registrationId = setup.RegistrationIds[4];

        var outcome = await lookup.CheckInAsync(setup.EventId, registrationId, "Member 04", ct);

        Assert.Equal(CheckInOutcomeViewModel.Success, outcome.Outcome);
        Assert.Equal(registrationId, outcome.RegistrationId);
        Assert.Equal($"{NamePrefix} 04", outcome.MemberName);
        Assert.Equal(setup.StudentNumbers[4], outcome.StudentNumber);
        Assert.Equal(Now, outcome.RecordedAt);

        int attendanceId;
        await using (var observer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var attendance = await observer.AttendanceRecords.AsNoTracking()
                .SingleAsync(row => row.RegistrationId == registrationId, ct);
            attendanceId = attendance.AttendanceId;
            Assert.Equal(CheckInMethodCode.ManualSearch, attendance.CheckInMethodId);
            Assert.Equal(setup.OfficerId, attendance.RecordedBy);
            Assert.Equal(Now, attendance.RecordedAt);
            Assert.Null(attendance.InvalidatedAt);

            var log = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(row => row.EntityType == "attendance" && row.EntityId == attendanceId, ct);
            Assert.Equal(AuditActions.AttendanceRecorded, log.Action);
            Assert.Equal(setup.OfficerId, log.ActorUserId);
            Assert.Contains($"\"RegistrationId\":{registrationId}", log.NewValues!, StringComparison.Ordinal);
        }

        var repeat = await lookup.CheckInAsync(setup.EventId, registrationId, "Member 04", ct);

        Assert.Equal(CheckInOutcomeViewModel.Repeat, repeat.Outcome);
        Assert.Equal(Now, repeat.RecordedAt);
        Assert.Equal(setup.StudentNumbers[4], repeat.StudentNumber);

        await using var final = EventDraftTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(1, await final.AttendanceRecords.AsNoTracking()
            .CountAsync(row => row.RegistrationId == registrationId, ct));
        Assert.Equal(1, await final.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "attendance" && row.EntityId == attendanceId, ct));
    }

    [Fact]
    public async Task StudentNumberQueryRecordsTheStudentNumberMethod()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Officer);
        var registrationId = setup.RegistrationIds[2];

        var outcome = await lookup.CheckInAsync(setup.EventId, registrationId, $"  {setup.StudentNumbers[2]}  ", ct);

        Assert.Equal(CheckInOutcomeViewModel.Success, outcome.Outcome);
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var attendance = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(row => row.RegistrationId == registrationId, ct);
        Assert.Equal(CheckInMethodCode.StudentNumber, attendance.CheckInMethodId);
    }

    [Fact]
    public async Task ClosedWindowAndUnpublishedEventsAreClosedAndWriteNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var closed = await SeedEventAsync(open: false);
        var draft = await SeedEventAsync(status: EventStatusCode.Draft);
        var completed = await SeedEventAsync(status: EventStatusCode.Completed);
        var cancelled = await SeedEventAsync(status: EventStatusCode.Cancelled);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, closed.OfficerId, RoleCodes.Officer);
        var attendanceBefore = await AttendanceCountAsync();
        var auditBefore = await AuditCountAsync();

        foreach (var setup in new[] { closed, draft, completed, cancelled })
        {
            var outcome = await lookup.CheckInAsync(setup.EventId, setup.RegistrationIds[0], NamePrefix, ct);
            Assert.Equal(CheckInOutcomeViewModel.Closed, outcome.Outcome);
            AssertNoIdentity(outcome);
        }

        var closedOutcome = await lookup.CheckInAsync(closed.EventId, closed.RegistrationIds[0], NamePrefix, ct);
        Assert.Equal("Check-in is closed for this event. Open it before checking in.", closedOutcome.Message);

        Assert.Equal(attendanceBefore, await AttendanceCountAsync());
        Assert.Equal(auditBefore, await AuditCountAsync());
    }

    [Fact]
    public async Task RefusedRegistrationsAreInvalidAndWriteNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        var ineligible = await SeedEventAsync(membershipStatus: MembershipStatusCode.Inactive);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Officer);
        var attendanceBefore = await AttendanceCountAsync();
        var auditBefore = await AuditCountAsync();

        // Cancelled in this event, confirmed in another event, and unknown ids are all refused.
        var cancelled = await lookup.CheckInAsync(setup.EventId, setup.CancelledRegistrationId, NamePrefix, ct);
        var otherEvent = await lookup.CheckInAsync(setup.EventId, setup.OtherEventRegistrationId, NamePrefix, ct);
        var unknown = await lookup.CheckInAsync(setup.EventId, 999_999, NamePrefix, ct);
        var inactiveMembership = await lookup.CheckInAsync(
            ineligible.EventId, ineligible.RegistrationIds[0], NamePrefix, ct);

        Assert.Equal(CheckInOutcomeViewModel.Invalid, cancelled.Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, otherEvent.Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, unknown.Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, inactiveMembership.Outcome);
        AssertNoIdentity(cancelled);
        AssertNoIdentity(otherEvent);
        AssertNoIdentity(unknown);
        AssertNoIdentity(inactiveMembership);
        Assert.Equal(attendanceBefore, await AttendanceCountAsync());
        Assert.Equal(auditBefore, await AuditCountAsync());
    }

    [Fact]
    public async Task InvalidatedAttendanceIsInvalidAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        var registrationId = setup.RegistrationIds[1];
        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            writer.AttendanceRecords.Add(new Attendance
            {
                RegistrationId = registrationId,
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = setup.OfficerId,
                RecordedAt = Now.AddMinutes(-5),
                InvalidatedBy = setup.OfficerId,
                InvalidatedAt = Now.AddMinutes(-1),
                InvalidationReason = "Correction."
            });
            await writer.SaveChangesAsync(ct);
        }

        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Officer);
        var auditBefore = await AuditCountAsync();

        var outcome = await lookup.CheckInAsync(setup.EventId, registrationId, "Member 01", ct);

        Assert.Equal(CheckInOutcomeViewModel.Invalid, outcome.Outcome);
        AssertNoIdentity(outcome);
        await using var final = EventDraftTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(1, await final.AttendanceRecords.AsNoTracking()
            .CountAsync(row => row.RegistrationId == registrationId, ct));
        Assert.Equal(auditBefore, await AuditCountAsync());
    }

    [Fact]
    public async Task NonManagementIsForbiddenBeforeAnyWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedEventAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var lookup = Lookup(context, setup.OfficerId, RoleCodes.Member);
        var attendanceBefore = await AttendanceCountAsync();
        var auditBefore = await AuditCountAsync();

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => lookup.CheckInAsync(setup.EventId, setup.RegistrationIds[0], NamePrefix, ct));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
        Assert.Equal(attendanceBefore, await AttendanceCountAsync());
        Assert.Equal(auditBefore, await AuditCountAsync());
    }

    private static CheckInLookupService Lookup(AsiBasecodeDBContext context, int actorId, string role)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorId, role);
        var clock = new FixedClock(Now);
        var events = new EventRepository(unitOfWork);
        var registrations = new RegistrationRepository(unitOfWork);
        var engine = new AttendanceRecordingService(
            registrations,
            new UserRepository(unitOfWork),
            new MemberRepository(unitOfWork),
            events,
            new AttendanceRepository(unitOfWork),
            unitOfWork,
            actor,
            new ActivityLogWriter(context, actor, clock),
            clock);
        return new CheckInLookupService(events, registrations, engine);
    }

    /// <summary>
    /// Seeds one event with 30 confirmed participants (<c>Lookup Member 00</c> to <c>Lookup Member 29</c>),
    /// one cancelled registration, and a second event with one confirmed registration, so the
    /// event-scoping and status filters have something to exclude.
    /// </summary>
    private async Task<LookupSetup> SeedEventAsync(
        EventStatusCode status = EventStatusCode.Published,
        bool open = true,
        MembershipStatusCode membershipStatus = MembershipStatusCode.Active)
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, status,
            Now.AddHours(1), Now.AddMinutes(-30), 50, null, $"Lookup flow {Guid.NewGuid():N}", ct);

        if (open)
        {
            await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
            var row = await writer.Events.SingleAsync(item => item.EventId == eventItem.EventId, ct);
            row.IsCheckInOpen = true;
            row.CheckInOpenedBy = officerId;
            row.CheckInOpenedAt = Now;
            await writer.SaveChangesAsync(ct);
        }

        var registrationIds = new List<int>();
        var studentNumbers = new List<string>();
        for (var index = 0; index < 30; index++)
        {
            var member = await SeedMemberAsync(officerId, $"Lookup Member {index:D2}", membershipStatus, ct);
            var registration = await MemberEventTestSupport.SeedRegistrationAsync(
                fixture.ConnectionString, eventItem.EventId, member.MemberId,
                RegistrationStatusCode.Confirmed, officerId, ct);
            registrationIds.Add(registration.RegistrationId);
            studentNumbers.Add(member.StudentNumber);
        }

        var cancelledMember = await SeedMemberAsync(officerId, "Lookup Cancelled Member", membershipStatus, ct);
        var cancelled = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, cancelledMember.MemberId,
            RegistrationStatusCode.Cancelled, officerId, ct);

        var otherEvent = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(2), Now.AddMinutes(-30), 20, null, $"Lookup flow other {Guid.NewGuid():N}", ct);
        var otherMember = await SeedMemberAsync(officerId, "Other Event Member", membershipStatus, ct);
        var otherRegistration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, otherEvent.EventId, otherMember.MemberId,
            RegistrationStatusCode.Confirmed, officerId, ct);

        return new LookupSetup(
            officerId,
            eventItem.EventId,
            registrationIds,
            studentNumbers,
            cancelled.RegistrationId,
            cancelledMember.StudentNumber,
            otherRegistration.RegistrationId,
            otherMember.StudentNumber);
    }

    private async Task<Member> SeedMemberAsync(
        int officerId,
        string name,
        MembershipStatusCode status,
        CancellationToken cancellationToken)
    {
        var user = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"lookup-{Guid.NewGuid():N}@attendta.test",
            MemberEventTestSupport.NewPassword(), RoleCode.Member, cancellationToken);
        var member = await MemberEventTestSupport.SeedMemberAsync(
            fixture.ConnectionString, user.Id, status, cancellationToken);

        await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var row = await writer.Users.SingleAsync(item => item.Id == user.Id, cancellationToken);
        row.Name = name;
        await writer.SaveChangesAsync(cancellationToken);
        return member;
    }

    private async Task<int> AttendanceCountAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        return await context.AttendanceRecords.AsNoTracking().CountAsync(ct);
    }

    private async Task<int> AuditCountAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        return await context.ActivityLogs.AsNoTracking().CountAsync(ct);
    }

    private static void AssertNoIdentity(CheckInOutcomeViewModel outcome)
    {
        Assert.Null(outcome.MemberName);
        Assert.Null(outcome.StudentNumber);
        Assert.Null(outcome.RecordedAt);
    }

    private sealed record LookupSetup(
        int OfficerId,
        int EventId,
        IReadOnlyList<int> RegistrationIds,
        IReadOnlyList<string> StudentNumbers,
        int CancelledRegistrationId,
        string CancelledStudentNumber,
        int OtherEventRegistrationId,
        string OtherEventStudentNumber);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }

    private sealed class Actor(int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => role;
    }
}
