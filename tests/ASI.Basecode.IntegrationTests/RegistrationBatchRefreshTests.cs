using System.Data.Common;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class RegistrationBatchRefreshTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task ReviewQueueUsesOneCountAndOnePageQueryForMultipleRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actorId, ct);
        var start = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actorId, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Review read count", ct);
        for (var i = 0; i < 4; i++)
        {
            var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"read-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
            var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
            await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, actorId, ct);
        }
        var counter = new ReadCounter { Counting = true };
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, counter);
        var repository = new RegistrationRepository(new UnitOfWork(context), new ListReadOptions { Search = "Review read count" });
        Assert.Equal(4, await repository.CountReviewQueueAsync(RegistrationStatusCode.Confirmed, eventItem.EventId, ct));
        var rows = await repository.GetReviewQueueAsync(RegistrationStatusCode.Confirmed, eventItem.EventId, 0, 10, ct);
        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.False(row.HasValidAttendance));
        Assert.Equal(2, counter.Reads);
        Assert.Contains("COUNT", counter.Commands[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIKE", counter.Commands[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attendance", counter.Commands[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("attendance", counter.Commands[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BatchRefreshUsesOneReadAndPreservesTrackedEditsDuringCancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, actorId, ct);
        var start = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, actorId, typeId, venueId,
            EventStatusCode.Published, start, start.AddDays(-1), 20, null, "Batch refresh", ct);
        var ids = new List<int>();
        foreach (var status in new[] { RegistrationStatusCode.Confirmed, RegistrationStatusCode.Pending, RegistrationStatusCode.Waitlisted, RegistrationStatusCode.Cancelled })
        {
            var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, $"batch-{Guid.NewGuid():N}@test.invalid", MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
            var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
            var row = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString, eventItem.EventId, member.MemberId, status, actorId, ct);
            ids.Add(row.RegistrationId);
        }

        var counter = new ReadCounter();
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString, counter);
        var uow = new UnitOfWork(context);
        var repository = new RegistrationRepository(uow);
        var actor = new Actor(actorId);
        var clock = TimeProvider.System;
        var cancellation = new RegistrationCancellationService(repository, new EventRepository(uow), uow, actor, new ActivityLogWriter(context, actor, clock), clock);
        await uow.ExecuteInTransactionAsync(async token =>
        {
            await new EventRepository(uow).LockEventAsync(eventItem.EventId, token);
            var tracked = await context.Registrations.SingleAsync(row => row.RegistrationId == ids[0], token);
            tracked.ReviewNotes = "unsaved review";
            tracked.RegistrationStatusId = RegistrationStatusCode.Cancelled;
            counter.Counting = true;
            var open = await repository.GetOpenForEventAsync(eventItem.EventId, token);
            counter.Counting = false;

            Assert.Equal(1, counter.Reads);
            Assert.Equal(ids.Take(3), open.Select(row => row.RegistrationId));
            Assert.Same(tracked, open[0]);
            Assert.Equal(RegistrationStatusCode.Confirmed, tracked.RegistrationStatusId);
            Assert.Equal("unsaved review", tracked.ReviewNotes);

            await cancellation.CancelAllForEventAsync(eventItem.EventId, CancellationCause.EventCancelled, "event cancelled", token);
        }, ct);

        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        var rows = await observer.Registrations.AsNoTracking().Where(row => row.EventId == eventItem.EventId).OrderBy(row => row.RegistrationId).ToListAsync(ct);
        Assert.All(rows, row => Assert.Equal(RegistrationStatusCode.Cancelled, row.RegistrationStatusId));
        Assert.Equal("unsaved review", rows[0].ReviewNotes);
        Assert.Equal("event cancelled", rows[0].CancellationReason);
        Assert.Equal(3, await observer.ActivityLogs.CountAsync(log => log.EntityType == "registrations" && ids.Contains(log.EntityId), ct));
    }

    private sealed class Actor(int id) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => "officer";
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public bool Counting { get; set; }
        public int Reads { get; private set; }
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Counting) { Reads++; Commands.Add(command.CommandText); }
            return ValueTask.FromResult(result);
        }
    }
}
