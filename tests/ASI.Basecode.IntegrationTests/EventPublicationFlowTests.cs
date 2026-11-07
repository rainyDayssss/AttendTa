using System.Globalization;
using System.Net;
using Microsoft.Data.SqlClient;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventPublicationFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task OfficerPublishesCoveredDraftWithAntiforgeryAndAuditWhileRefusalsLeaveRowsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"publish-officer-{suffix}@attendta.test";
        var memberEmail = $"publish-member-{suffix}@attendta.test";
        const string password = "Publish-test-password-aA1!";
        var officerId = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, officerId, 30, ct);
        var publishableId = await SeedEventAsync(officerId, typeId, venueId, "uploads/events/0123456789abcdef0123456789abcdef.jpg", ct);
        var missingCoverId = await SeedEventAsync(officerId, typeId, venueId, null, ct);
        var rollbackId = await SeedEventAsync(officerId, typeId, venueId, "uploads/events/11111111111111111111111111111111.jpg", ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var member = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EventDraftTestSupport.SignInAsync(officer, officerEmail, password, ct);
        await EventDraftTestSupport.SignInAsync(member, memberEmail, password, ct);

        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsync($"/Event/Publish?id={publishableId}", new FormUrlEncodedContent([]), ct)).StatusCode);

        var memberToken = await EventDraftTestSupport.AntiforgeryTokenAsync(member, "/Home/Index", ct);
        var forbidden = await member.PostAsync($"/Event/Publish?id={publishableId}", EventDraftTestSupport.Form(new() { ["__RequestVerificationToken"] = memberToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Contains("/Account/AccessDenied", EventDraftTestSupport.LocationPath(forbidden), StringComparison.Ordinal);

        var token = await EventDraftTestSupport.AntiforgeryTokenAsync(officer, "/Event", ct);
        var published = await officer.PostAsync($"/Event/Publish?id={publishableId}", EventDraftTestSupport.Form(new() { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, published.StatusCode);
        Assert.Equal("/Event", EventDraftTestSupport.LocationPath(published));

        var refusalToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officer, "/Event", ct);
        var refused = await officer.PostAsync($"/Event/Publish?id={missingCoverId}", EventDraftTestSupport.Form(new() { ["__RequestVerificationToken"] = refusalToken }), ct);
        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);

        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var persisted = await context.Events.AsNoTracking().SingleAsync(row => row.EventId == publishableId, ct);
        Assert.Equal(EventStatusCode.Published, persisted.EventStatusId);
        Assert.Equal(officerId, persisted.PublishedBy);
        Assert.Equal(officerId, persisted.UpdatedBy);
        var audit = await context.ActivityLogs.AsNoTracking().SingleAsync(row => row.EntityType == "events" && row.EntityId == publishableId, ct);
        Assert.Equal(AuditActions.EventPublished, audit.Action);
        Assert.Equal(officerId, audit.ActorUserId);
        Assert.Equal(EventStatusCode.Draft, (await context.Events.AsNoTracking().SingleAsync(row => row.EventId == missingCoverId, ct)).EventStatusId);
        Assert.Equal(0, await context.ActivityLogs.CountAsync(row => row.EntityType == "events" && row.EntityId == missingCoverId, ct));

        await CreateFailingPublicationAuditTriggerAsync(ct);
        try
        {
            var rollbackToken = await EventDraftTestSupport.AntiforgeryTokenAsync(officer, "/Event", ct);
            var rollback = await officer.PostAsync($"/Event/Publish?id={rollbackId}", EventDraftTestSupport.Form(new() { ["__RequestVerificationToken"] = rollbackToken }), ct);
            Assert.Equal(HttpStatusCode.InternalServerError, rollback.StatusCode);
        }
        finally
        {
            await DropFailingPublicationAuditTriggerAsync(ct);
        }

        await using var fresh = EventDraftTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(EventStatusCode.Draft, (await fresh.Events.AsNoTracking().SingleAsync(row => row.EventId == rollbackId, ct)).EventStatusId);
        Assert.Equal(0, await fresh.ActivityLogs.CountAsync(row => row.EntityType == "events" && row.EntityId == rollbackId, ct));

        var facts = await SeedFactDataAsync(officerId, typeId, venueId, ct);
        await using var factContext = EventDraftTestSupport.Context(fixture.ConnectionString);
        Assert.Equal(new[] { facts.EligibleMemberId }, await ParticipationFacts.EligibleParticipantMemberIds(factContext.Users, factContext.Members).ToArrayAsync(ct));
        Assert.Equal(new[] { facts.ValidAttendanceId }, await ParticipationFacts.ValidAttendances(factContext.AttendanceRecords).Select(row => row.AttendanceId).ToArrayAsync(ct));
        Assert.Equal(new[] { facts.ConfirmedRegistrationIds[0], facts.ConfirmedRegistrationIds[1], facts.ConfirmedRegistrationIds[2] }, await ParticipationFacts.SeatHolders(factContext.Registrations).Where(row => row.EventId == facts.CompletedEventId).Select(row => row.RegistrationId).OrderBy(id => id).ToArrayAsync(ct));
        Assert.Equal(new[] { facts.WaitlistedRegistrationIds[1], facts.WaitlistedRegistrationIds[0] }, await ParticipationFacts.WaitlistPriority(factContext.Registrations, facts.WaitlistEventId).Select(row => row.RegistrationId).ToArrayAsync(ct));
        Assert.Empty(await factContext.Events.Where(ParticipationFacts.StartCutoffPassed(facts.Now.AddTicks(-1))).Where(row => row.EventId == facts.CompletedEventId).ToListAsync(ct));
        Assert.Single(await factContext.Events.Where(ParticipationFacts.StartCutoffPassed(facts.Now)).Where(row => row.EventId == facts.CompletedEventId).ToListAsync(ct));
        Assert.Equal(new[] { facts.PublishedEventId }, await factContext.Events.Where(ParticipationFacts.SignupOpen(facts.Now)).Where(row => row.EventId == facts.PublishedEventId).Select(row => row.EventId).ToArrayAsync(ct));
        Assert.Equal(new[] { facts.PublishableEventId }, await ParticipationFacts.PublishableEventIds(factContext.Events, factContext.EventTypes, factContext.Venues, facts.Now).Where(id => id == facts.PublishableEventId).ToArrayAsync(ct));
        Assert.Equal(new[] { facts.ConfirmedRegistrationIds[1], facts.ConfirmedRegistrationIds[2] }, await ParticipationFacts.NoShows(factContext.Registrations, factContext.Events, factContext.AttendanceRecords).Where(row => row.EventId == facts.CompletedEventId).Select(row => row.RegistrationId).OrderBy(id => id).ToArrayAsync(ct));
        var rate = Assert.Single(await ParticipationFacts.AttendanceRates(factContext.Events.Where(row => row.EventId == facts.CompletedEventId), factContext.Registrations, factContext.AttendanceRecords).ToListAsync(ct));
        Assert.InRange(rate!.Value, 0.333333m, 0.333334m);

        var reportFacts = await new ParticipationReportRepository(new UnitOfWork(factContext))
            .GetSummaryFactsAsync(facts.CompletedEventId, ct);
        Assert.Equal(3, reportFacts.ConfirmedCount);
        Assert.Equal(1, reportFacts.ValidAttendeeCount);
        Assert.Equal(0, reportFacts.PendingCount);
        Assert.Equal(0, reportFacts.WaitlistedCount);
        Assert.Equal(0, reportFacts.CancelledCount);
        Assert.InRange(reportFacts.AttendanceRate!.Value, 0.333333m, 0.333334m);
    }

    private async Task<int> SeedEventAsync(int actorId, int typeId, int venueId, string? imagePath, CancellationToken cancellationToken)
    {
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var eventItem = new Event
        {
            Title = $"Publication test {Guid.NewGuid():N}",
            TypeId = typeId,
            VenueId = venueId,
            StartsAt = new DateTime(2099, 12, 10, 9, 0, 0, DateTimeKind.Utc),
            RegistrationDeadline = new DateTime(2099, 12, 9, 9, 0, 0, DateTimeKind.Utc),
            ParticipantLimit = 10,
            EventStatusId = EventStatusCode.Draft,
            ImagePath = imagePath,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow
        };
        context.Events.Add(eventItem);
        await context.SaveChangesAsync(cancellationToken);
        return eventItem.EventId;
    }

    private async Task<FactData> SeedFactDataAsync(int actorId, int typeId, int venueId, CancellationToken cancellationToken)
    {
        var now = new DateTime(2040, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var users = Enumerable.Range(1, 3).Select(index => new User
        {
            Name = $"Fact user {index}", UserId = $"fact-user-{Guid.NewGuid():N}@attendta.test", Password = "test",
            RoleId = RoleCode.Member, IsActive = index != 3
        }).ToArray();
        context.Users.AddRange(users);
        await context.SaveChangesAsync(cancellationToken);
        var members = users.Select((user, index) => new Member
        {
            UserId = user.Id, StudentNumber = $"FACT-{Guid.NewGuid():N}", Course = "BSIT", YearLevel = 1,
            Section = "A", MembershipStatusId = index == 1 ? MembershipStatusCode.Inactive : MembershipStatusCode.Active,
            CreatedBy = actorId, CreatedAt = now
        }).ToArray();
        context.Members.AddRange(members);
        var completed = FactEvent("completed", typeId, venueId, actorId, now, EventStatusCode.Completed, now, now);
        var published = FactEvent("published", typeId, venueId, actorId, now, EventStatusCode.Published, now.AddDays(2), now.AddDays(1));
        var publishable = FactEvent("publishable", typeId, venueId, actorId, now, EventStatusCode.Draft, now.AddDays(2), now.AddDays(1));
        var waitlist = FactEvent("waitlist", typeId, venueId, actorId, now, EventStatusCode.Published, now.AddDays(2), now.AddDays(1));
        context.Events.AddRange(completed, published, publishable, waitlist);
        await context.SaveChangesAsync(cancellationToken);
        var confirmed = members.Select(member => new Registration
        {
            MemberId = member.MemberId, EventId = completed.EventId, RegistrationStatusId = RegistrationStatusCode.Confirmed,
            CourseSnapshot = "BSIT", YearLevelSnapshot = 1, SectionSnapshot = "A", SubmittedAt = now, CreatedBy = actorId, CreatedAt = now
        }).ToArray();
        var waitlisted = new[]
        {
            Registration(members[0].MemberId, waitlist.EventId, actorId, now, RegistrationStatusCode.Waitlisted, now.AddMinutes(2)),
            Registration(members[1].MemberId, waitlist.EventId, actorId, now, RegistrationStatusCode.Waitlisted, now.AddMinutes(1))
        };
        context.Registrations.AddRange(confirmed);
        context.Registrations.AddRange(waitlisted);
        await context.SaveChangesAsync(cancellationToken);
        var valid = new Attendance { RegistrationId = confirmed[0].RegistrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = actorId, RecordedAt = now };
        context.AttendanceRecords.AddRange(valid, new Attendance { RegistrationId = confirmed[1].RegistrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = actorId, RecordedAt = now, InvalidatedBy = actorId, InvalidatedAt = now, InvalidationReason = "test" });
        await context.SaveChangesAsync(cancellationToken);
        return new FactData(now, members[0].MemberId, valid.AttendanceId, completed.EventId, published.EventId, publishable.EventId, waitlist.EventId,
            confirmed.Select(row => row.RegistrationId).ToArray(), waitlisted.Select(row => row.RegistrationId).ToArray());
    }

    private static Event FactEvent(string name, int typeId, int venueId, int actorId, DateTime now, EventStatusCode status, DateTime startsAt, DateTime deadline) => new()
    {
        Title = $"Fact {name} {Guid.NewGuid():N}", TypeId = typeId, VenueId = venueId, StartsAt = startsAt,
        RegistrationDeadline = deadline, ParticipantLimit = 10, EventStatusId = status,
        ImagePath = "uploads/events/22222222222222222222222222222222.jpg", CreatedBy = actorId, CreatedAt = now
    };

    private static Registration Registration(int memberId, int eventId, int actorId, DateTime now, RegistrationStatusCode status, DateTime reviewedAt) => new()
    {
        MemberId = memberId, EventId = eventId, RegistrationStatusId = status, CourseSnapshot = "BSIT", YearLevelSnapshot = 1,
        SectionSnapshot = "A", SubmittedAt = now, ReviewedAt = reviewedAt, CreatedBy = actorId, CreatedAt = now
    };

    private sealed record FactData(DateTime Now, int EligibleMemberId, int ValidAttendanceId, int CompletedEventId, int PublishedEventId,
        int PublishableEventId, int WaitlistEventId, int[] ConfirmedRegistrationIds, int[] WaitlistedRegistrationIds);

    private async Task CreateFailingPublicationAuditTriggerAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER dbo.TR_TestFailEventPublicationAudit ON dbo.activity_logs AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE action = 'event.published') THROW 51053, 'Test-only publication audit failure.', 1; END;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task DropFailingPublicationAuditTriggerAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "IF OBJECT_ID(N'dbo.TR_TestFailEventPublicationAudit', N'TR') IS NOT NULL DROP TRIGGER dbo.TR_TestFailEventPublicationAudit;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
