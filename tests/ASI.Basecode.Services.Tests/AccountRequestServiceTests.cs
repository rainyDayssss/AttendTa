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
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class AccountRequestServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 5, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidSubmissionStoresOnePendingRequestWithAnArgon2idHashAndTheInjectedUtcTime()
    {
        var harness = new Harness();
        var model = ValidModel();

        await harness.Service.SubmitAsync(model, TestContext.Current.CancellationToken);

        var request = Assert.Single(harness.Requests.Requests);
        Assert.Equal(AccountRequestStatusCode.Pending, request.AccountRequestStatusId);
        Assert.Equal("2023-01234", request.StudentNumber);
        Assert.Equal("Ana Reyes", request.FullName);
        Assert.Equal("ana.reyes@example.com", request.Email);
        Assert.Equal("BSIT", request.Course);
        Assert.Equal(2, request.YearLevel);
        Assert.Equal("A", request.Section);
        Assert.Equal("09171234567", request.MobileNumber);
        Assert.Equal(FixedNow.UtcDateTime, request.RequestedAt);
        Assert.Equal(DateTimeKind.Utc, request.RequestedAt.Kind);
        Assert.StartsWith("$argon2id$", request.PasswordHash, StringComparison.Ordinal);
        Assert.True(harness.Hasher.Verify(model.Password, request.PasswordHash));
        Assert.DoesNotContain(model.Password, request.PasswordHash, StringComparison.Ordinal);
        Assert.Null(request.CreatedUserId);
        Assert.Null(request.ReviewedBy);
        Assert.Null(request.ReviewedAt);
        Assert.Null(request.ReviewNotes);
        Assert.Equal(1, harness.UnitOfWork.SaveCount);
        Assert.Empty(harness.Users.Users);
        Assert.Empty(harness.Members.Members);
    }

    [Fact]
    public async Task DuplicateEmailForAnActiveUserIsRejectedOnTheEmailField()
    {
        var harness = new Harness();
        harness.Users.Users.Add(new User { UserId = "ana.reyes@example.com", Name = "Existing", IsActive = true });

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(ValidModel(), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.Email), exception.Field);
        Assert.Empty(harness.Requests.Requests);
        Assert.Equal(0, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task DuplicateEmailForAnInactiveUserIsRejectedOnTheEmailField()
    {
        var harness = new Harness();
        harness.Users.Users.Add(new User { UserId = "ana.reyes@example.com", Name = "Inactive", IsActive = false });

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(ValidModel(), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.Email), exception.Field);
        Assert.Empty(harness.Requests.Requests);
    }

    [Fact]
    public async Task DuplicateEmailForAPendingRequestIsRejectedOnTheEmailField()
    {
        var harness = new Harness();
        harness.Requests.Requests.Add(new AccountRequest
        {
            Email = "ana.reyes@example.com",
            StudentNumber = "2023-00001",
            AccountRequestStatusId = AccountRequestStatusCode.Pending
        });

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(ValidModel(), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.Email), exception.Field);
        Assert.Single(harness.Requests.Requests);
    }

    [Fact]
    public async Task DuplicateStudentNumberForAnActiveMemberIsRejectedOnTheStudentNumberField()
    {
        var harness = new Harness();
        harness.Members.Members.Add(new Member
        {
            StudentNumber = "2023-01234",
            MembershipStatusId = MembershipStatusCode.Active
        });

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(ValidModel(), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.StudentNumber), exception.Field);
        Assert.Empty(harness.Requests.Requests);
    }

    [Fact]
    public async Task DuplicateStudentNumberForAnInactiveMemberIsRejectedOnTheStudentNumberField()
    {
        var harness = new Harness();
        harness.Members.Members.Add(new Member
        {
            StudentNumber = "2023-01234",
            MembershipStatusId = MembershipStatusCode.Inactive
        });

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(ValidModel(), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.StudentNumber), exception.Field);
        Assert.Empty(harness.Requests.Requests);
    }

    [Fact]
    public async Task DuplicateStudentNumberForAPendingRequestIsRejectedOnTheStudentNumberField()
    {
        var harness = new Harness();
        harness.Requests.Requests.Add(new AccountRequest
        {
            Email = "someone.else@example.com",
            StudentNumber = "2023-01234",
            AccountRequestStatusId = AccountRequestStatusCode.Pending
        });

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(ValidModel(), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.StudentNumber), exception.Field);
        Assert.Single(harness.Requests.Requests);
    }

    [Theory]
    [InlineData(nameof(AccountRequestViewModel.StudentNumber), "  ")]
    [InlineData(nameof(AccountRequestViewModel.FullName), null)]
    [InlineData(nameof(AccountRequestViewModel.Email), null)]
    [InlineData(nameof(AccountRequestViewModel.Email), "not-an-email")]
    [InlineData(nameof(AccountRequestViewModel.Course), "")]
    [InlineData(nameof(AccountRequestViewModel.Section), null)]
    public async Task InvalidFieldIsRejectedWithItsFieldName(string field, string? value)
    {
        var harness = new Harness();
        var model = ValidModel();
        typeof(AccountRequestViewModel).GetProperty(field)!.SetValue(model, value);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(model, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(field, exception.Field);
        Assert.Empty(harness.Requests.Requests);
        Assert.Equal(0, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task YearLevelZeroIsRejectedOnTheYearLevelField()
    {
        var harness = new Harness();
        var model = ValidModel();
        model.YearLevel = 0;

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(model, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.YearLevel), exception.Field);
        Assert.Empty(harness.Requests.Requests);
    }

    [Fact]
    public async Task MissingPasswordIsRejectedOnThePasswordField()
    {
        var harness = new Harness();
        var model = ValidModel();
        model.Password = null;
        model.ConfirmPassword = null;

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(model, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.Password), exception.Field);
        Assert.Empty(harness.Requests.Requests);
    }

    [Fact]
    public async Task MismatchedConfirmationIsRejectedOnTheConfirmPasswordField()
    {
        var harness = new Harness();
        var model = ValidModel();
        model.ConfirmPassword = "Different#2026";

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(model, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(nameof(AccountRequestViewModel.ConfirmPassword), exception.Field);
        Assert.Empty(harness.Requests.Requests);
    }

    [Fact]
    public async Task RaceDuplicateFromTheDatabaseIsPropagatedWithoutAField()
    {
        var harness = new Harness();
        harness.UnitOfWork.SaveFailure = new DomainException(OutcomeCode.Duplicate, "A record with these values already exists.");

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.SubmitAsync(ValidModel(), TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Null(exception.Field);
        Assert.Single(harness.Requests.Requests);
        Assert.Equal(1, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task FinalizedRequestsDoNotBlockANewSubmission()
    {
        var harness = new Harness();
        harness.Requests.Requests.Add(new AccountRequest
        {
            Email = "ana.reyes@example.com",
            StudentNumber = "2023-01234",
            AccountRequestStatusId = AccountRequestStatusCode.Rejected
        });

        await harness.Service.SubmitAsync(ValidModel(), TestContext.Current.CancellationToken);

        Assert.Equal(2, harness.Requests.Requests.Count);
        Assert.Equal(AccountRequestStatusCode.Pending, harness.Requests.Requests[^1].AccountRequestStatusId);
    }

    [Fact]
    public async Task TextFieldsAreTrimmedAndABlankMobileNumberIsStoredAsNull()
    {
        var harness = new Harness();
        var model = ValidModel();
        model.StudentNumber = "  2023-01234  ";
        model.FullName = "  Ana Reyes  ";
        model.Email = "  Ana.Reyes@Example.com  ";
        model.Course = "  BSIT  ";
        model.Section = "  A  ";
        model.MobileNumber = "   ";

        await harness.Service.SubmitAsync(model, TestContext.Current.CancellationToken);

        var request = Assert.Single(harness.Requests.Requests);
        Assert.Equal("2023-01234", request.StudentNumber);
        Assert.Equal("Ana Reyes", request.FullName);
        Assert.Equal("Ana.Reyes@Example.com", request.Email);
        Assert.Equal("BSIT", request.Course);
        Assert.Equal("A", request.Section);
        Assert.Null(request.MobileNumber);
    }

    [Fact]
    public async Task ManagementOfficerApprovalCreatesActiveMemberAndAuditsWithoutChangingThePasswordHash()
    {
        var harness = new Harness();
        var request = PendingRequest();
        var originalHash = request.PasswordHash;
        harness.Requests.Requests.Add(request);

        await harness.Service.ApproveAsync(request.RequestId, harness.Actor.UserId, TestContext.Current.CancellationToken);

        var user = Assert.Single(harness.Users.Users);
        Assert.Equal(request.Email, user.UserId);
        Assert.Equal(request.FullName, user.Name);
        Assert.Equal(originalHash, user.Password);
        Assert.Equal(RoleCode.Member, user.RoleId);
        Assert.True(user.IsActive);
        Assert.Equal(harness.Actor.UserId, user.CreatedById);
        Assert.Equal(AccountRequestStatusCode.Approved, request.AccountRequestStatusId);
        Assert.Null(request.PasswordHash);
        Assert.Equal(user.Id, request.CreatedUserId);
        Assert.Equal(harness.Actor.UserId, request.ReviewedBy);
        Assert.Equal(FixedNow.UtcDateTime, request.ReviewedAt);
        Assert.Null(request.ReviewNotes);
        Assert.Equal(2, harness.UnitOfWork.SaveCount);
        Assert.Equal(user.Id, harness.MemberService.CreatedUserId);
        Assert.Equal(request.StudentNumber, harness.MemberService.Model!.StudentNumber);
        Assert.Equal(request.Course, harness.MemberService.Model.Course);
        Assert.Equal(request.YearLevel, harness.MemberService.Model.YearLevel);
        Assert.Equal(request.Section, harness.MemberService.Model.Section);
        Assert.Equal(request.MobileNumber, harness.MemberService.Model.MobileNumber);
        Assert.Equal(request.FullName, harness.MemberService.Model.FullName);
        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Equal(AuditActions.AccountRequestApproved, audit.Action);
        Assert.Equal("account_requests", audit.EntityType);
        Assert.Equal(request.RequestId, audit.EntityId);
        Assert.DoesNotContain("hash", JsonSerializer.Serialize(audit), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(originalHash, JsonSerializer.Serialize(audit), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "officer", 42)]
    [InlineData(true, "member", 42)]
    [InlineData(true, "admin", 43)]
    public async Task ApprovalRequiresAnAuthenticatedMatchingManagementActor(bool authenticated, string role, int actorId)
    {
        var harness = new Harness();
        harness.Actor.IsAuthenticatedValue = authenticated;
        harness.Actor.RoleValue = role;
        harness.Requests.Requests.Add(PendingRequest());

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.ApproveAsync(1, actorId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
        Assert.Empty(harness.Users.Users);
        Assert.Empty(harness.Audit.Entries);
        Assert.Equal(0, harness.UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task RejectionRequiresANonblankReasonWithoutChangingRequestOrWritingAudit()
    {
        var harness = new Harness();
        var request = PendingRequest();
        var originalHash = request.PasswordHash;
        harness.Requests.Requests.Add(request);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.RejectAsync(request.RequestId, "  \t ", harness.Actor.UserId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(AccountRequestStatusCode.Pending, request.AccountRequestStatusId);
        Assert.Equal(originalHash, request.PasswordHash);
        Assert.Null(request.ReviewedBy);
        Assert.Empty(harness.Audit.Entries);
        Assert.Equal(0, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task MissingAndAlreadyFinalizedRequestsReturnNotFoundAndStale()
    {
        var harness = new Harness();
        var missing = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.ApproveAsync(99, harness.Actor.UserId, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.NotFound, missing.Outcome);

        var finalized = PendingRequest();
        finalized.AccountRequestStatusId = AccountRequestStatusCode.Rejected;
        finalized.PasswordHash = null;
        harness.Requests.Requests.Add(finalized);
        var stale = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.RejectAsync(finalized.RequestId, "duplicate attempt", harness.Actor.UserId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Stale, stale.Outcome);
        Assert.Empty(harness.Users.Users);
        Assert.Empty(harness.Audit.Entries);
        Assert.Equal(0, harness.UnitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApprovalRejectsExistingEmailOrStudentNumberIncludingInactiveMembers(bool emailConflict)
    {
        var harness = new Harness();
        var request = PendingRequest();
        harness.Requests.Requests.Add(request);
        if (emailConflict)
        {
            harness.Users.Users.Add(new User { Id = 17, UserId = request.Email, IsActive = false });
        }
        else
        {
            harness.Members.Members.Add(new Member
            {
                StudentNumber = request.StudentNumber,
                MembershipStatusId = MembershipStatusCode.Inactive
            });
        }

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => harness.Service.ApproveAsync(request.RequestId, harness.Actor.UserId, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(AccountRequestStatusCode.Pending, request.AccountRequestStatusId);
        Assert.NotNull(request.PasswordHash);
        Assert.Empty(harness.Audit.Entries);
        Assert.Empty(harness.MemberService.Calls);
        Assert.Equal(0, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task RejectionRecordsReviewerReasonClearsHashAndPermitsResubmission()
    {
        var harness = new Harness();
        var request = PendingRequest();
        harness.Requests.Requests.Add(request);

        await harness.Service.RejectAsync(request.RequestId, "  Not eligible this term.  ", harness.Actor.UserId, TestContext.Current.CancellationToken);

        Assert.Equal(AccountRequestStatusCode.Rejected, request.AccountRequestStatusId);
        Assert.Null(request.PasswordHash);
        Assert.Equal(harness.Actor.UserId, request.ReviewedBy);
        Assert.Equal(FixedNow.UtcDateTime, request.ReviewedAt);
        Assert.Equal("Not eligible this term.", request.ReviewNotes);
        Assert.Empty(harness.Users.Users);
        Assert.Empty(harness.MemberService.Calls);
        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Equal(AuditActions.AccountRequestRejected, audit.Action);
        Assert.Equal("Not eligible this term.", audit.Reason);
        Assert.Equal(1, harness.UnitOfWork.SaveCount);

        var resubmission = ValidModel();
        resubmission.Email = request.Email;
        resubmission.StudentNumber = request.StudentNumber;
        await harness.Service.SubmitAsync(resubmission, TestContext.Current.CancellationToken);
        Assert.Equal(2, harness.Requests.Requests.Count);
        Assert.Equal(AccountRequestStatusCode.Pending, harness.Requests.Requests[1].AccountRequestStatusId);
    }

    private static AccountRequestViewModel ValidModel()
    {
        return new AccountRequestViewModel
        {
            StudentNumber = "2023-01234",
            FullName = "Ana Reyes",
            Email = "ana.reyes@example.com",
            Course = "BSIT",
            YearLevel = 2,
            Section = "A",
            MobileNumber = "09171234567",
            Password = "Applicant#2026",
            ConfirmPassword = "Applicant#2026"
        };
    }

    private static AccountRequest PendingRequest() => new()
    {
        RequestId = 1,
        StudentNumber = "2023-01234",
        FullName = "Ana Reyes",
        Email = "ana.reyes@example.com",
        PasswordHash = "$argon2id$original-approved-hash",
        Course = "BSIT",
        YearLevel = 2,
        Section = "A",
        MobileNumber = "09171234567",
        AccountRequestStatusId = AccountRequestStatusCode.Pending,
        RequestedAt = FixedNow.UtcDateTime.AddDays(-1)
    };

    private sealed class Harness
    {
        public FakeAccountRequestRepository Requests { get; } = new();
        public FakeUserRepository Users { get; } = new();
        public FakeMemberRepository Members { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public Argon2PasswordHasher Hasher { get; } = new();
        public FixedClock Clock { get; } = new() { Now = FixedNow };
        public FakeCurrentActor Actor { get; } = new() { Id = 42, RoleValue = "officer" };
        public FakeMemberService MemberService { get; } = new();
        public FakeActivityLogWriter Audit { get; } = new();
        public AccountRequestService Service { get; }

        public Harness()
        {
            Service = new AccountRequestService(Requests, Users, Members, UnitOfWork, Hasher, Clock, Actor, MemberService, Audit);
        }
    }

    private sealed class FakeCurrentActor : ICurrentActor
    {
        public int Id { get; set; }
        public bool IsAuthenticatedValue { get; set; } = true;
        public string RoleValue { get; set; } = "officer";
        public bool IsAuthenticated => IsAuthenticatedValue;
        public int UserId => Id;
        public string Role => RoleValue;
    }

    private sealed class FakeMemberService : IMemberService
    {
        public List<(int UserId, MemberViewModel Model)> Calls { get; } = new();
        public int CreatedUserId => Calls.Single().UserId;
        public MemberViewModel? Model => Calls.SingleOrDefault().Model;

        public Task<PagedResult<MemberViewModel>> GetPageAsync(string search, string status, int? pageNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberViewModel> GetMemberAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberViewModel> GetSelfProfileAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMemberAsync(MemberViewModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateMemberFromApprovedAccountRequestAsync(int userId, MemberViewModel model, CancellationToken cancellationToken = default)
        {
            Calls.Add((userId, model));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeActivityLogWriter : IActivityLogWriter
    {
        public List<AuditEntry> Entries { get; } = new();
        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeAccountRequestRepository : IAccountRequestRepository
    {
        public List<AccountRequest> Requests { get; } = new();

        public Task<int> CountByStatusAsync(AccountRequestStatusCode? status, string search, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AccountRequestQueueItem>> GetPageByStatusAsync(AccountRequestStatusCode? status, string search, bool oldestFirst, int skip, int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AccountRequestStatusCounts> GetStatusCountsAsync(string search, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AccountRequest> LockAccountRequestAsync(int requestId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Requests.FirstOrDefault(request => request.RequestId == requestId))!;
        }

        public Task AddAsync(AccountRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<bool> PendingEmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Requests.Any(request =>
                request.AccountRequestStatusId == AccountRequestStatusCode.Pending
                && string.Equals(request.Email, email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<bool> PendingStudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Requests.Any(request =>
                request.AccountRequestStatusId == AccountRequestStatusCode.Pending
                && string.Equals(request.StudentNumber, studentNumber, StringComparison.Ordinal)));
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = new();

        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<User>>(Users);

        public Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.UserId, email, StringComparison.OrdinalIgnoreCase)))!;
        }

        public Task<User> GetByIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(user => user.Id == userId))!;
        }

        public Task<User> LockUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(user => user.Id == userId))!;
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            user.Id = Users.Count == 0 ? 100 : Users.Max(item => item.Id) + 1;
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task<string> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string>(null!);
        }
    }

    private sealed class FakeMemberRepository : IMemberRepository
    {
        public List<Member> Members { get; } = new();

        public IQueryable<MemberRecord> GetMemberRecords() => Array.Empty<MemberRecord>().AsQueryable();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default) => Task.FromResult<MemberRecord>(null!);

        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult<MemberRecord>(null!);

        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Members.FirstOrDefault(member => member.UserId == userId)!);

        public Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Members.FirstOrDefault(member => member.MemberId == memberId)!);

        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default)
        {
            Members.Add(member);
            return Task.CompletedTask;
        }

        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Members.Any(member => member.UserId == userId));

        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Members.Any(member =>
                string.Equals(member.StudentNumber, studentNumber, StringComparison.Ordinal)
                && (!excludeId.HasValue || member.MemberId != excludeId.Value)));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Exception? SaveFailure { get; set; }

        public int SaveCount { get; private set; }
        public int TransactionCount { get; private set; }

        public DbContext Database => throw new NotSupportedException();

        public void SaveChanges() => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (SaveFailure is not null)
            {
                throw SaveFailure;
            }

            return Task.FromResult(1);
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return work(cancellationToken);
        }

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return work(cancellationToken);
        }
    }
}
