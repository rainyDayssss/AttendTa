using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class AccountRoleServiceTests
{
    private const int ActorId = 7;

    [Fact]
    public async Task PromotionWritesRoleStampsAndOneAuditRow()
    {
        var clock = new FixedClock { Now = new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.Zero) };
        var accounts = new FakeAccounts();
        var target = accounts.Add(42, "Ana Reyes", "ana@example.com", RoleCode.Member, isActive: true);
        var users = new FakeUsers(target);
        var audit = new RecordingAudit();
        var work = new ImmediateUnitOfWork();
        var service = Create(accounts, users, work, audit, clock);

        await service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "officer",
            Reason = "  Promoted to officer.  "
        }, TestContext.Current.CancellationToken);

        Assert.Equal(RoleCode.Officer, target.RoleId);
        Assert.Equal(ActorId, target.UpdatedById);
        Assert.Equal(clock.Now.UtcDateTime, target.UpdatedTime);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(2, work.SaveCount);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.UserRoleAssigned, entry.Action);
        Assert.Equal("users", entry.EntityType);
        Assert.Equal(42, entry.EntityId);
        Assert.Equal("Promoted to officer.", entry.Reason);
        Assert.Contains("\"RoleCode\":\"member\"", Json(entry.OldValues));
        Assert.Contains("\"RoleCode\":\"officer\"", Json(entry.NewValues));
    }

    [Fact]
    public async Task MissingRoleLookupFailsWithoutWritingTheChange()
    {
        var accounts = new FakeAccounts();
        var target = accounts.Add(42, "Ana Reyes", "ana@example.com", RoleCode.Member, isActive: true);
        var users = new FakeUsers(target) { RoleLookupMissing = true };
        var audit = new RecordingAudit();
        var work = new ImmediateUnitOfWork();
        var service = Create(accounts, users, work, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "officer",
            Reason = "Promote to officer."
        }, TestContext.Current.CancellationToken));

        // A broken role lookup is a data-integrity fault: no numeric fallback reaches the audit row.
        Assert.Equal(RoleCode.Member, target.RoleId);
        Assert.Empty(audit.Entries);
        Assert.Equal(0, work.SaveCount);
    }

    [Fact]
    public async Task NoOpChangeSucceedsWithoutWriteOrAudit()
    {
        var accounts = new FakeAccounts();
        var target = accounts.Add(42, "Ana Reyes", "ana@example.com", RoleCode.Officer, isActive: true);
        var users = new FakeUsers(target);
        var audit = new RecordingAudit();
        var work = new ImmediateUnitOfWork();
        var service = Create(accounts, users, work, audit);

        await service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "officer",
            Reason = "Already an officer."
        }, TestContext.Current.CancellationToken);

        Assert.Equal(RoleCode.Officer, target.RoleId);
        Assert.Null(target.UpdatedById);
        Assert.Null(target.UpdatedTime);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task LastActiveAdministratorCannotBeDemoted()
    {
        var accounts = new FakeAccounts { ActiveAdministratorCount = 1 };
        var target = accounts.Add(42, "Root Admin", "root@example.com", RoleCode.Admin, isActive: true);
        var users = new FakeUsers(target);
        var audit = new RecordingAudit();
        var work = new ImmediateUnitOfWork();
        var service = Create(accounts, users, work, audit);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "member",
            Reason = "Stepping down."
        }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.Contains("last active Administrator", error.Message, StringComparison.Ordinal);
        Assert.True(accounts.LockAcquired);
        Assert.Equal(RoleCode.Admin, target.RoleId);
        Assert.Null(target.UpdatedById);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task DemotionSucceedsWhenAnotherActiveAdministratorRemains()
    {
        var accounts = new FakeAccounts { ActiveAdministratorCount = 2 };
        var target = accounts.Add(42, "Second Admin", "second@example.com", RoleCode.Admin, isActive: true);
        var users = new FakeUsers(target);
        var audit = new RecordingAudit();
        var work = new ImmediateUnitOfWork();
        var service = Create(accounts, users, work, audit);

        await service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "member",
            Reason = "Handover complete."
        }, TestContext.Current.CancellationToken);

        Assert.True(accounts.LockAcquired);
        Assert.Equal(RoleCode.Member, target.RoleId);
        Assert.Equal(ActorId, target.UpdatedById);
        var entry = Assert.Single(audit.Entries);
        Assert.Contains("\"RoleCode\":\"admin\"", Json(entry.OldValues));
        Assert.Contains("\"RoleCode\":\"member\"", Json(entry.NewValues));
    }

    [Fact]
    public async Task InactiveAdministratorDemotionDoesNotTakeTheAppLock()
    {
        var accounts = new FakeAccounts { ActiveAdministratorCount = 1 };
        var target = accounts.Add(42, "Former Admin", "former@example.com", RoleCode.Admin, isActive: false);
        var users = new FakeUsers(target);
        var audit = new RecordingAudit();
        var work = new ImmediateUnitOfWork();
        var service = Create(accounts, users, work, audit);

        await service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "member",
            Reason = "Cleanup."
        }, TestContext.Current.CancellationToken);

        Assert.False(accounts.LockAcquired);
        Assert.Equal(RoleCode.Member, target.RoleId);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task PostLockAdministratorDemotionWithoutPreLockReadIsStale()
    {
        var accounts = new FakeAccounts();
        accounts.Add(42, "Promoted Meanwhile", "promoted@example.com", RoleCode.Member, isActive: true);
        var users = new FakeUsers(new User
        {
            Id = 42,
            UserId = "promoted@example.com",
            Name = "Promoted Meanwhile",
            RoleId = RoleCode.Admin,
            IsActive = true
        });
        var audit = new RecordingAudit();
        var work = new ImmediateUnitOfWork();
        var service = Create(accounts, users, work, audit);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "member",
            Reason = "Demote."
        }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(RoleCode.Admin, users.User!.RoleId);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("")]
    [InlineData(null)]
    public async Task UnknownRoleIsInvalid(string? roleCode)
    {
        var (service, accounts, users, audit, work) = CreateDefault();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = roleCode!,
            Reason = "Reason."
        }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(ChangeAccountRoleViewModel.RoleCode), error.Field);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
        Assert.Equal(RoleCode.Member, users.User!.RoleId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task BlankReasonIsInvalid(string? reason)
    {
        var (service, accounts, users, audit, work) = CreateDefault();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "officer",
            Reason = reason!
        }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(ChangeAccountRoleViewModel.Reason), error.Field);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
        Assert.Equal(RoleCode.Member, users.User!.RoleId);
    }

    [Fact]
    public async Task OverlongReasonIsInvalid()
    {
        var (service, accounts, users, audit, work) = CreateDefault();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "officer",
            Reason = new string('x', 1001)
        }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(ChangeAccountRoleViewModel.Reason), error.Field);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
        Assert.Equal(RoleCode.Member, users.User!.RoleId);
    }

    [Fact]
    public async Task ReasonOfExactlyOneThousandCharactersIsAccepted()
    {
        var (service, accounts, users, audit, work) = CreateDefault();
        var reason = new string('x', 1000);

        await service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "officer",
            Reason = reason
        }, TestContext.Current.CancellationToken);

        Assert.Equal(reason, Assert.Single(audit.Entries).Reason);
        Assert.Equal(2, work.SaveCount);
        Assert.Equal(RoleCode.Officer, users.User!.RoleId);
    }

    [Fact]
    public async Task OfficerActorIsForbidden()
    {
        var (service, accounts, users, audit, work) = CreateDefault(actorRole: RoleCodes.Officer);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "officer",
            Reason = "Reason."
        }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
        Assert.Equal(RoleCode.Member, users.User!.RoleId);
    }

    [Fact]
    public async Task AnonymousActorIsForbidden()
    {
        var (service, accounts, users, audit, work) = CreateDefault(authenticated: false);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 42,
            RoleCode = "officer",
            Reason = "Reason."
        }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
        Assert.Equal(RoleCode.Member, users.User!.RoleId);
    }

    [Fact]
    public async Task UnknownAccountIsNotFound()
    {
        var (service, accounts, _, audit, work) = CreateDefault();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(new ChangeAccountRoleViewModel
        {
            AccountId = 404,
            RoleCode = "officer",
            Reason = "Reason."
        }, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
        Assert.False(accounts.LockAcquired);
        Assert.Equal(0, work.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task RoleChangeFormShowsCurrentRoleAndFixedChoices()
    {
        var (service, _, _, _, _) = CreateDefault();

        var model = await service.GetRoleChangeAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(42, model.AccountId);
        Assert.Equal("Ana Reyes", model.FullName);
        Assert.Equal("ana@example.com", model.Email);
        Assert.Equal(RoleCodes.Member, model.CurrentRoleCode);
        Assert.Equal("Member", model.CurrentRoleLabel);
        Assert.Equal(RoleCodes.Member, model.RoleCode);
        Assert.Equal(new[] { RoleCodes.Admin, RoleCodes.Officer, RoleCodes.Member },
            model.Roles.Select(role => role.Code).ToArray());
    }

    [Fact]
    public async Task RoleChangeFormForUnknownAccountIsNotFound()
    {
        var (service, _, _, _, _) = CreateDefault();

        var error = await Assert.ThrowsAsync<DomainException>(() => service.GetRoleChangeAsync(404, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
    }

    [Fact]
    public async Task RoleChangeFormRequiresAdministrator()
    {
        var (service, _, _, _, _) = CreateDefault(actorRole: RoleCodes.Officer);

        var error = await Assert.ThrowsAsync<DomainException>(() => service.GetRoleChangeAsync(42, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
    }

    private static (AccountRoleService Service, FakeAccounts Accounts, FakeUsers Users, RecordingAudit Audit, ImmediateUnitOfWork Work) CreateDefault(
        string actorRole = RoleCodes.Admin,
        bool authenticated = true)
    {
        var accounts = new FakeAccounts();
        var target = accounts.Add(42, "Ana Reyes", "ana@example.com", RoleCode.Member, isActive: true);
        var users = new FakeUsers(target);
        var audit = new RecordingAudit();
        var work = new ImmediateUnitOfWork();
        var service = Create(accounts, users, work, audit, TimeProvider.System, actorRole, authenticated);
        return (service, accounts, users, audit, work);
    }

    private static AccountRoleService Create(
        FakeAccounts accounts,
        FakeUsers users,
        ImmediateUnitOfWork work,
        RecordingAudit audit,
        TimeProvider? clock = null,
        string actorRole = RoleCodes.Admin,
        bool authenticated = true) =>
        new(accounts, users, work, clock ?? TimeProvider.System, new FixedActor(ActorId, actorRole, authenticated), audit);

    private static string Json(IAuditValues values) => JsonSerializer.Serialize(values, values.GetType());

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FixedActor(int userId, string role, bool isAuthenticated) : ICurrentActor
    {
        public bool IsAuthenticated => isAuthenticated;
        public int UserId => userId;
        public string Role => role;
    }

    private sealed class RecordingAudit : IActivityLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class ImmediateUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public DbContext Database => throw new NotSupportedException();

        public void SaveChanges() => SaveCount++;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) => work(cancellationToken);

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default) => work(cancellationToken);
    }

    private sealed class FakeAccounts : IAccountAdministrationRepository
    {
        private readonly Dictionary<int, AccountListItem> accounts = new();

        public int ActiveAdministratorCount { get; set; }
        public bool LockAcquired { get; private set; }

        public User Add(int accountId, string fullName, string email, RoleCode roleId, bool isActive)
        {
            accounts[accountId] = new AccountListItem
            {
                AccountId = accountId,
                FullName = fullName,
                Email = email,
                RoleCode = Code(roleId),
                RoleLabel = Label(roleId),
                IsActive = isActive
            };

            return new User
            {
                Id = accountId,
                UserId = email,
                Name = fullName,
                RoleId = roleId,
                IsActive = isActive
            };
        }

        public Task<int> CountAsync(string search, CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts.Count);

        public Task<IReadOnlyList<AccountListItem>> GetPageAsync(string search, int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountListItem>>(accounts.Values.ToList());

        public Task<AccountListItem?> GetAccountAsync(int accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts.TryGetValue(accountId, out var account) ? account : null);

        public Task<IReadOnlyList<RoleOption>> GetRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoleOption>>(new[]
            {
                new RoleOption { RoleId = RoleCode.Admin, Code = RoleCodes.Admin, Label = "Administrator" },
                new RoleOption { RoleId = RoleCode.Officer, Code = RoleCodes.Officer, Label = "Officer" },
                new RoleOption { RoleId = RoleCode.Member, Code = RoleCodes.Member, Label = "Member" }
            });

        public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ActiveAdministratorCount);

        public Task AcquireLastAdministratorLockAsync(CancellationToken cancellationToken = default)
        {
            LockAcquired = true;
            return Task.CompletedTask;
        }

        private static string Code(RoleCode roleId) => roleId switch
        {
            RoleCode.Admin => RoleCodes.Admin,
            RoleCode.Officer => RoleCodes.Officer,
            _ => RoleCodes.Member
        };

        private static string Label(RoleCode roleId) => roleId switch
        {
            RoleCode.Admin => "Administrator",
            RoleCode.Officer => "Officer",
            _ => "Member"
        };
    }

    private sealed class FakeUsers(User user) : IUserRepository
    {
        public User? User { get; } = user;

        /// <summary>When true, every role lookup misses, as a broken role table would.</summary>
        public bool RoleLookupMissing { get; set; }

        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<User>>(new[] { User! });

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(User);

        public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(User?.Id == userId ? User : null);

        public Task<User?> LockUserAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(User?.Id == userId ? User : null);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RoleLookupMissing
                ? null!
                : roleId switch
                {
                    RoleCode.Admin => RoleCodes.Admin,
                    RoleCode.Officer => RoleCodes.Officer,
                    _ => RoleCodes.Member
                });
    }
}
