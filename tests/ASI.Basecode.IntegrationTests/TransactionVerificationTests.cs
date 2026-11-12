using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public class TransactionVerificationTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private AsiBasecodeDBContext Context() => new(new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);
    private static string Email() => $"{Guid.NewGuid():N}@example.invalid";
    private static User User(string email) => new() { Name = "Test", UserId = email, Password = "test", IsActive = true, RoleId = RoleCode.Member };
    private static AccountRequest Request(string email) => new() { StudentNumber = Guid.NewGuid().ToString("N"), FullName = "Test", Email = email, PasswordHash = "test", Course = "Computing", YearLevel = 1, Section = "A", AccountRequestStatusId = AccountRequestStatusCode.Pending };
    private sealed class Actor(int id, bool authenticated = true) : ICurrentActor { public bool IsAuthenticated => authenticated; public int UserId => id; public string Role => "member"; }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero); }
    private sealed record Values(string Status, int Count) : IAuditValues;
    private sealed record Nested(object Value) : IAuditValues;
    private sealed record Renamed([property: JsonPropertyName("api_secret")] string Safe) : IAuditValues;
    private sealed record ClrName([property: JsonPropertyName("safe")] string PasswordHash) : IAuditValues;
    private sealed class DerivedActivityLog : ActivityLog, IAuditValues;
    private static AuditEntry Entry(IAuditValues? values = null) => new(AuditActions.AccountRequestApproved, "account_requests", 42, NewValues: values);

    [Fact]
    public async Task NestedWorkIsInvisibleUntilOwnerCommitsAndRollsBackWithOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var owner = Context();
        await using var observer = Context();
        var uow = new UnitOfWork(owner);
        var actor = User(Email()); owner.Users.Add(actor); await uow.SaveChangesAsync(ct);
        var writer = new ActivityLogWriter(owner, new Actor(actor.Id), new Clock());
        var email = Email();
        await uow.ExecuteInTransactionAsync(async outer =>
        {
            await uow.ExecuteInTransactionAsync(async inner => { owner.Users.Add(User(email)); writer.Append(Entry(new Values("approved", 2))); Assert.Equal(DateTimeKind.Utc, owner.ChangeTracker.Entries<ActivityLog>().Single().Entity.OccurredAt.Kind); await uow.SaveChangesAsync(inner); }, outer);
            Assert.False(await observer.Users.AnyAsync(u => u.UserId == email, outer));
            Assert.Equal(0, await observer.ActivityLogs.CountAsync(a => a.ActorUserId == actor.Id, outer));
        }, ct);
        Assert.True(await observer.Users.AnyAsync(u => u.UserId == email, ct));
        var audit = await observer.ActivityLogs.SingleAsync(a => a.ActorUserId == actor.Id, ct);
        Assert.Equal(ActorTypeCode.User, audit.ActorTypeId); Assert.Equal(actor.Id, audit.ActorUserId);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), audit.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, audit.OccurredAt.Kind);
        Assert.Equal("Account request approved.", audit.Description); Assert.Null(audit.Reason);
        Assert.Equal("{\"Status\":\"approved\",\"Count\":2}", audit.NewValues);
        var rollbackEmail = Email();
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async outer =>
        {
            await uow.ExecuteInTransactionAsync(async inner => { owner.Users.Add(User(rollbackEmail)); writer.Append(Entry()); await uow.SaveChangesAsync(inner); }, outer);
            throw new InvalidOperationException();
        }, ct));
        Assert.Empty(owner.ChangeTracker.Entries());
        Assert.False(await observer.Users.AnyAsync(u => u.UserId == rollbackEmail, ct));
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(a => a.ActorUserId == actor.Id, ct));
    }

    [Fact]
    public async Task AuditRefusesAnonymousUnsafeAndUnknownInputsWithoutAppending()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context(); var uow = new UnitOfWork(context);
        var actor = User(Email()); context.Users.Add(actor); await uow.SaveChangesAsync(ct);
        var writer = new ActivityLogWriter(context, new Actor(actor.Id), new Clock());
        Assert.Throws<InvalidOperationException>(() => writer.Append(Entry()));
        await uow.ExecuteInTransactionAsync(_ =>
        {
            Assert.Throws<InvalidOperationException>(() => new ActivityLogWriter(context, new Actor(0, false), new Clock()).Append(Entry()));
            Assert.Throws<ArgumentException>(() => writer.Append(Entry(new Renamed("x"))));
            Assert.Throws<ArgumentException>(() => writer.Append(Entry(new ClrName("x"))));
            Assert.Throws<ArgumentException>(() => writer.Append(Entry(new Nested(new { Inner = new { secret_key = "x" } }))));
            Assert.Throws<ArgumentException>(() => writer.Append(Entry(new Nested(new { Inner = new { file_path = "x" } }))));
            Assert.Throws<ArgumentException>(() => writer.Append(Entry(new Nested(new { Inner = new { qr_token = "x" } }))));
            Assert.Throws<ArgumentException>(() => writer.Append(Entry(new Nested(actor))));
            Assert.Throws<ArgumentException>(() => writer.Append(new AuditEntry("unknown", "account_requests", 1)));
            Assert.Throws<ArgumentException>(() => writer.Append(new AuditEntry(AuditActions.AccountRequestApproved, "users", 1)));
            Assert.Empty(context.ChangeTracker.Entries<ActivityLog>());
            return Task.CompletedTask;
        }, ct);
    }

    [Fact]
    public async Task AuditRejectsUnmappedEntitySubclassesAtRootAndNested()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context();
        Assert.Null(context.Model.FindEntityType(typeof(DerivedActivityLog)));
        var writer = new ActivityLogWriter(context, new Actor(1), new Clock());
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var entity = new DerivedActivityLog();
        Assert.Equal("Entity payloads are not permitted.", Assert.Throws<ArgumentException>(() => writer.Append(Entry(entity))).Message);
        Assert.Equal("Entity payloads are not permitted.", Assert.Throws<ArgumentException>(() => writer.Append(Entry(new Nested(new { Inner = entity })))).Message);
        Assert.Empty(context.ChangeTracker.Entries<ActivityLog>());
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    public async Task BothSqlDuplicateCodesAreTranslatedAtTransactionAndSaveBoundaries(int code)
    {
        var ct = TestContext.Current.CancellationToken;
        var sql = code == 2601 ? "CREATE TABLE #d (id int NOT NULL); CREATE UNIQUE INDEX ux_d ON #d(id); INSERT #d VALUES (1); INSERT #d VALUES (1);" : "CREATE TABLE #d (id int NOT NULL PRIMARY KEY); INSERT #d VALUES (1); INSERT #d VALUES (1);";
        await using (var probe = new SqlConnection(fixture.ConnectionString)) { await probe.OpenAsync(ct); await using var command = new SqlCommand(sql, probe); Assert.Equal(code, (await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync(ct))).Number); }
        await using (var context = Context()) { var failure = await Assert.ThrowsAsync<DomainException>(() => new UnitOfWork(context).ExecuteInTransactionAsync(async token => await context.Database.ExecuteSqlRawAsync(sql, token), ct)); Assert.Equal(OutcomeCode.Duplicate, failure.Outcome); Assert.Null(failure.InnerException); }
        var email = Email();
        await using (var seed = Context()) { if (code == 2601) seed.AccountRequests.Add(Request(email)); else seed.Users.Add(User(email)); await seed.SaveChangesAsync(ct); }
        await using (var probe = Context()) { if (code == 2601) probe.AccountRequests.Add(Request(email)); else probe.Users.Add(User(email)); var ex = await Assert.ThrowsAsync<DbUpdateException>(() => probe.SaveChangesAsync(ct)); Assert.Equal(code, Assert.IsType<SqlException>(ex.InnerException).Number); }
        await using (var asyncContext = Context()) { if (code == 2601) asyncContext.AccountRequests.Add(Request(email)); else asyncContext.Users.Add(User(email)); Assert.Equal(OutcomeCode.Duplicate, (await Assert.ThrowsAsync<DomainException>(() => new UnitOfWork(asyncContext).SaveChangesAsync(ct))).Outcome); }
        await using (var syncContext = Context()) { if (code == 2601) syncContext.AccountRequests.Add(Request(email)); else syncContext.Users.Add(User(email)); Assert.Equal(OutcomeCode.Duplicate, Assert.Throws<DomainException>(() => new UnitOfWork(syncContext).SaveChanges()).Outcome); }
    }

    [Fact]
    public async Task OtherSqlErrorsArePreserved()
    {
        var ct = TestContext.Current.CancellationToken; await using var context = Context(); var uow = new UnitOfWork(context);
        var sql = await Assert.ThrowsAsync<SqlException>(() => uow.ExecuteInTransactionAsync(async token => await context.Database.ExecuteSqlRawAsync("SELECT 1 / 0", token), ct)); Assert.Equal(8134, sql.Number);
        var user = User(Email()); user.Name = null!;
        var wrapped = await Assert.ThrowsAsync<DbUpdateException>(() => uow.ExecuteInTransactionAsync(async token => { context.Users.Add(user); await uow.SaveChangesAsync(token); }, ct));
        Assert.Equal(515, Assert.IsType<SqlException>(wrapped.InnerException).Number); Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task LocksWaitRefreshTrackedRowsAndHonorCancellation()
    {
        var ct = TestContext.Current.CancellationToken; await using var first = Context(); await using var second = Context();
        var user = User(Email()); var request = Request(Email()); first.Users.Add(user); first.AccountRequests.Add(request); await first.SaveChangesAsync(ct);
        var firstUow = new UnitOfWork(first); var secondUow = new UnitOfWork(second); var users = new UserRepository(firstUow); var requests = new AccountRequestRepository(firstUow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => users.LockUserAsync(user.Id, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => requests.LockAccountRequestAsync(request.RequestId, ct));
        await second.Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Updated"), ct);
        await second.AccountRequests.Where(x => x.RequestId == request.RequestId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FullName, "Updated"), ct);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = firstUow.ExecuteInTransactionAsync(async token =>
        {
            Assert.Null(await requests.LockAccountRequestAsync(int.MaxValue, token)); Assert.Same(request, await requests.LockAccountRequestAsync(request.RequestId, token)); Assert.Equal("Updated", request.FullName);
            Assert.Null(await users.LockUserAsync(int.MaxValue, token)); Assert.Same(user, await users.LockUserAsync(user.Id, token)); Assert.Equal("Updated", user.Name);
            acquired.SetResult(); await release.Task.WaitAsync(token);
        }, ct);
        await acquired.Task.WaitAsync(ct);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var waiting = secondUow.ExecuteInTransactionAsync(token => new UserRepository(secondUow).LockUserAsync(user.Id, token), cancellation.Token);
            Assert.NotSame(waiting, await Task.WhenAny(waiting, Task.Delay(300, ct))); cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            var successor = secondUow.ExecuteInTransactionAsync(token => new UserRepository(secondUow).LockUserAsync(user.Id, token), ct);
            Assert.NotSame(successor, await Task.WhenAny(successor, Task.Delay(300, ct))); release.SetResult(); await owner; Assert.NotNull(await successor);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task ExternalTransactionIsRolledBackOnCaughtNestedFailure()
    {
        var ct = TestContext.Current.CancellationToken; await using var context = Context(); await using var observer = Context();
        var uow = new UnitOfWork(context); var email = Email(); var original = new InvalidOperationException("nested");
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async token =>
        {
            context.Users.Add(User(email)); await uow.SaveChangesAsync(token);
            try { await uow.ExecuteInTransactionAsync(_ => Task.FromException(original), token); } catch (InvalidOperationException) { }
        }, ct));
        Assert.Same(original, thrown); Assert.Empty(context.ChangeTracker.Entries());
        await Assert.ThrowsAnyAsync<Exception>(() => transaction.CommitAsync(ct));
        Assert.False(await observer.Users.AnyAsync(x => x.UserId == email, ct));
    }

    [Fact]
    public async Task ExternalTransactionRemainsCallerOwnedAndNoImplicitSaveOccurs()
    {
        var ct = TestContext.Current.CancellationToken; await using var context = Context(); await using var observer = Context();
        var uow = new UnitOfWork(context); var saved = Email(); var unsaved = Email();
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await uow.ExecuteInTransactionAsync(async token => { context.Users.Add(User(saved)); await uow.SaveChangesAsync(token); }, ct);
        await uow.ExecuteInTransactionAsync(_ => { context.Users.Add(User(unsaved)); return Task.CompletedTask; }, ct);
        Assert.False(await observer.Users.AnyAsync(x => x.UserId == saved, ct));
        Assert.Equal(EntityState.Added, context.ChangeTracker.Entries<User>().Single(x => x.Entity.UserId == unsaved).State);
        await transaction.CommitAsync(ct);
        Assert.True(await observer.Users.AnyAsync(x => x.UserId == saved, ct));
        Assert.False(await observer.Users.AnyAsync(x => x.UserId == unsaved, ct));
    }

    [Fact]
    public async Task HttpRequestCorrelationIdAppearsInConfiguredFileSink()
    {
        var ct = TestContext.Current.CancellationToken; await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString, "Information");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/Account/Login?do_not_log=private-marker", ct);
        var id = Assert.Single(response.Headers.GetValues("X-Correlation-ID")); Assert.True(Guid.TryParseExact(id, "N", out _));
        var files = Directory.GetFiles(Path.Combine(factory.SetupRoot, "logs"), "log-*.txt", SearchOption.AllDirectories); Assert.NotEmpty(files);
        var contents = ""; foreach (var file in files) { await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var reader = new StreamReader(stream); contents += await reader.ReadToEndAsync(ct); }
        Assert.Contains($"[{id}] Request completed with status", contents); Assert.DoesNotContain("private-marker", contents);
    }
}
