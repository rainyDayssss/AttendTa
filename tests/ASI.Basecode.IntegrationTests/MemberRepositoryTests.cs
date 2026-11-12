using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public class MemberRepositoryTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture fixture;

    public MemberRepositoryTests(SqlServerDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task GetMemberRecordsJoinsTheMappedUserAndPreservesMemberFields()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);
        var cancellationToken = TestContext.Current.CancellationToken;

        var user = new User
        {
            Name = "Test Member",
            UserId = "member@example.invalid",
            Password = "test-hash",
            IsActive = true,
            RoleId = ASI.Basecode.Data.Models.Lookups.RoleCode.Member
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        var member = new Member
        {
            UserId = user.Id,
            StudentNumber = "IT-10001",
            Course = "Computer Science",
            YearLevel = 2,
            Section = "A",
            MobileNumber = "555-0100",
            MembershipType = "Regular",
            Status = "active",
            CreatedBy = user.Id
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(cancellationToken);

        var repository = new MemberRepository(new TestUnitOfWork(context));
        var record = await repository.GetMemberRecords().SingleAsync(cancellationToken);

        Assert.Equal(member.MemberId, record.MemberId);
        Assert.Equal(member.UserId, record.UserId);
        Assert.Equal("Test Member", record.FullName);
        Assert.Equal("member@example.invalid", record.Email);
        Assert.Equal(member.StudentNumber, record.StudentNumber);
        Assert.Equal(member.Course, record.Course);
        Assert.Equal(member.YearLevel, record.YearLevel);
        Assert.Equal(member.Section, record.Section);
        Assert.Equal(member.MobileNumber, record.MobileNumber);
        Assert.Equal(member.MembershipType, record.MembershipType);
        Assert.Equal("active", record.Status);
        Assert.Equal(user.Id, record.CreatedBy);

        var ownerRecord = await repository.GetMemberRecordByUserIdAsync(user.Id, cancellationToken);
        Assert.Equal(member.MemberId, ownerRecord.MemberId);
        Assert.Null(await repository.GetMemberRecordByUserIdAsync(int.MaxValue, cancellationToken));
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public TestUnitOfWork(DbContext database)
        {
            Database = database;
        }

        public DbContext Database { get; }

        public System.Threading.Tasks.Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default) => Database.SaveChangesAsync(cancellationToken);
        public System.Threading.Tasks.Task ExecuteInTransactionAsync(System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> work, System.Threading.CancellationToken cancellationToken = default) => throw new System.NotSupportedException();
        public System.Threading.Tasks.Task<T> ExecuteInTransactionAsync<T>(System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<T>> work, System.Threading.CancellationToken cancellationToken = default) => throw new System.NotSupportedException();

        public void SaveChanges()
        {
            Database.SaveChanges();
        }
    }
}
