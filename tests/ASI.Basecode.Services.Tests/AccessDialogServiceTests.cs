using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public class AccessDialogServiceTests
{
    [Fact]
    public async Task MissingAccountIsNotFound()
    {
        var service = Create(new FakeAccounts(), new FakeMembers(), new RecordingCancellation());

        var error = await Assert.ThrowsAsync<DomainException>(
            () => service.GetAccountAccessAsync(404, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
        Assert.Equal("Account not found.", error.Message);
    }

    [Fact]
    public async Task ActiveAdministratorAloneIsFlaggedAsTheLastAdministrator()
    {
        var accounts = new FakeAccounts { ActiveAdministratorCount = 1 };
        accounts.Add(42, "Root Admin", "root@example.com", "admin", isActive: true);
        var cancellations = new RecordingCancellation();
        var service = Create(accounts, new FakeMembers(), cancellations);

        var model = await service.GetAccountAccessAsync(42, TestContext.Current.CancellationToken);

        Assert.True(model.IsLastActiveAdministrator);
        Assert.Equal(new[] { 42 }, cancellations.PreviewUserIds);
    }

    [Fact]
    public async Task ActiveAdministratorWithAnotherAdministratorIsNotFlagged()
    {
        var accounts = new FakeAccounts { ActiveAdministratorCount = 2 };
        accounts.Add(42, "Second Admin", "second@example.com", "admin", isActive: true);
        var service = Create(accounts, new FakeMembers(), new RecordingCancellation());

        var model = await service.GetAccountAccessAsync(42, TestContext.Current.CancellationToken);

        Assert.False(model.IsLastActiveAdministrator);
    }

    [Fact]
    public async Task InactiveAdministratorIsNotFlaggedAndPreviewsNothing()
    {
        var accounts = new FakeAccounts { ActiveAdministratorCount = 1 };
        accounts.Add(42, "Retired Admin", "retired@example.com", "admin", isActive: false);
        var cancellations = new RecordingCancellation();
        var service = Create(accounts, new FakeMembers(), cancellations);

        var model = await service.GetAccountAccessAsync(42, TestContext.Current.CancellationToken);

        Assert.False(model.IsLastActiveAdministrator);
        Assert.Empty(model.AffectedRegistrations);
        Assert.Empty(cancellations.PreviewUserIds);
    }

    [Fact]
    public async Task ActiveNonAdministratorIsNotFlagged()
    {
        var accounts = new FakeAccounts { ActiveAdministratorCount = 1 };
        accounts.Add(42, "Officer One", "officer@example.com", "officer", isActive: true);
        var service = Create(accounts, new FakeMembers(), new RecordingCancellation());

        var model = await service.GetAccountAccessAsync(42, TestContext.Current.CancellationToken);

        Assert.False(model.IsLastActiveAdministrator);
    }

    [Fact]
    public async Task AccountFieldsAndPreviewAreMapped()
    {
        var accounts = new FakeAccounts { ActiveAdministratorCount = 2 };
        accounts.Add(7, "Ana Reyes", "ana.reyes@example.com", "officer", isActive: true);
        var preview = Preview(11, 3, "Org Fair");
        var cancellations = new RecordingCancellation { Previews = [preview] };
        var service = Create(accounts, new FakeMembers(), cancellations);

        var model = await service.GetAccountAccessAsync(7, TestContext.Current.CancellationToken);

        Assert.Equal(7, model.AccountId);
        Assert.Equal(7, model.UserId);
        Assert.Equal("Ana Reyes", model.FullName);
        Assert.Equal("ana.reyes@example.com", model.Email);
        Assert.Equal("officer", model.RoleCode);
        Assert.Equal("Officer", model.RoleLabel);
        Assert.True(model.IsActive);
        Assert.Single(model.AffectedRegistrations);
        Assert.Same(preview, model.AffectedRegistrations[0]);
        Assert.Equal(new[] { 7 }, cancellations.PreviewUserIds);
    }

    [Fact]
    public async Task MissingMemberIsNotFound()
    {
        var service = Create(new FakeAccounts(), new FakeMembers(), new RecordingCancellation());

        var error = await Assert.ThrowsAsync<DomainException>(
            () => service.GetMembershipAccessAsync(404, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
        Assert.Equal("Member not found.", error.Message);
    }

    [Fact]
    public async Task ActiveMemberPreviewsThroughTheLinkedUser()
    {
        var members = new FakeMembers();
        members.Add(9, userId: 31, "Bea Cruz", "2024-0009", "active");
        var preview = Preview(12, 4, "General Assembly");
        var cancellations = new RecordingCancellation { Previews = [preview] };
        var service = Create(new FakeAccounts(), members, cancellations);

        var model = await service.GetMembershipAccessAsync(9, TestContext.Current.CancellationToken);

        Assert.True(model.IsActive);
        Assert.Single(model.AffectedRegistrations);
        Assert.Same(preview, model.AffectedRegistrations[0]);
        Assert.Equal(new[] { 31 }, cancellations.PreviewUserIds);
    }

    [Fact]
    public async Task MembershipStatusIsCaseInsensitive()
    {
        var members = new FakeMembers();
        members.Add(9, userId: 31, "Bea Cruz", "2024-0009", "ACTIVE");
        var service = Create(new FakeAccounts(), members, new RecordingCancellation());

        var model = await service.GetMembershipAccessAsync(9, TestContext.Current.CancellationToken);

        Assert.True(model.IsActive);
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("ACTIVE", true)]
    [InlineData("Active", true)]
    [InlineData("inactive", false)]
    [InlineData("Inactive", false)]
    public void MembershipAccessRowModelTreatsTheStatusCaseInsensitively(string status, bool expected)
    {
        // The member list builds its access action model through this factory, so a mixed-case lookup
        // code must not read as inactive there either.
        var model = MembershipAccessViewModel.For(new MemberViewModel
        {
            MemberId = 9,
            UserId = 31,
            FullName = "Bea Cruz",
            StudentNumber = "2024-0009",
            Status = status
        });

        Assert.Equal(expected, model.IsActive);
    }

    [Fact]
    public async Task InactiveMemberPreviewsNothing()
    {
        var members = new FakeMembers();
        members.Add(9, userId: 31, "Bea Cruz", "2024-0009", "inactive");
        var cancellations = new RecordingCancellation();
        var service = Create(new FakeAccounts(), members, cancellations);

        var model = await service.GetMembershipAccessAsync(9, TestContext.Current.CancellationToken);

        Assert.False(model.IsActive);
        Assert.Empty(model.AffectedRegistrations);
        Assert.Empty(cancellations.PreviewUserIds);
    }

    [Fact]
    public async Task MembershipFieldsAreMapped()
    {
        var members = new FakeMembers();
        members.Add(9, userId: 31, "Bea Cruz", "2024-0009", "active");
        var service = Create(new FakeAccounts(), members, new RecordingCancellation());

        var model = await service.GetMembershipAccessAsync(9, TestContext.Current.CancellationToken);

        Assert.Equal(9, model.MemberId);
        Assert.Equal(31, model.UserId);
        Assert.Equal("Bea Cruz", model.FullName);
        Assert.Equal("2024-0009", model.StudentNumber);
    }

    private static AccessDialogService Create(
        IAccountAdministrationRepository accounts,
        IMemberRepository members,
        IRegistrationCancellationService cancellations) =>
        new(accounts, members, cancellations);

    private static UpcomingRegistrationPreview Preview(int registrationId, int eventId, string title) =>
        new(registrationId, eventId, title, new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc));

    private sealed class FakeAccounts : IAccountAdministrationRepository
    {
        private readonly List<AccountListItem> _accounts = [];

        public int ActiveAdministratorCount { get; set; }

        public AccountListItem Add(int accountId, string fullName, string email, string roleCode, bool isActive)
        {
            var account = new AccountListItem
            {
                AccountId = accountId,
                FullName = fullName,
                Email = email,
                RoleCode = roleCode,
                RoleLabel = roleCode.Length == 0 ? roleCode : char.ToUpperInvariant(roleCode[0]) + roleCode[1..],
                IsActive = isActive
            };
            _accounts.Add(account);
            return account;
        }

        public Task<AccountListItem?> GetAccountAsync(int accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_accounts.FirstOrDefault(account => account.AccountId == accountId));

        public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ActiveAdministratorCount);

        public Task<int> CountAsync(string search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountListItem>> GetPageAsync(string search, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoleOption>> GetRolesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AcquireLastAdministratorLockAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeMembers : IMemberRepository
    {
        private readonly List<MemberRecord> _members = [];

        public MemberRecord Add(int memberId, int userId, string fullName, string studentNumber, string status)
        {
            var member = new MemberRecord
            {
                MemberId = memberId,
                UserId = userId,
                FullName = fullName,
                StudentNumber = studentNumber,
                Status = status
            };
            _members.Add(member);
            return member;
        }

        public Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_members.FirstOrDefault(member => member.MemberId == memberId)!);

        public IQueryable<MemberRecord> GetMemberRecords() => _members.AsQueryable();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingCancellation : IRegistrationCancellationService
    {
        public List<int> PreviewUserIds { get; } = [];
        public IReadOnlyList<UpcomingRegistrationPreview> Previews { get; set; } = [];

        public Task<IReadOnlyList<UpcomingRegistrationPreview>> PreviewUpcomingForUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            PreviewUserIds.Add(userId);
            return Task.FromResult(Previews);
        }

        public Task CancelRegistrationAsync(int registrationId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelAllForEventAsync(int eventId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelUpcomingForUserAsync(int userId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
