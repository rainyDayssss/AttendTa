using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class TransactionAuditTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private AsiBasecodeDBContext Context() => new(new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    private sealed class Actor : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId { get; set; }
        public string Role => "admin";
    }
    private sealed record Values(string Status) : IAuditValues;

    [Fact]
    public async Task CommitAndRollbackIncludeAuditAndClearTracker()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context();
        var uow = new UnitOfWork(context);
        var actor = new Actor();
        var writer = new ActivityLogWriter(context, actor, TimeProvider.System);
        var email = $"{Guid.NewGuid():N}@example.invalid";
        await uow.ExecuteInTransactionAsync(async ct =>
        {
            var user = new User { Name = "Test", UserId = email, Password = "test-hash", IsActive = true, RoleId = RoleCode.Admin };
            context.Users.Add(user);
            await uow.SaveChangesAsync(ct);
            actor.UserId = user.Id;
            writer.Append(new AuditEntry(AuditActions.AccountRequestApproved, "account_requests", user.Id, NewValues: new Values("active")));
            await uow.SaveChangesAsync(ct);
        }, ct);
        Assert.Equal(1, await context.ActivityLogs.CountAsync(x => x.ActorUserId == actor.UserId, ct));

        var original = new InvalidOperationException("rollback");
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async ct =>
        {
            writer.Append(new AuditEntry(AuditActions.AccountRequestApproved, "account_requests", actor.UserId));
            await uow.SaveChangesAsync(ct);
            throw original;
        }, ct));
        Assert.Same(original, thrown);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(1, await context.ActivityLogs.CountAsync(x => x.ActorUserId == actor.UserId, ct));
    }

    [Fact]
    public async Task CaughtNestedFailureStillRollsBackAndRethrowsOriginal()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context();
        var uow = new UnitOfWork(context);
        var original = new InvalidOperationException("nested");
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async ct =>
        {
            try { await uow.ExecuteInTransactionAsync(_ => Task.FromException(original), ct); }
            catch (InvalidOperationException) { }
        }, ct));
        Assert.Same(original, thrown);
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
