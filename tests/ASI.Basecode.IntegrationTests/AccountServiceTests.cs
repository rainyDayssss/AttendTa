using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public class AccountServiceTests : IClassFixture<SqlServerDatabaseFixture>
{
    private const string Password = "AccountService#2026";

    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();

    public AccountServiceTests(SqlServerDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Theory]
    [InlineData(AccountRequestStatusCode.Approved)]
    [InlineData(AccountRequestStatusCode.Rejected)]
    public async Task DecisionCommittedDuringPasswordVerificationCannotReturnPending(AccountRequestStatusCode decision)
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var seed = new AsiBasecodeDBContext(options);
        var email = $"race-{Guid.NewGuid():N}@attendta.test";
        var request = new AccountRequest { StudentNumber = Guid.NewGuid().ToString("N"), FullName = "Race Applicant", Email = email, PasswordHash = hasher.Hash(Password), Course = "BSIT", YearLevel = 1, Section = "A", AccountRequestStatusId = AccountRequestStatusCode.Pending, RequestedAt = DateTime.UtcNow };
        seed.AccountRequests.Add(request);
        await seed.SaveChangesAsync(ct);

        await using var loginContext = new AsiBasecodeDBContext(options);
        var raceHasher = new DecisionDuringVerifyHasher(hasher, () =>
        {
            using var reviewContext = new AsiBasecodeDBContext(options);
            var reviewed = reviewContext.AccountRequests.Single(row => row.RequestId == request.RequestId);
            reviewed.AccountRequestStatusId = decision;
            reviewed.PasswordHash = null;
            reviewContext.SaveChanges();
        });
        var uow = new UnitOfWork(loginContext);
        var service = new AccountService(new UserRepository(uow), raceHasher, () => new AccountRequestRepository(uow));
        var result = await service.LoginAsync(email, Password, ct);
        Assert.Equal(LoginStatus.Invalid, result.Status);
        Assert.Null(result.User);
        Assert.True(raceHasher.DecisionCommitted);
    }

    [Fact]
    public async Task CaseOnlyHashChangeDuringVerificationCannotReturnPending()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var seed = new AsiBasecodeDBContext(options);
        var email = $"hash-race-{Guid.NewGuid():N}@attendta.test";
        var original = hasher.Hash(Password);
        var index = original.LastIndexOf('$') + 1;
        while (!char.IsAsciiLetter(original[index])) index++;
        var chars = original.ToCharArray();
        chars[index] = char.IsUpper(chars[index]) ? char.ToLowerInvariant(chars[index]) : char.ToUpperInvariant(chars[index]);
        var changed = new string(chars);
        var request = new AccountRequest { StudentNumber = Guid.NewGuid().ToString("N"), FullName = "Race Applicant", Email = email, PasswordHash = original, Course = "BSIT", YearLevel = 1, Section = "A", AccountRequestStatusId = AccountRequestStatusCode.Pending, RequestedAt = DateTime.UtcNow };
        seed.AccountRequests.Add(request);
        await seed.SaveChangesAsync(ct);

        await using var loginContext = new AsiBasecodeDBContext(options);
        var uow = new UnitOfWork(loginContext);
        var repository = new AccountRequestRepository(uow);
        Assert.True(await repository.IsPendingCredentialAsync(request.RequestId, original, ct));
        Assert.False(await repository.IsPendingCredentialAsync(request.RequestId, original + " ", ct));
        var raceHasher = new DecisionDuringVerifyHasher(hasher, () =>
        {
            using var reviewContext = new AsiBasecodeDBContext(options);
            var row = reviewContext.AccountRequests.Single(item => item.RequestId == request.RequestId);
            row.PasswordHash = changed;
            reviewContext.SaveChanges();
        });
        var result = await new AccountService(new UserRepository(uow), raceHasher, () => repository).LoginAsync(email, Password, ct);
        Assert.True(raceHasher.DecisionCommitted);
        Assert.Equal(LoginStatus.Invalid, result.Status);
        Assert.Null(result.User);
        Assert.False(await repository.IsPendingCredentialAsync(request.RequestId, original, ct));
        Assert.True(await repository.IsPendingCredentialAsync(request.RequestId, changed, ct));
    }

    private sealed class DecisionDuringVerifyHasher(IPasswordHasher inner, Action commit) : IPasswordHasher
    {
        public bool DecisionCommitted { get; private set; }
        public string Hash(string password) => inner.Hash(password);
        public bool Verify(string password, string hash)
        {
            var valid = inner.Verify(password, hash);
            commit();
            DecisionCommitted = true;
            return valid;
        }
    }

    [Theory]
    [InlineData(RoleCode.Admin, "admin")]
    [InlineData(RoleCode.Officer, "officer")]
    [InlineData(RoleCode.Member, "member")]
    public async Task AuthenticateReturnsTheLookupRoleCodeForAnActiveAccount(RoleCode role, string expectedCode)
    {
        var email = $"{expectedCode}-active@attendta.test";
        await SeedUserAsync(email, role, isActive: true);

        var user = await CreateService().AuthenticateAsync(email, Password, TestContext.Current.CancellationToken);

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(expectedCode, user.RoleCode);
        Assert.True(user.UserId > 0);
        Assert.Equal("Seeded Account", user.FullName);
    }

    [Fact]
    public async Task AuthenticateReturnsNullForAnUnknownEmail()
    {
        var user = await CreateService().AuthenticateAsync("missing@attendta.test", Password, TestContext.Current.CancellationToken);

        Assert.Null(user);
    }

    [Fact]
    public async Task AuthenticateReturnsNullForAWrongPassword()
    {
        const string email = "wrong-password@attendta.test";
        await SeedUserAsync(email, RoleCode.Admin, isActive: true);

        var user = await CreateService().AuthenticateAsync(email, "not-the-password", TestContext.Current.CancellationToken);

        Assert.Null(user);
    }

    [Fact]
    public async Task AuthenticateReturnsNullForAnInactiveAccount()
    {
        const string email = "inactive@attendta.test";
        await SeedUserAsync(email, RoleCode.Admin, isActive: false);

        var user = await CreateService().AuthenticateAsync(email, Password, TestContext.Current.CancellationToken);

        Assert.Null(user);
    }

    [Fact]
    public async Task GetActiveUserReturnsNullAfterDeactivation()
    {
        const string email = "deactivated@attendta.test";
        var userId = await SeedUserAsync(email, RoleCode.Admin, isActive: true);

        Assert.NotNull(await CreateService().GetActiveUserAsync(userId, TestContext.Current.CancellationToken));

        await SetActiveAsync(userId, false);
        Assert.Null(await CreateService().GetActiveUserAsync(userId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetActiveUserReportsTheCurrentRoleAfterADemotion()
    {
        const string email = "demoted@attendta.test";
        var userId = await SeedUserAsync(email, RoleCode.Admin, isActive: true);

        await SetRoleAsync(userId, RoleCode.Member);

        var user = await CreateService().GetActiveUserAsync(userId, TestContext.Current.CancellationToken);
        Assert.NotNull(user);
        Assert.Equal("member", user.RoleCode);
    }

    private AccountService CreateService()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        var context = new AsiBasecodeDBContext(options);
        return new AccountService(new UserRepository(new UnitOfWork(context)), hasher, () => new AccountRequestRepository(new UnitOfWork(context)));
    }

    private async Task<int> SeedUserAsync(string email, RoleCode role, bool isActive)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);

        var user = new User
        {
            Email = email,
            Name = "Seeded Account",
            Password = hasher.Hash(Password),
            RoleId = role,
            IsActive = isActive
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }

    private async Task SetActiveAsync(int userId, bool isActive)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);
        var user = await context.Users.SingleAsync(item => item.Id == userId, TestContext.Current.CancellationToken);
        user.IsActive = isActive;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SetRoleAsync(int userId, RoleCode role)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);
        var user = await context.Users.SingleAsync(item => item.Id == userId, TestContext.Current.CancellationToken);
        user.RoleId = role;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
