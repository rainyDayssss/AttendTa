using System.Net;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventCheckInWindowFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OfficerOpensAndClosesPublishedEventAndRefusalsLeaveAuditUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var published = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(1), Now.AddMinutes(-30), 20, null, "Check-in window published", ct);
        var draft = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officerId, typeId, venueId, EventStatusCode.Draft,
            Now.AddHours(2), Now.AddMinutes(-15), 20, null, "Check-in window draft", ct);

        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = EventDraftTestSupport.Service(context, officerId, new FixedClock(Now));

        await service.SetCheckInWindowAsync(published.EventId, true, ct);

        await using (var observer = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var opened = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == published.EventId, ct);
            Assert.True(opened.IsCheckInOpen);
            Assert.Equal(officerId, opened.CheckInOpenedBy);
            Assert.Equal(Now, opened.CheckInOpenedAt);
            Assert.Equal(officerId, opened.UpdatedBy);
            Assert.Equal(Now, opened.UpdatedAt);

            var openLog = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(row => row.EntityType == "events" && row.EntityId == published.EventId, ct);
            Assert.Equal(AuditActions.EventCheckInOpened, openLog.Action);
            Assert.Equal(officerId, openLog.ActorUserId);
            Assert.Contains("\"IsCheckInOpen\":false", openLog.OldValues!, StringComparison.Ordinal);
            Assert.Contains("\"IsCheckInOpen\":true", openLog.NewValues!, StringComparison.Ordinal);
            Assert.Contains($"\"CheckInOpenedBy\":{officerId}", openLog.NewValues!, StringComparison.Ordinal);
            Assert.DoesNotContain("token", openLog.NewValues!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("path", openLog.NewValues!, StringComparison.OrdinalIgnoreCase);
        }

        await service.SetCheckInWindowAsync(published.EventId, false, ct);

        await using (var observer = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var closed = await observer.Events.AsNoTracking().SingleAsync(row => row.EventId == published.EventId, ct);
            Assert.False(closed.IsCheckInOpen);
            Assert.Equal(officerId, closed.CheckInClosedBy);
            Assert.Equal(Now, closed.CheckInClosedAt);

            var logs = await observer.ActivityLogs.AsNoTracking()
                .Where(row => row.EntityType == "events" && row.EntityId == published.EventId)
                .OrderBy(row => row.LogId)
                .ToListAsync(ct);
            Assert.Equal(2, logs.Count);
            Assert.Equal(AuditActions.EventCheckInOpened, logs[0].Action);
            var closeLog = logs[1];
            Assert.Equal(AuditActions.EventCheckInClosed, closeLog.Action);
            Assert.Contains("\"IsCheckInOpen\":true", closeLog.OldValues!, StringComparison.Ordinal);
            Assert.Contains("\"IsCheckInOpen\":false", closeLog.NewValues!, StringComparison.Ordinal);
            Assert.Contains($"\"CheckInClosedBy\":{officerId}", closeLog.NewValues!, StringComparison.Ordinal);
        }

        var repeated = await Assert.ThrowsAsync<DomainException>(
            () => service.SetCheckInWindowAsync(published.EventId, false, ct));
        Assert.Equal(OutcomeCode.Invalid, repeated.Outcome);

        var refused = await Assert.ThrowsAsync<DomainException>(
            () => service.SetCheckInWindowAsync(draft.EventId, true, ct));
        Assert.Equal(OutcomeCode.Invalid, refused.Outcome);

        await using var final = MemberEventTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(2, await final.ActivityLogs
            .CountAsync(row => row.EntityType == "events" && row.EntityId == published.EventId, ct));
        var draftRow = await final.Events.AsNoTracking().SingleAsync(row => row.EventId == draft.EventId, ct);
        Assert.False(draftRow.IsCheckInOpen);
        Assert.Null(draftRow.CheckInOpenedBy);
        Assert.Equal(0, await final.ActivityLogs
            .CountAsync(row => row.EntityType == "events" && row.EntityId == draft.EventId, ct));
    }

    [Fact]
    public async Task RealCheckInWindowCommandWorksThroughTheHttpEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"check-in-window-{Guid.NewGuid():N}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var officer = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, email, password, RoleCode.Officer, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(
            fixture.ConnectionString, officer.Id, ct);
        var published = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officer.Id, typeId, venueId, EventStatusCode.Published,
            Now.AddHours(1), Now.AddMinutes(-30), 20, null, "Check-in window without demo data", ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await MemberEventTestSupport.SignInAsync(client, email, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, "/Event", ct);
        using var response = await client.PostAsync("/Event/OpenCheckIn", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = published.EventId.ToString(),
            ["__RequestVerificationToken"] = token
        }), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var opened = await observer.Events.AsNoTracking()
            .SingleAsync(row => row.EventId == published.EventId, ct);
        Assert.True(opened.IsCheckInOpen);
        Assert.Equal(officer.Id, opened.CheckInOpenedBy);
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(
            row => row.EntityType == "events"
                && row.EntityId == published.EventId
                && row.Action == AuditActions.EventCheckInOpened, ct));

        var openPage = await client.GetStringAsync("/Event", ct);
        Assert.Contains("Check-in open", openPage, StringComparison.Ordinal);
        Assert.Contains($"Opened by {officer.Name} &#xB7; ", openPage, StringComparison.Ordinal);

        var closeToken = await MemberEventTestSupport.AntiforgeryTokenAsync(client, "/Event", ct);
        using var closeResponse = await client.PostAsync("/Event/CloseCheckIn", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = published.EventId.ToString(),
            ["__RequestVerificationToken"] = closeToken
        }), ct);

        Assert.Equal(HttpStatusCode.Redirect, closeResponse.StatusCode);

        var closedPage = await client.GetStringAsync("/Event", ct);
        Assert.Contains("Check-in closed", closedPage, StringComparison.Ordinal);
        Assert.Contains($"Closed by {officer.Name} &#xB7; ", closedPage, StringComparison.Ordinal);

        await using var closedObserver = MemberEventTestSupport.Context(fixture.ConnectionString);
        var closed = await closedObserver.Events.AsNoTracking()
            .SingleAsync(row => row.EventId == published.EventId, ct);
        Assert.False(closed.IsCheckInOpen);
        Assert.Equal(officer.Id, closed.CheckInClosedBy);

        var auditRows = await closedObserver.ActivityLogs.AsNoTracking()
            .Where(row => row.EntityType == "events" && row.EntityId == published.EventId)
            .ToListAsync(ct);
        Assert.Equal(2, auditRows.Count);
        Assert.Single(auditRows, row => row.Action == AuditActions.EventCheckInOpened);
        Assert.Single(auditRows, row => row.Action == AuditActions.EventCheckInClosed);
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}
