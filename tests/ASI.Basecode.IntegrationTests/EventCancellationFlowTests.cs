using System.Net;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventCancellationServiceTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CancelPublishedEventCancelsOpenRegistrationsClosesCheckInAndWritesAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddDays(10), Now.AddDays(9), 20, null, "Cancel published event", ct);
        await OpenCheckInAsync(eventItem.EventId, officerId, ct);
        var seeded = await SeedOpenRegistrationsAsync(eventItem.EventId, officerId, ct);

        var recorder = new EventDraftPersistenceTests.LockOrderRecorder();
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, recorder);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now));

        await service.CancelAsync(eventItem.EventId, "  Typhoon warning  ", ct);

        Assert.Single(recorder.Locks);
        Assert.Contains("events", recorder.Locks[0], StringComparison.OrdinalIgnoreCase);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Cancelled, persisted.EventStatusId);
        Assert.Equal(officerId, persisted.CancelledBy);
        Assert.Equal(Now, persisted.CancelledAt);
        Assert.Equal("Typhoon warning", persisted.CancellationReason);
        Assert.False(persisted.IsCheckInOpen);
        Assert.Equal(officerId, persisted.CheckInClosedBy);
        Assert.Equal(Now, persisted.CheckInClosedAt);
        Assert.Equal(officerId, persisted.UpdatedBy);
        Assert.Equal(Now, persisted.UpdatedAt);

        var rows = await observer.Registrations.AsNoTracking()
            .Where(row => row.EventId == eventItem.EventId)
            .ToDictionaryAsync(row => row.RegistrationId, ct);
        foreach (var id in new[] { seeded.PendingId, seeded.ConfirmedId, seeded.WaitlistedId })
        {
            Assert.Equal(RegistrationStatusCode.Cancelled, rows[id].RegistrationStatusId);
            Assert.Null(rows[id].QrToken);
            Assert.Equal(officerId, rows[id].CancelledBy);
            Assert.Equal(Now, rows[id].CancelledAt);
            Assert.Equal("Typhoon warning", rows[id].CancellationReason);
        }

        Assert.Equal("previous cancellation", rows[seeded.PreviouslyCancelledId].CancellationReason);
        Assert.NotEqual(officerId, rows[seeded.PreviouslyCancelledId].CancelledBy);

        var registrationIds = rows.Keys.ToArray();
        var logs = await observer.ActivityLogs.AsNoTracking()
            .Where(log => (log.EntityType == "events" && log.EntityId == eventItem.EventId)
                || (log.EntityType == "registrations" && registrationIds.Contains(log.EntityId)))
            .ToListAsync(ct);
        Assert.Equal(4, logs.Count);
        var eventLog = Assert.Single(logs, log => log.Action == AuditActions.EventCancelled);
        Assert.Equal(officerId, eventLog.ActorUserId);
        Assert.Equal("Typhoon warning", eventLog.Reason);
        Assert.Contains("\"Status\":2", eventLog.OldValues!, StringComparison.Ordinal);
        Assert.Contains("\"IsCheckInOpen\":true", eventLog.OldValues!, StringComparison.Ordinal);
        Assert.Contains("\"Status\":3", eventLog.NewValues!, StringComparison.Ordinal);
        Assert.Contains("\"IsCheckInOpen\":false", eventLog.NewValues!, StringComparison.Ordinal);
        Assert.DoesNotContain("token", eventLog.NewValues!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("path", eventLog.NewValues!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, logs.Count(log => log.Action == AuditActions.RegistrationCancelled));
        Assert.DoesNotContain(logs, log => log.EntityType == "registrations" && log.EntityId == seeded.PreviouslyCancelledId);
    }

    [Fact]
    public async Task CancelDraftEventRecordsMetadataWithoutClosingAnAlreadyClosedCheckIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Draft,
            Now.AddDays(10), Now.AddDays(9), 20, null, "Cancel draft event", ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now));

        await service.CancelAsync(eventItem.EventId, "No longer needed", ct);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Cancelled, persisted.EventStatusId);
        Assert.Equal(officerId, persisted.CancelledBy);
        Assert.Equal(Now, persisted.CancelledAt);
        Assert.Equal("No longer needed", persisted.CancellationReason);
        Assert.False(persisted.IsCheckInOpen);
        Assert.Null(persisted.CheckInClosedBy);
        Assert.Null(persisted.CheckInClosedAt);
        var log = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct);
        Assert.Equal(AuditActions.EventCancelled, log.Action);
        Assert.Equal(0, await observer.Registrations.CountAsync(row => row.EventId == eventItem.EventId, ct));
    }

    [Theory]
    [InlineData("attendance")]
    [InlineData("certificate")]
    public async Task CancelRefusesValidAttendanceOrAnIssuedCertificateWithoutChanges(string blocker)
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddDays(10), Now.AddDays(9), 20, null, $"Blocked by {blocker}", ct);
        await OpenCheckInAsync(eventItem.EventId, officerId, ct);
        var seeded = await SeedOpenRegistrationsAsync(eventItem.EventId, officerId, ct);
        await SeedBlockerAsync(seeded.ConfirmedId, blocker, officerId, ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now));

        var error = await Assert.ThrowsAsync<DomainException>(() => service.CancelAsync(eventItem.EventId, "Typhoon warning", ct));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Published, persisted.EventStatusId);
        Assert.True(persisted.IsCheckInOpen);
        Assert.Null(persisted.CancelledBy);
        Assert.Null(persisted.CancellationReason);
        Assert.Equal(RegistrationStatusCode.Confirmed,
            (await observer.Registrations.AsNoTracking().SingleAsync(row => row.RegistrationId == seeded.ConfirmedId, ct)).RegistrationStatusId);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct));
    }

    [Theory]
    [InlineData(EventStatusCode.Completed)]
    [InlineData(EventStatusCode.Cancelled)]
    public async Task CancelRefusesTerminalEventsWithoutChanges(EventStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, status,
            Now.AddDays(10), Now.AddDays(9), 20, null, $"Terminal {status}", ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now));

        var error = await Assert.ThrowsAsync<DomainException>(() => service.CancelAsync(eventItem.EventId, "Typhoon warning", ct));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(status, persisted.EventStatusId);
        Assert.Null(persisted.CancelledBy);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct));
    }

    [Fact]
    public async Task CancelRefusesABlankReasonAndAMissingEventWithoutChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddDays(10), Now.AddDays(9), 20, null, "Blank reason event", ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now));

        var blank = await Assert.ThrowsAsync<DomainException>(() => service.CancelAsync(eventItem.EventId, "   ", ct));
        Assert.Equal(OutcomeCode.Invalid, blank.Outcome);

        var missing = await Assert.ThrowsAsync<DomainException>(() => service.CancelAsync(999_999, "Typhoon warning", ct));
        Assert.Equal(OutcomeCode.NotFound, missing.Outcome);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(EventStatusCode.Published,
            (await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct)).EventStatusId);
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(row => row.EntityType == "events" && row.EntityId == eventItem.EventId, ct));
    }

    [Fact]
    public async Task AuditFailureRollsBackTheEventRegistrationsAndAuditTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddDays(10), Now.AddDays(9), 20, null, "Rollback event", ct);
        await OpenCheckInAsync(eventItem.EventId, officerId, ct);
        var seeded = await SeedOpenRegistrationsAsync(eventItem.EventId, officerId, ct);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now),
            audit => new FailEventCancellationAudit(audit));

        await Assert.ThrowsAsync<IOException>(() => service.CancelAsync(eventItem.EventId, "Typhoon warning", ct));

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == eventItem.EventId, ct);
        Assert.Equal(EventStatusCode.Published, persisted.EventStatusId);
        Assert.True(persisted.IsCheckInOpen);
        Assert.Null(persisted.CancelledBy);
        Assert.Null(persisted.CancellationReason);
        Assert.Null(persisted.CheckInClosedBy);
        var rows = await observer.Registrations.AsNoTracking()
            .Where(row => row.EventId == eventItem.EventId)
            .ToDictionaryAsync(row => row.RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Pending, rows[seeded.PendingId].RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Confirmed, rows[seeded.ConfirmedId].RegistrationStatusId);
        Assert.Equal(RegistrationStatusCode.Waitlisted, rows[seeded.WaitlistedId].RegistrationStatusId);
        Assert.NotNull(rows[seeded.PendingId].QrToken);
        var registrationIds = rows.Keys.ToArray();
        Assert.Equal(0, await observer.ActivityLogs.CountAsync(
            log => (log.EntityType == "events" && log.EntityId == eventItem.EventId)
                || (log.EntityType == "registrations" && registrationIds.Contains(log.EntityId)), ct));
    }

    private async Task OpenCheckInAsync(int eventId, int actorId, CancellationToken cancellationToken)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Events.SingleAsync(item => item.EventId == eventId, cancellationToken);
        row.IsCheckInOpen = true;
        row.CheckInOpenedBy = actorId;
        row.CheckInOpenedAt = Now.AddHours(-1);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<SeededRegistrations> SeedOpenRegistrationsAsync(int eventId, int actorId, CancellationToken cancellationToken)
    {
        var pendingUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"cancel-pending-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, cancellationToken);
        var confirmedUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"cancel-confirmed-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, cancellationToken);
        var waitlistedUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"cancel-waitlisted-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, cancellationToken);
        var previousUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"cancel-previous-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, cancellationToken);
        var pendingMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, pendingUser.Id, MembershipStatusCode.Active, cancellationToken);
        var confirmedMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, confirmedUser.Id, MembershipStatusCode.Active, cancellationToken);
        var waitlistedMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, waitlistedUser.Id, MembershipStatusCode.Active, cancellationToken);
        var previousMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, previousUser.Id, MembershipStatusCode.Active, cancellationToken);

        var pending = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventId, pendingMember.MemberId, RegistrationStatusCode.Pending, actorId, cancellationToken);
        var confirmed = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventId, confirmedMember.MemberId, RegistrationStatusCode.Confirmed, actorId, cancellationToken);
        var waitlisted = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventId, waitlistedMember.MemberId, RegistrationStatusCode.Waitlisted, actorId, cancellationToken);
        var previous = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventId, previousMember.MemberId, RegistrationStatusCode.Cancelled, actorId, cancellationToken);

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var rows = await context.Registrations.Where(row => row.EventId == eventId).ToListAsync(cancellationToken);
        foreach (var row in rows.Where(row => row.RegistrationStatusId != RegistrationStatusCode.Cancelled))
        {
            row.QrToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        }

        var preserved = rows.Single(row => row.RegistrationId == previous.RegistrationId);
        preserved.CancelledBy = previousUser.Id;
        preserved.CancelledAt = Now.AddDays(-5);
        preserved.CancellationReason = "previous cancellation";
        await context.SaveChangesAsync(cancellationToken);

        return new SeededRegistrations(pending.RegistrationId, confirmed.RegistrationId, waitlisted.RegistrationId, previous.RegistrationId);
    }

    private async Task SeedBlockerAsync(int registrationId, string blocker, int actorId, CancellationToken cancellationToken)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        if (blocker == "attendance")
        {
            context.AttendanceRecords.Add(new Attendance
            {
                RegistrationId = registrationId,
                CheckInMethodId = CheckInMethodCode.ManualSearch,
                RecordedBy = actorId,
                RecordedAt = Now
            });
        }
        else
        {
            context.Certificates.Add(new Certificate
            {
                RegistrationId = registrationId,
                CertificateStatusId = CertificateStatusCode.Issued,
                CertificateNumber = $"CERT-{Guid.NewGuid():N}",
                CreatedAt = Now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeededRegistrations(int PendingId, int ConfirmedId, int WaitlistedId, int PreviouslyCancelledId);

    private sealed class FailEventCancellationAudit(IActivityLogWriter inner) : IActivityLogWriter
    {
        public void Append(AuditEntry entry)
        {
            if (entry.Action == AuditActions.EventCancelled) throw new IOException("simulated audit failure");
            inner.Append(entry);
        }
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}

public sealed class EventCancellationFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OfficerCancelsThroughTheConfirmationScreenWhileAuthorizationAndRefusalsLeaveRowsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"cancel-officer-{suffix}@attendta.test";
        var memberEmail = $"cancel-member-{suffix}@attendta.test";
        const string password = "Cancel-test-password-aA1!";
        var officerId = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var published = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddDays(10), Now.AddDays(9), 20, null, "Officer cancellation event", ct);
        var draft = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Draft,
            Now.AddDays(11), Now.AddDays(10), 20, null, "Draft cancellation event", ct);
        var completed = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Completed,
            Now.AddDays(12), Now.AddDays(11), 20, null, "Completed cancellation event", ct);
        await OpenCheckInAsync(published.EventId, officerId, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, officerId, MembershipStatusCode.Active, ct);
        await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, published.EventId, member.MemberId, RegistrationStatusCode.Confirmed, officerId, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var anonymousPage = await anonymous.GetAsync($"/Event/Cancel?id={published.EventId}", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymousPage.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymousPage), StringComparison.Ordinal);

        await EventDraftTestSupport.SignInAsync(officer, officerEmail, password, ct);
        await EventDraftTestSupport.SignInAsync(memberClient, memberEmail, password, ct);

        var list = await officer.GetStringAsync("/Event", ct);
        Assert.Contains($"/Event/Cancel?id={published.EventId}", list, StringComparison.Ordinal);
        Assert.Contains($"/Event/Cancel?id={draft.EventId}", list, StringComparison.Ordinal);
        Assert.DoesNotContain($"/Event/Cancel?id={completed.EventId}", list, StringComparison.Ordinal);

        var confirmation = await officer.GetStringAsync($"/Event/Cancel?id={published.EventId}", ct);
        Assert.Contains("Officer cancellation event", confirmation, StringComparison.Ordinal);
        Assert.Contains("Reason", confirmation, StringComparison.Ordinal);
        Assert.Contains("closes check-in", confirmation, StringComparison.Ordinal);

        var memberToken = await EventDraftTestSupport.AntiforgeryTokenAsync(memberClient, "/Home/Index", ct);
        var forbidden = await memberClient.PostAsync("/Event/Cancel",
            EventDraftTestSupport.Form(new() { ["EventId"] = published.EventId.ToString(), ["Reason"] = "Not allowed", ["__RequestVerificationToken"] = memberToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Contains("/Account/AccessDenied", MemberEventTestSupport.LocationPath(forbidden), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await officer.PostAsync("/Event/Cancel", new FormUrlEncodedContent([]), ct)).StatusCode);

        var blankToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officer, $"/Event/Cancel?id={published.EventId}", ct);
        var blank = await officer.PostAsync("/Event/Cancel",
            EventDraftTestSupport.Form(new() { ["EventId"] = published.EventId.ToString(), ["Reason"] = "   ", ["__RequestVerificationToken"] = blankToken }), ct);
        Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
        Assert.Contains("A cancellation reason is required.", await blank.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var token = await EventDraftTestSupport.AntiforgeryTokenAsync(officer, $"/Event/Cancel?id={published.EventId}", ct);
        var cancelled = await officer.PostAsync("/Event/Cancel",
            EventDraftTestSupport.Form(new() { ["EventId"] = published.EventId.ToString(), ["Reason"] = "Typhoon warning", ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        Assert.Equal("/Event", EventDraftTestSupport.LocationPath(cancelled));

        var afterCancel = await officer.GetStringAsync("/Event", ct);
        Assert.Contains("Event cancelled.", afterCancel, StringComparison.Ordinal);
        Assert.DoesNotContain($"/Event/Cancel?id={published.EventId}", afterCancel, StringComparison.Ordinal);

        var terminalPage = await officer.GetAsync($"/Event/Cancel?id={completed.EventId}", ct);
        Assert.Equal(HttpStatusCode.Redirect, terminalPage.StatusCode);
        Assert.Equal("/Event", EventDraftTestSupport.LocationPath(terminalPage));
        var terminalList = await officer.GetStringAsync("/Event", ct);
        Assert.Contains("Only draft or published events can be cancelled.", terminalList, StringComparison.Ordinal);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var persisted = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == published.EventId, ct);
        Assert.Equal(EventStatusCode.Cancelled, persisted.EventStatusId);
        Assert.Equal(officerId, persisted.CancelledBy);
        Assert.Equal("Typhoon warning", persisted.CancellationReason);
        Assert.False(persisted.IsCheckInOpen);
        Assert.Equal(officerId, persisted.CheckInClosedBy);
        var registration = await observer.Registrations.AsNoTracking().SingleAsync(row => row.EventId == published.EventId, ct);
        Assert.Equal(RegistrationStatusCode.Cancelled, registration.RegistrationStatusId);
        Assert.Null(registration.QrToken);
        Assert.Equal(EventStatusCode.Draft, (await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == draft.EventId, ct)).EventStatusId);
        Assert.Equal(EventStatusCode.Completed, (await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == completed.EventId, ct)).EventStatusId);
        var logs = await observer.ActivityLogs.AsNoTracking()
            .Where(log => (log.EntityType == "events" && log.EntityId == published.EventId)
                || (log.EntityType == "registrations" && log.EntityId == registration.RegistrationId))
            .ToListAsync(ct);
        Assert.Equal(2, logs.Count);
        Assert.Single(logs, log => log.Action == AuditActions.EventCancelled);
        Assert.Single(logs, log => log.Action == AuditActions.RegistrationCancelled);
    }

    private async Task OpenCheckInAsync(int eventId, int actorId, CancellationToken cancellationToken)
    {
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var row = await context.Events.SingleAsync(item => item.EventId == eventId, cancellationToken);
        row.IsCheckInOpen = true;
        row.CheckInOpenedBy = actorId;
        row.CheckInOpenedAt = Now.AddHours(-1);
        await context.SaveChangesAsync(cancellationToken);
    }
}
