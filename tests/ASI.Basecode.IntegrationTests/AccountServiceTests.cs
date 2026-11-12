using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Security;
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
        return new AccountService(new UserRepository(new UnitOfWork(context)), hasher);
    }

    private async Task<int> SeedUserAsync(string email, RoleCode role, bool isActive)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);

        var user = new User
        {
            UserId = email,
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
