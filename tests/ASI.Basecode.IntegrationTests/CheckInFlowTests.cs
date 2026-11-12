using System.Security.Cryptography;
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
/// Focused smoke over real SQL for the 4.3 check-in engine: the valid and repeat paths, every
/// refusal, the refusal payload, the audit row and the scanner screen read. The parallel duplicate
/// race is proved here on two SQL connections (SM-4: zero duplicate attendance records).
/// </summary>
public sealed class CheckInFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    private const string MemberName = "Member event Member";

    [Fact]
    public async Task ValidScanWritesOneAttendanceAndOneAuditRowAndRepeatKeepsTheOriginalTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedScanAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (scanner, _) = Services(context, setup.OfficerId, RoleCodes.Officer);

        var outcome = await scanner.ScanAsync(setup.EventId, setup.Token, ct);

        Assert.Equal(CheckInOutcomeViewModel.Success, outcome.Outcome);
        Assert.Equal(setup.RegistrationId, outcome.RegistrationId);
        Assert.Equal(MemberName, outcome.MemberName);
        Assert.Equal(setup.StudentNumber, outcome.StudentNumber);
        Assert.Equal(Now, outcome.RecordedAt);

        int attendanceId;
        await using (var observer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var attendance = await observer.AttendanceRecords.AsNoTracking()
                .SingleAsync(row => row.RegistrationId == setup.RegistrationId, ct);
            attendanceId = attendance.AttendanceId;
            Assert.Equal(CheckInMethodCode.QrCode, attendance.CheckInMethodId);
            Assert.Equal(setup.OfficerId, attendance.RecordedBy);
            Assert.Equal(Now, attendance.RecordedAt);
            Assert.Null(attendance.InvalidatedAt);

            var log = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(row => row.EntityType == "attendance" && row.EntityId == attendance.AttendanceId, ct);
            Assert.Equal(AuditActions.AttendanceRecorded, log.Action);
            Assert.Equal(setup.OfficerId, log.ActorUserId);
            Assert.Contains($"\"RegistrationId\":{setup.RegistrationId}", log.NewValues!, StringComparison.Ordinal);
            Assert.DoesNotContain("token", log.NewValues!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("qr", log.NewValues!, StringComparison.OrdinalIgnoreCase);
        }

        await using var repeatContext = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (repeatScanner, _) = Services(repeatContext, setup.OfficerId, RoleCodes.Officer);
        var repeat = await repeatScanner.ScanAsync(setup.EventId, setup.Token, ct);

        Assert.Equal(CheckInOutcomeViewModel.Repeat, repeat.Outcome);
        Assert.Equal(Now, repeat.RecordedAt);
        // A repeat arrives as a separate request with a fresh context, so this reads the stored
        // datetime2 rather than the tracked insert. The JSON must carry the UTC marker.
        Assert.Equal(DateTimeKind.Utc, repeat.RecordedAt!.Value.Kind);
        Assert.Equal(MemberName, repeat.MemberName);
        Assert.Equal(setup.StudentNumber, repeat.StudentNumber);

        await using var final = EventDraftTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(1, await final.AttendanceRecords.AsNoTracking()
            .CountAsync(row => row.RegistrationId == setup.RegistrationId, ct));
        Assert.Equal(1, await final.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "attendance" && row.EntityId == attendanceId, ct));
    }

    [Fact]
    public async Task BlankUnknownAndWrongEventTokensAreInvalidAndWriteNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await SeedScanAsync();
        var second = await SeedScanAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (scanner, _) = Services(context, first.OfficerId, RoleCodes.Officer);
        var before = await AuditCountAsync();

        var blank = await scanner.ScanAsync(first.EventId, "   ", ct);
        var unknown = await scanner.ScanAsync(first.EventId, new string('a', 64), ct);
        var wrongEvent = await scanner.ScanAsync(first.EventId, second.Token, ct);

        Assert.Equal(CheckInOutcomeViewModel.Invalid, blank.Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, unknown.Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, wrongEvent.Outcome);
        Assert.Equal(unknown.Message, wrongEvent.Message);
        AssertNoIdentity(blank);
        AssertNoIdentity(unknown);
        AssertNoIdentity(wrongEvent);
        await AssertNoAttendanceAsync(first.RegistrationId, second.RegistrationId);
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task UppercaseAndPaddedTokenIsNormalized()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedScanAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (scanner, _) = Services(context, setup.OfficerId, RoleCodes.Officer);

        var outcome = await scanner.ScanAsync(setup.EventId, $"  {setup.Token.ToUpperInvariant()}  ", ct);

        Assert.Equal(CheckInOutcomeViewModel.Success, outcome.Outcome);
        Assert.Equal(setup.RegistrationId, outcome.RegistrationId);
    }

    [Fact]
    public async Task ClosedWindowAndUnpublishedEventsAreClosedAndWriteNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var closed = await SeedScanAsync(open: false);
        var draft = await SeedScanAsync(status: EventStatusCode.Draft);
        var completed = await SeedScanAsync(status: EventStatusCode.Completed);
        var cancelled = await SeedScanAsync(status: EventStatusCode.Cancelled);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (scanner, _) = Services(context, closed.OfficerId, RoleCodes.Officer);
        var before = await AuditCountAsync();

        foreach (var setup in new[] { closed, draft, completed, cancelled })
        {
            var outcome = await scanner.ScanAsync(setup.EventId, setup.Token, ct);
            Assert.Equal(CheckInOutcomeViewModel.Closed, outcome.Outcome);
            AssertNoIdentity(outcome);
        }

        await AssertNoAttendanceAsync(closed.RegistrationId, draft.RegistrationId, completed.RegistrationId, cancelled.RegistrationId);
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task UnconfirmedAndCancelledRegistrationsAreInvalidAndWriteNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var pending = await SeedScanAsync(registrationStatus: RegistrationStatusCode.Pending);
        var cancelled = await SeedScanAsync(registrationStatus: RegistrationStatusCode.Cancelled);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (_, engine) = Services(context, pending.OfficerId, RoleCodes.Officer);
        var before = await AuditCountAsync();

        var pendingOutcome = await engine.RecordAsync(pending.EventId, pending.RegistrationId, CheckInMethodCode.QrCode, ct);
        var cancelledOutcome = await engine.RecordAsync(cancelled.EventId, cancelled.RegistrationId, CheckInMethodCode.QrCode, ct);

        Assert.Equal(AttendanceRecordingOutcome.Invalid, pendingOutcome.Outcome);
        Assert.Equal(AttendanceRecordingOutcome.Invalid, cancelledOutcome.Outcome);
        AssertNoIdentity(pendingOutcome);
        AssertNoIdentity(cancelledOutcome);
        await AssertNoAttendanceAsync(pending.RegistrationId, cancelled.RegistrationId);
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task IneligibleParticipantIsInvalidAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var inactiveMembership = await SeedScanAsync(membershipStatus: MembershipStatusCode.Inactive);
        var inactiveUser = await SeedScanAsync(userActive: false);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (scanner, _) = Services(context, inactiveMembership.OfficerId, RoleCodes.Officer);
        var before = await AuditCountAsync();

        var membershipOutcome = await scanner.ScanAsync(inactiveMembership.EventId, inactiveMembership.Token, ct);
        var userOutcome = await scanner.ScanAsync(inactiveUser.EventId, inactiveUser.Token, ct);

        Assert.Equal(CheckInOutcomeViewModel.Invalid, membershipOutcome.Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Invalid, userOutcome.Outcome);
        AssertNoIdentity(membershipOutcome);
        AssertNoIdentity(userOutcome);
        await AssertNoAttendanceAsync(inactiveMembership.RegistrationId, inactiveUser.RegistrationId);
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task InvalidatedAttendanceIsInvalidAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedScanAsync();
        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            writer.AttendanceRecords.Add(new Attendance
            {
                RegistrationId = setup.RegistrationId,
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
        var (scanner, _) = Services(context, setup.OfficerId, RoleCodes.Officer);
        var before = await AuditCountAsync();

        var outcome = await scanner.ScanAsync(setup.EventId, setup.Token, ct);

        Assert.Equal(CheckInOutcomeViewModel.Invalid, outcome.Outcome);
        AssertNoIdentity(outcome);
        await using var final = EventDraftTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(1, await final.AttendanceRecords.AsNoTracking()
            .CountAsync(row => row.RegistrationId == setup.RegistrationId, ct));
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task AttendedButNowRefusedReturnsInvalidInsteadOfRepeat()
    {
        var ct = TestContext.Current.CancellationToken;
        var cancelledAfterAttendance = await SeedScanAsync();
        var ineligibleAfterAttendance = await SeedScanAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (scanner, engine) = Services(context, cancelledAfterAttendance.OfficerId, RoleCodes.Officer);

        Assert.Equal(CheckInOutcomeViewModel.Success,
            (await scanner.ScanAsync(cancelledAfterAttendance.EventId, cancelledAfterAttendance.Token, ct)).Outcome);
        Assert.Equal(CheckInOutcomeViewModel.Success,
            (await scanner.ScanAsync(ineligibleAfterAttendance.EventId, ineligibleAfterAttendance.Token, ct)).Outcome);

        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Registrations.SingleAsync(
                registration => registration.RegistrationId == cancelledAfterAttendance.RegistrationId, ct);
            row.RegistrationStatusId = RegistrationStatusCode.Cancelled;
            await writer.SaveChangesAsync(ct);
        }

        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var member = await writer.Members.SingleAsync(
                item => item.MemberId == ineligibleAfterAttendance.MemberId, ct);
            member.MembershipStatusId = MembershipStatusCode.Inactive;
            await writer.SaveChangesAsync(ct);
        }

        var cancelledOutcome = await engine.RecordAsync(
            cancelledAfterAttendance.EventId, cancelledAfterAttendance.RegistrationId, CheckInMethodCode.QrCode, ct);
        var ineligibleOutcome = await engine.RecordAsync(
            ineligibleAfterAttendance.EventId, ineligibleAfterAttendance.RegistrationId, CheckInMethodCode.QrCode, ct);

        Assert.Equal(AttendanceRecordingOutcome.Invalid, cancelledOutcome.Outcome);
        Assert.Equal(AttendanceRecordingOutcome.Invalid, ineligibleOutcome.Outcome);
        AssertNoIdentity(cancelledOutcome);
        AssertNoIdentity(ineligibleOutcome);

        await using var final = EventDraftTestSupport.Context(fixture.ConnectionString);
        var attendanceIds = await final.AttendanceRecords.AsNoTracking()
            .Where(row => row.RegistrationId == cancelledAfterAttendance.RegistrationId
                          || row.RegistrationId == ineligibleAfterAttendance.RegistrationId)
            .Select(row => row.AttendanceId)
            .ToListAsync(ct);
        Assert.Equal(2, attendanceIds.Count);
        Assert.Equal(2, await final.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "attendance" && attendanceIds.Contains(row.EntityId), ct));
    }

    [Fact]
    public async Task ParallelDuplicateScansWriteOneAttendanceRowAndOneAuditEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedScanAsync();

        // Both scans pause immediately before their users-row lock, so they enter the engine
        // together and the second one has to observe the first one's committed row. SM-4: the
        // unique registration index plus the lock order must leave exactly one attendance record.
        var firstGate = new MemberEventTestSupport.LockGateInterceptor("users");
        var secondGate = new MemberEventTestSupport.LockGateInterceptor("users");
        await using var firstContext = MemberEventTestSupport.Context(fixture.ConnectionString, firstGate);
        await using var secondContext = MemberEventTestSupport.Context(fixture.ConnectionString, secondGate);
        var (firstScanner, _) = Services(firstContext, setup.OfficerId, RoleCodes.Officer);
        var (secondScanner, _) = Services(secondContext, setup.OfficerId, RoleCodes.Officer);

        var first = firstScanner.ScanAsync(setup.EventId, setup.Token, ct);
        var second = secondScanner.ScanAsync(setup.EventId, setup.Token, ct);
        try
        {
            await Task.WhenAll(firstGate.Attempted, secondGate.Attempted)
                .WaitAsync(TimeSpan.FromSeconds(30), ct);
        }
        finally
        {
            firstGate.Release.TrySetResult();
            secondGate.Release.TrySetResult();
        }

        var outcomes = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30), ct);

        var recorded = Assert.Single(outcomes, outcome => outcome.Outcome == CheckInOutcomeViewModel.Success);
        var repeat = Assert.Single(outcomes, outcome => outcome.Outcome == CheckInOutcomeViewModel.Repeat);
        Assert.Equal(setup.RegistrationId, recorded.RegistrationId);
        Assert.Equal(setup.RegistrationId, repeat.RegistrationId);
        // The repeat answers with the original time, not the second scan's clock reading.
        Assert.Equal(recorded.RecordedAt, repeat.RecordedAt);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var attendance = await observer.AttendanceRecords.AsNoTracking()
            .SingleAsync(row => row.RegistrationId == setup.RegistrationId, ct);
        Assert.Equal(Now, attendance.RecordedAt);
        Assert.Equal(1, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "attendance" && row.EntityId == attendance.AttendanceId, ct));
    }

    [Fact]
    public async Task WaitlistedRegistrationIsInvalidAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedScanAsync(registrationStatus: RegistrationStatusCode.Waitlisted);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (scanner, engine) = Services(context, setup.OfficerId, RoleCodes.Officer);
        var before = await AuditCountAsync();

        var outcome = await scanner.ScanAsync(setup.EventId, setup.Token, ct);
        var direct = await engine.RecordAsync(setup.EventId, setup.RegistrationId, CheckInMethodCode.QrCode, ct);

        Assert.Equal(CheckInOutcomeViewModel.Invalid, outcome.Outcome);
        Assert.Equal(AttendanceRecordingOutcome.Invalid, direct.Outcome);
        AssertNoIdentity(outcome);
        AssertNoIdentity(direct);
        await AssertNoAttendanceAsync(setup.RegistrationId);
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task NonManagementIsForbiddenBeforeAnyWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedScanAsync();
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (_, engine) = Services(context, setup.OfficerId, RoleCodes.Member);
        var before = await AuditCountAsync();

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => engine.RecordAsync(setup.EventId, setup.RegistrationId, CheckInMethodCode.QrCode, ct));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
        await AssertNoAttendanceAsync(setup.RegistrationId);
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task ScreenReadsRealRecentLogNewestFirstAndCapsAt25()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedScanAsync();
        var registrationIds = await SeedLogAsync(setup.EventId, setup.OfficerId, 30);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var (scanner, _) = Services(context, setup.OfficerId, RoleCodes.Officer);

        var screen = await scanner.GetScreenAsync(setup.EventId, ct);

        Assert.Equal(setup.EventId, screen.EventId);
        Assert.True(screen.IsCheckInOpen);
        Assert.Equal(25, screen.RecentResults.Count);
        Assert.Equal(registrationIds[0], screen.RecentResults[0].RegistrationId);
        Assert.Equal(registrationIds[24], screen.RecentResults[24].RegistrationId);
        Assert.Equal("Recorded", screen.RecentResults[0].Outcome);
        Assert.False(screen.RecentResults[0].CanInvalidate);
        Assert.True(screen.RecentResults[1].IsInvalidated);
        Assert.Equal("Correction.", screen.RecentResults[1].Message);
        Assert.False(screen.RecentResults[1].CanInvalidate);
        Assert.True(screen.RecentResults[2].CanInvalidate);
    }

    private static (CheckInService Scanner, AttendanceRecordingService Engine) Services(
        AsiBasecodeDBContext context,
        int actorId,
        string role)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorId, role);
        var clock = new FixedClock(Now);
        var events = new EventRepository(unitOfWork);
        var registrations = new RegistrationRepository(unitOfWork);
        var attendance = new AttendanceRepository(unitOfWork);
        var engine = new AttendanceRecordingService(
            registrations,
            new UserRepository(unitOfWork),
            new MemberRepository(unitOfWork),
            events,
            attendance,
            unitOfWork,
            actor,
            new ActivityLogWriter(context, actor, clock),
            clock);
        return (new CheckInService(events, registrations, attendance, engine), engine);
    }

    private async Task<ScanSetup> SeedScanAsync(
        EventStatusCode status = EventStatusCode.Published,
        bool open = true,
        RegistrationStatusCode registrationStatus = RegistrationStatusCode.Confirmed,
        MembershipStatusCode membershipStatus = MembershipStatusCode.Active,
        bool userActive = true)
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, status,
            Now.AddHours(1), Now.AddMinutes(-30), 20, null, $"Check-in flow {Guid.NewGuid():N}", ct);

        if (open)
        {
            await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
            var row = await writer.Events.SingleAsync(item => item.EventId == eventItem.EventId, ct);
            row.IsCheckInOpen = true;
            row.CheckInOpenedBy = officerId;
            row.CheckInOpenedAt = Now;
            await writer.SaveChangesAsync(ct);
        }

        var memberUser = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"check-in-{Guid.NewGuid():N}@attendta.test",
            MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);

        if (!userActive)
        {
            await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
            var row = await writer.Users.SingleAsync(user => user.Id == memberUser.Id, ct);
            row.IsActive = false;
            await writer.SaveChangesAsync(ct);
        }

        var member = await MemberEventTestSupport.SeedMemberAsync(
            fixture.ConnectionString, memberUser.Id, membershipStatus, ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, registrationStatus, officerId, ct);

        var token = NewToken();
        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Registrations.SingleAsync(
                item => item.RegistrationId == registration.RegistrationId, ct);
            row.QrToken = token;
            await writer.SaveChangesAsync(ct);
        }

        return new ScanSetup(
            officerId,
            member.MemberId,
            eventItem.EventId,
            registration.RegistrationId,
            token,
            member.StudentNumber);
    }

    private async Task<IReadOnlyList<int>> SeedLogAsync(int eventId, int officerId, int count)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);

        var users = Enumerable.Range(0, count)
            .Select(index => new User
            {
                UserId = $"log-{Guid.NewGuid():N}@attendta.test",
                Name = $"Log Member {index:D2}",
                Password = "test",
                RoleId = RoleCode.Member,
                IsActive = true
            })
            .ToList();
        context.Users.AddRange(users);
        await context.SaveChangesAsync(ct);

        var members = users
            .Select(user => new Member
            {
                UserId = user.Id,
                StudentNumber = $"LOG-{Guid.NewGuid():N}",
                Course = "BSIT",
                YearLevel = 1,
                Section = "A",
                MembershipStatusId = MembershipStatusCode.Active,
                CreatedBy = officerId,
                CreatedAt = Now
            })
            .ToList();
        context.Members.AddRange(members);
        await context.SaveChangesAsync(ct);

        var registrations = members
            .Select(member => new Registration
            {
                EventId = eventId,
                MemberId = member.MemberId,
                RegistrationStatusId = RegistrationStatusCode.Confirmed,
                CourseSnapshot = "BSIT",
                YearLevelSnapshot = 1,
                SectionSnapshot = "A",
                SubmittedAt = Now,
                CreatedBy = officerId,
                CreatedAt = Now
            })
            .ToList();
        context.Registrations.AddRange(registrations);
        await context.SaveChangesAsync(ct);

        var attendance = registrations
            .Select((registration, index) => new Attendance
            {
                RegistrationId = registration.RegistrationId,
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = officerId,
                RecordedAt = Now.AddMinutes(-index)
            })
            .ToList();
        context.AttendanceRecords.AddRange(attendance);
        await context.SaveChangesAsync(ct);

        // The newest row already carries an issued certificate; the second is invalidated.
        context.Certificates.Add(new Certificate
        {
            RegistrationId = registrations[0].RegistrationId,
            CertificateStatusId = CertificateStatusCode.Issued,
            CreatedBy = officerId,
            CreatedAt = Now
        });
        attendance[1].InvalidatedBy = officerId;
        attendance[1].InvalidatedAt = Now;
        attendance[1].InvalidationReason = "Correction.";
        await context.SaveChangesAsync(ct);

        return registrations.Select(registration => registration.RegistrationId).ToList();
    }

    private async Task AssertNoAttendanceAsync(params int[] registrationIds)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        foreach (var registrationId in registrationIds)
        {
            Assert.False(await context.AttendanceRecords.AsNoTracking()
                .AnyAsync(row => row.RegistrationId == registrationId, ct));
        }
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

    private static void AssertNoIdentity(AttendanceRecordingResult result)
    {
        Assert.Null(result.MemberName);
        Assert.Null(result.StudentNumber);
        Assert.Null(result.RecordedAt);
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private sealed record ScanSetup(
        int OfficerId,
        int MemberId,
        int EventId,
        int RegistrationId,
        string Token,
        string StudentNumber);

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
