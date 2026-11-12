using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class PublishedEventEditFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task PublishedEditEnforcesSecurityAndTerminalStatesAndRollsBackAuditFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"edit-officer-{suffix}@attendta.test";
        var memberEmail = $"edit-member-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var officerId = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (typeId, venueId) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, officerId, 30, ct);
        int eventId;
        await using (var seed = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = new Event
            {
                Title = "Original", Description = "Before", TypeId = typeId, VenueId = venueId,
                StartsAt = new DateTime(2026, 12, 10, 1, 0, 0, 123),
                RegistrationDeadline = new DateTime(2026, 12, 9, 9, 30, 0, 456),
                ParticipantLimit = 10, EventStatusId = EventStatusCode.Published,
                CreatedBy = officerId, CreatedAt = DateTime.UtcNow
            };
            seed.Events.Add(row);
            await seed.SaveChangesAsync(ct);
            eventId = row.EventId;
            (await seed.EventTypes.SingleAsync(x => x.TypeId == typeId, ct)).IsActive = false;
            (await seed.Venues.SingleAsync(x => x.VenueId == venueId, ct)).IsActive = false;
            await seed.SaveChangesAsync(ct);
        }

        Dictionary<string, string> Values(string title) => new()
        {
            ["EventId"] = eventId.ToString(CultureInfo.InvariantCulture), ["Title"] = title,
            ["Description"] = "After", ["TypeId"] = typeId.ToString(CultureInfo.InvariantCulture),
            ["VenueId"] = venueId.ToString(CultureInfo.InvariantCulture),
            ["StartsAt"] = "2026-12-10T09:00:00.123", ["RegistrationDeadline"] = "2026-12-09T17:30:00.456",
            ["ParticipantLimit"] = "10", ["EventStatusId"] = ((int)EventStatusCode.Draft).ToString(CultureInfo.InvariantCulture)
        };

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var member = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await EventDraftTestSupport.SignInAsync(officer, officerEmail, password, ct);
        await EventDraftTestSupport.SignInAsync(member, memberEmail, password, ct);
        var memberValues = Values("Forbidden");
        memberValues["__RequestVerificationToken"] = await EventDraftTestSupport.AntiforgeryTokenAsync(member, "/Home/Index", ct);
        var denied = await member.PostAsync("/Event/Edit", EventDraftTestSupport.Form(memberValues), ct);
        Assert.Contains("/Account/AccessDenied", EventDraftTestSupport.LocationPath(denied), StringComparison.Ordinal);
        var anon = await anonymous.PostAsync("/Event/Edit", EventDraftTestSupport.Form(Values("Anonymous")), ct);
        Assert.Contains("/Account/Login", EventDraftTestSupport.LocationPath(anon), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, (await officer.PostAsync("/Event/Edit", EventDraftTestSupport.Form(Values("No token")), ct)).StatusCode);

        var page = await officer.GetStringAsync($"/Event/Edit?id={eventId}", ct);
        Assert.Contains("2026-12-10T09:00:00.1230000", page, StringComparison.Ordinal);
        Assert.Contains("Active Type", page, StringComparison.Ordinal);
        var token = await EventDraftTestSupport.AntiforgeryTokenAsync(officer, "/Event", ct);
        var invalid = Values("");
        invalid["__RequestVerificationToken"] = token;
        Assert.Equal(HttpStatusCode.OK, (await officer.PostAsync("/Event/Edit", EventDraftTestSupport.Form(invalid), ct)).StatusCode);
        await AssertStateAsync("Original", "Before", 0, EventStatusCode.Published, ct);

        var valid = Values("Edited");
        valid["__RequestVerificationToken"] = token;
        Assert.Equal(HttpStatusCode.Redirect, (await officer.PostAsync("/Event/Edit", EventDraftTestSupport.Form(valid), ct)).StatusCode);
        await AssertStateAsync("Edited", "After", 1, EventStatusCode.Published, ct);
        await using (var observer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var audit = await observer.ActivityLogs.AsNoTracking().SingleAsync(x => x.EntityType == "events" && x.EntityId == eventId, ct);
            Assert.Equal(AuditActions.EventUpdated, audit.Action);
            Assert.Contains("\"Title\":\"Original\"", audit.OldValues, StringComparison.Ordinal);
            Assert.Contains("\"Description\":\"Before\"", audit.OldValues, StringComparison.Ordinal);
            Assert.Contains("\"Title\":\"Edited\"", audit.NewValues, StringComparison.Ordinal);
            Assert.Contains("\"Description\":\"After\"", audit.NewValues, StringComparison.Ordinal);
            Assert.DoesNotContain("uploads/", audit.NewValues, StringComparison.OrdinalIgnoreCase);
        }

        // A check constraint fails the actual audit INSERT, without SQL Server rejecting EF's OUTPUT clause first.
        await ExecuteSqlAsync("ALTER TABLE dbo.activity_logs ADD CONSTRAINT CK_TestRejectPublishedEditAudit CHECK (action <> 'event.updated' OR new_values NOT LIKE '%Must roll back%');", ct);
        try
        {
            var fail = Values("Must roll back");
            fail["__RequestVerificationToken"] = token;
            Assert.Equal(HttpStatusCode.InternalServerError, (await officer.PostAsync("/Event/Edit", EventDraftTestSupport.Form(fail), ct)).StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync("ALTER TABLE dbo.activity_logs DROP CONSTRAINT CK_TestRejectPublishedEditAudit;", ct);
        }
        await AssertStateAsync("Edited", "After", 1, EventStatusCode.Published, ct);

        // EF batches the audit INSERT before the event UPDATE; fail the latter to prove the
        // already-inserted audit row rolls back with it as well.
        await ExecuteSqlAsync("ALTER TABLE dbo.events ADD CONSTRAINT CK_TestRejectPublishedEdit CHECK (title <> 'Reject event update');", ct);
        try
        {
            var fail = Values("Reject event update");
            fail["__RequestVerificationToken"] = token;
            Assert.Equal(HttpStatusCode.InternalServerError, (await officer.PostAsync("/Event/Edit", EventDraftTestSupport.Form(fail), ct)).StatusCode);
        }
        finally
        {
            await ExecuteSqlAsync("ALTER TABLE dbo.events DROP CONSTRAINT CK_TestRejectPublishedEdit;", ct);
        }
        await AssertStateAsync("Edited", "After", 1, EventStatusCode.Published, ct);

        foreach (var status in new[] { EventStatusCode.Completed, EventStatusCode.Cancelled })
        {
            await using (var change = EventDraftTestSupport.Context(fixture.ConnectionString))
            {
                (await change.Events.SingleAsync(x => x.EventId == eventId, ct)).EventStatusId = status;
                await change.SaveChangesAsync(ct);
            }
            Assert.Equal(HttpStatusCode.Redirect, (await officer.GetAsync($"/Event/Edit?id={eventId}", ct)).StatusCode);
            var terminal = Values("Terminal mutation");
            terminal["__RequestVerificationToken"] = token;
            Assert.Equal(HttpStatusCode.Redirect, (await officer.PostAsync("/Event/Edit", EventDraftTestSupport.Form(terminal), ct)).StatusCode);
            await AssertStateAsync("Edited", "After", 1, status, ct);
        }

        async Task AssertStateAsync(string title, string description, int audits, EventStatusCode status, CancellationToken cancellationToken)
        {
            await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
            var row = await observer.Events.AsNoTracking().SingleAsync(x => x.EventId == eventId, cancellationToken);
            Assert.Equal(title, row.Title);
            Assert.Equal(description, row.Description);
            Assert.Equal(status, row.EventStatusId);
            Assert.Equal(10, row.ParticipantLimit);
            Assert.Equal(typeId, row.TypeId);
            Assert.Equal(venueId, row.VenueId);
            Assert.Equal(new DateTime(2026, 12, 10, 1, 0, 0, 123), row.StartsAt);
            Assert.Equal(new DateTime(2026, 12, 9, 9, 30, 0, 456), row.RegistrationDeadline);
            Assert.Equal(audits == 0 ? null : officerId, row.UpdatedBy);
            Assert.Equal(audits, await observer.ActivityLogs.CountAsync(x => x.EntityType == "events" && x.EntityId == eventId, cancellationToken));
        }
    }

    private async Task ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}
