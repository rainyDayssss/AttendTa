#nullable disable
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class AccountLoginMatrixTests
{
    private const string Email = "applicant@example.test";
    private const string Password = "Password123";
    private static readonly Argon2PasswordHasher RealHasher = new();

    [Theory]
    [InlineData(AccountRequestStatusCode.Approved)]
    [InlineData(AccountRequestStatusCode.Rejected)]
    public async Task DecisionDuringVerificationReturnsInvalid(AccountRequestStatusCode decision)
    {
        var requests = Pending();
        var hasher = new DuringVerificationHasher(RealHasher, () => requests.Status = decision);
        var result = await Service(requests, hasher).LoginAsync(Email, Password, TestContext.Current.CancellationToken);

        Assert.Equal(LoginStatus.Invalid, result.Status);
        Assert.Null(result.User);
        Assert.Equal(1, requests.Rechecks);
        Assert.Equal(decision, requests.Status);
    }

    [Fact]
    public async Task CredentialChangeDuringVerificationReturnsInvalid()
    {
        var requests = Pending();
        var hasher = new DuringVerificationHasher(RealHasher, () => requests.Hash = "new hash");
        var result = await Service(requests, hasher).LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(LoginStatus.Invalid, result.Status);
        Assert.Null(result.User);
        Assert.Equal(1, requests.Rechecks);
    }

    [Fact]
    public async Task PendingCorrectPasswordIsRecheckedAndNeverReturnsUser()
    {
        var requests = Pending();
        var result = await Service(requests).LoginAsync("  " + Email + "  ", Password, TestContext.Current.CancellationToken);
        Assert.Equal(LoginStatus.Pending, result.Status);
        Assert.Null(result.User);
        Assert.Equal(1, requests.Rechecks);
        Assert.Equal(Email, requests.LookedUpEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-phc-hash")]
    [InlineData("$argon2id$v=19$m=garbage,t=3,p=1$bad$bad")]
    public async Task UnusablePendingHashIsGenericInvalid(string hash)
    {
        var requests = Pending();
        requests.Hash = hash;
        var result = await Service(requests).LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(LoginStatus.Invalid, result.Status);
        Assert.Null(result.User);
        Assert.Equal(0, requests.Rechecks);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "malformed")]
    public async Task MissingAndUnusableCredentialsReachTheHasher(bool pending, string hash)
    {
        var requests = new FakeRequests { Hash = hash, Status = pending ? AccountRequestStatusCode.Pending : AccountRequestStatusCode.Rejected };
        var hasher = new RecordingHasher(RealHasher);
        var result = await Service(requests, hasher).LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(LoginStatus.Invalid, result.Status);
        Assert.Equal(1, hasher.Calls);
        Assert.Equal(hash, hasher.LastHash);
        Assert.Equal(0, requests.Rechecks);
    }

    [Fact]
    public async Task WrongPasswordAndDecidedRequestAreInvalid()
    {
        var requests = Pending();
        Assert.Equal(LoginStatus.Invalid, (await Service(requests).LoginAsync(Email, "wrong", TestContext.Current.CancellationToken)).Status);
        Assert.Equal(0, requests.Rechecks);
        requests.Status = AccountRequestStatusCode.Rejected;
        Assert.Equal(LoginStatus.Invalid, (await Service(requests).LoginAsync(Email, Password, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(0, requests.Rechecks);
    }

    [Theory]
    [InlineData(false, true, LoginStatus.Invalid)]
    [InlineData(true, false, LoginStatus.Invalid)]
    [InlineData(true, true, LoginStatus.Authenticated)]
    public async Task ExistingAccountTakesPrecedenceOverPendingRequest(bool active, bool correct, LoginStatus expected)
    {
        var requests = Pending();
        var users = new FakeUsers { User = new User { Id = 42, Email = Email, Name = "Member", IsActive = active, Password = RealHasher.Hash(Password), RoleId = RoleCode.Member } };
        var result = await Service(requests, users: users).LoginAsync(Email, correct ? Password : "wrong", TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Status);
        Assert.Equal(expected == LoginStatus.Authenticated, result.User is not null);
        Assert.Equal(0, requests.Lookups);
    }

    private static FakeRequests Pending() => new() { Hash = RealHasher.Hash(Password) };
    private static AccountService Service(FakeRequests requests, IPasswordHasher hasher = null, FakeUsers users = null) =>
        new(users ?? new FakeUsers(), hasher ?? RealHasher, () => requests);

    private sealed class DuringVerificationHasher(IPasswordHasher inner, Action change) : IPasswordHasher
    {
        public string Hash(string password) => inner.Hash(password);
        public bool Verify(string password, string hash)
        {
            var valid = inner.Verify(password, hash);
            change(); // Decision commits after credential lookup and before verification returns.
            return valid;
        }
    }

    private sealed class RecordingHasher(IPasswordHasher inner) : IPasswordHasher
    {
        public int Calls { get; private set; }
        public string LastHash { get; private set; }
        public string Hash(string password) => inner.Hash(password);
        public bool Verify(string password, string hash)
        {
            Calls++;
            LastHash = hash;
            return inner.Verify(password, hash);
        }
    }

    private sealed class FakeUsers : IUserRepository
    {
        public User User { get; set; }
        public Task<User> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(User);
        public Task<string> GetRoleCodeAsync(RoleCode role, CancellationToken ct = default) => Task.FromResult("member");
        public Task<User> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User> LockUserAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(User user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeRequests : IAccountRequestRepository
    {
        public string Hash { get; set; }
        public AccountRequestStatusCode Status { get; set; } = AccountRequestStatusCode.Pending;
        public int Lookups { get; private set; }
        public int Rechecks { get; private set; }
        public string LookedUpEmail { get; private set; }
        public Task<PendingRequestCredential> GetPendingCredentialAsync(string email, CancellationToken ct = default)
        {
            Lookups++;
            LookedUpEmail = email;
            return Task.FromResult(Status == AccountRequestStatusCode.Pending && email == Email ? new PendingRequestCredential(12, Hash) : null);
        }
        public Task<bool> IsPendingCredentialAsync(int id, string hash, CancellationToken ct = default)
        {
            Rechecks++;
            return Task.FromResult(id == 12 && Status == AccountRequestStatusCode.Pending && Hash == hash);
        }
        public Task<AccountRequest> LockAccountRequestAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountByStatusAsync(AccountRequestStatusCode? status, string search, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountRequestQueueItem>> GetPageByStatusAsync(AccountRequestStatusCode? status, string search, bool oldestFirst, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AccountRequestStatusCounts> GetStatusCountsAsync(string search, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountRequestSummary>> GetSummariesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(AccountRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PendingEmailExistsAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PendingStudentNumberExistsAsync(string number, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
