using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Account-request decisions against SQL Server, including locking, rollback and the real MVC endpoints.
/// </summary>
public sealed class AccountRequestDecisionTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private const string ApplicantPassword = "ApplicantDecision#2026";
    private const string ActorPassword = "ManagementDecision#2026";
    private readonly Argon2PasswordHasher hasher = new();

    [Theory]
    [InlineData("BSCS", "BSCS")]
    [InlineData("BSIT", "BSIT")]
    [InlineData("BS Computer Science", "BSCS")]
    [InlineData("BS Information Technology", "BSIT")]
    public async Task ApprovalCreatesLinkedMemberAndOriginalPasswordCanSignIn(string savedCourse, string expectedCourse)
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "approval-actor", ActorPassword, ct);
        var applicantEmail = UniqueEmail("approval-applicant");
        var studentNumber = UniqueStudentNumber();
        var (requestId, originalHash) = await SeedRequestAsync(applicantEmail, studentNumber, ct, savedCourse);

        await using (var writer = Context())
        {
            await CreateService(writer, new TestActor(actor.Id, RoleCodes.Officer))
                .ApproveAsync(requestId, actor.Id, ct);
        }

        await using (var observer = Context())
        {
            var request = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct);
            var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == request.CreatedUserId, ct);
            var member = await observer.Members.AsNoTracking().SingleAsync(item => item.UserId == user.Id, ct);
            var audit = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct);
            var memberAudit = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(item => item.EntityType == "members" && item.EntityId == member.MemberId, ct);

            Assert.Equal(AccountRequestStatusCode.Approved, request.AccountRequestStatusId);
            Assert.Null(request.PasswordHash);
            Assert.Equal(actor.Id, request.ReviewedBy);
            Assert.NotNull(request.ReviewedAt);
            Assert.Equal(originalHash, user.Password);
            Assert.Equal(applicantEmail, user.Email);
            Assert.Equal("Ana Reyes", user.Name);
            Assert.Equal(RoleCode.Member, user.RoleId);
            Assert.True(user.IsActive);
            Assert.Equal(actor.Id, user.CreatedById);
            Assert.Equal(studentNumber, member.StudentNumber);
            Assert.Equal(savedCourse, request.Course);
            Assert.Equal(expectedCourse, member.Course);
            Assert.Equal(2, member.YearLevel);
            Assert.Equal("A", member.Section);
            Assert.Equal("09171234567", member.MobileNumber);
            Assert.Equal(MembershipStatusCode.Active, member.MembershipStatusId);
            Assert.Equal(actor.Id, member.CreatedBy);
            Assert.Equal(AuditActions.AccountRequestApproved, audit.Action);
            Assert.DoesNotContain(originalHash, audit.OldValues ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(originalHash, audit.NewValues ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(AuditActions.MemberCreated, memberAudit.Action);
            Assert.Contains($"\"StudentNumber\":\"{studentNumber}\"", memberAudit.NewValues, StringComparison.Ordinal);
            Assert.DoesNotContain(originalHash, memberAudit.NewValues, StringComparison.Ordinal);
            Assert.Equal(1, await observer.ActivityLogs.CountAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
        }

        await using var authContext = Context();
        var authenticated = await new AccountService(new UserRepository(new UnitOfWork(authContext)), hasher, () => new AccountRequestRepository(new UnitOfWork(authContext)))
            .AuthenticateAsync(applicantEmail, ApplicantPassword, ct);
        Assert.NotNull(authenticated);
        Assert.Equal(RoleCodes.Member, authenticated.RoleCode);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var management = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(management, actor.Email, ActorPassword, ct);
        var search = Uri.EscapeDataString(applicantEmail);
        var matchingRequests = await management.GetStringAsync($"/AccountRequest?status=approved&search={search}&course={expectedCourse}", ct);
        Assert.Contains($"data-request-id=\"{requestId}\"", matchingRequests);
        var matchingMembers = await management.GetStringAsync($"/Member?search={search}&course={expectedCourse}", ct);
        Assert.Contains(studentNumber, matchingMembers);
        var otherCourse = expectedCourse == "BSCS" ? "BSIT" : "BSCS";
        var otherRequests = await management.GetStringAsync($"/AccountRequest?status=approved&search={search}&course={otherCourse}", ct);
        Assert.DoesNotContain($"data-request-id=\"{requestId}\"", otherRequests);
        var otherMembers = await management.GetStringAsync($"/Member?search={search}&course={otherCourse}", ct);
        Assert.DoesNotContain(studentNumber, otherMembers);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, applicantEmail, ApplicantPassword, ct);
    }

    [Fact]
    public async Task ApprovalOfARequestStoredBeforeTheProfileRulesIsRefusedOnTheStudentNumberField()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "legacy-approval-actor", ActorPassword, ct);
        var (requestId, _) = await SeedRequestAsync(UniqueEmail("legacy-approval"), "2023-01234", ct);

        await using (var writer = Context())
        {
            var service = CreateService(writer, new TestActor(actor.Id, RoleCodes.Officer));

            var exception = await Assert.ThrowsAsync<DomainException>(() => service.ApproveAsync(requestId, actor.Id, ct));

            Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
            Assert.Equal(nameof(MemberViewModel.StudentNumber), exception.Field);
        }

        await using var observer = Context();
        var request = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct);
        Assert.Equal(AccountRequestStatusCode.Pending, request.AccountRequestStatusId);
        Assert.Null(request.CreatedUserId);
        Assert.False(await observer.Users.AnyAsync(item => item.Email == request.Email, ct));
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
    }

    [Fact]
    public async Task RejectionClearsHashAndAllowsApplicantToSubmitAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "rejection-actor", ActorPassword, ct);
        var email = UniqueEmail("rejected-applicant");
        var studentNumber = UniqueStudentNumber();
        var (requestId, _) = await SeedRequestAsync(email, studentNumber, ct);

        await using (var writer = Context())
        {
            await CreateService(writer, new TestActor(actor.Id, RoleCodes.Admin))
                .RejectAsync(requestId, "  Missing eligibility documents.  ", actor.Id, ct);
        }

        await using (var observer = Context())
        {
            var rejected = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct);
            Assert.Equal(AccountRequestStatusCode.Rejected, rejected.AccountRequestStatusId);
            Assert.Null(rejected.PasswordHash);
            Assert.Equal(actor.Id, rejected.ReviewedBy);
            Assert.Equal("Missing eligibility documents.", rejected.ReviewNotes);
            Assert.False(await observer.Users.AnyAsync(item => item.Email == email, ct));
            Assert.False(await observer.Members.AnyAsync(item => item.StudentNumber == studentNumber, ct));
            var audit = await observer.ActivityLogs.AsNoTracking().SingleAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct);
            Assert.Equal(AuditActions.AccountRequestRejected, audit.Action);
            Assert.Equal("Missing eligibility documents.", audit.Reason);
            Assert.DoesNotContain("password", audit.OldValues ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hash", audit.NewValues ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        await using (var submitContext = Context())
        {
            var actorForSubmission = new TestActor(actor.Id, RoleCodes.Admin);
            var service = CreateService(submitContext, actorForSubmission);
            await service.SubmitAsync(new AccountRequestViewModel
            {
                Email = email,
                StudentNumber = studentNumber,
                FullName = "Ana Reyes",
                Course = "BSIT",
                YearLevel = 2,
                Section = "A",
                Password = ApplicantPassword,
                ConfirmPassword = ApplicantPassword
            }, ct);
        }

        await using var finalObserver = Context();
        Assert.Equal(2, await finalObserver.AccountRequests.CountAsync(item => item.Email == email, ct));
        Assert.Equal(1, await finalObserver.AccountRequests.CountAsync(item => item.Email == email && item.AccountRequestStatusId == AccountRequestStatusCode.Pending, ct));
    }

    [Fact]
    public async Task ParallelDecisionsOnOneRequestCommitExactlyOneAndTheLoserIsStale()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "parallel-actor", ActorPassword, ct);
        var email = UniqueEmail("parallel-applicant");
        var studentNumber = UniqueStudentNumber();
        var (requestId, _) = await SeedRequestAsync(email, studentNumber, ct);

        async Task<OutcomeCode?> DecideAsync()
        {
            await using var context = Context();
            var service = CreateService(context, new TestActor(actor.Id, RoleCodes.Officer));
            try
            {
                await service.ApproveAsync(requestId, actor.Id, ct);
                return null;
            }
            catch (DomainException exception)
            {
                return exception.Outcome;
            }
        }

        var outcomes = await Task.WhenAll(DecideAsync(), DecideAsync());
        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(outcomes, outcome => outcome == OutcomeCode.Stale);

        await using var observer = Context();
        var request = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct);
        Assert.Equal(AccountRequestStatusCode.Approved, request.AccountRequestStatusId);
        Assert.Null(request.PasswordHash);
        Assert.NotNull(request.CreatedUserId);
        Assert.Equal(1, await observer.Users.CountAsync(item => item.Email == email, ct));
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Email == email, ct);
        Assert.Equal(request.CreatedUserId, user.Id);
        Assert.Equal(1, await observer.Members.CountAsync(item => item.UserId == user.Id && item.StudentNumber == studentNumber, ct));
        var audit = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct);
        Assert.Equal(AuditActions.AccountRequestApproved, audit.Action);
    }

    [Fact]
    public async Task EmailBecomingOccupiedDuringApprovalRollsBackTheRequestAndWritesNoAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "email-race-actor", ActorPassword, ct);
        var email = UniqueEmail("email-race-applicant");
        var studentNumber = UniqueStudentNumber();
        var (requestId, originalHash) = await SeedRequestAsync(email, studentNumber, ct);
        await using var writer = Context();
        var work = new UnitOfWork(writer);
        var innerUsers = new UserRepository(work);
        var racingUsers = new UserInsertRaceRepository(innerUsers, async (candidate, token) =>
        {
            await using var winnerContext = Context();
            winnerContext.Users.Add(new User
            {
                Email = candidate.Email,
                Name = "Concurrent account",
                Password = hasher.Hash("Concurrent#2026"),
                RoleId = RoleCode.Member,
                IsActive = false,
                CreatedById = actor.Id
            });
            await winnerContext.SaveChangesAsync(token);
        });
        var service = CreateService(writer, new TestActor(actor.Id, RoleCodes.Officer), work, racingUsers);

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.ApproveAsync(requestId, actor.Id, ct));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        await using var observer = Context();
        var request = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct);
        Assert.Equal(AccountRequestStatusCode.Pending, request.AccountRequestStatusId);
        Assert.Equal(originalHash, request.PasswordHash);
        Assert.Equal(1, await observer.Users.CountAsync(item => item.Email == email, ct));
        Assert.False(await observer.Members.AnyAsync(item => item.StudentNumber == studentNumber, ct));
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
    }

    [Fact]
    public async Task StudentNumberBecomingOccupiedAtInsertRollsBackTheNewAccountAndRequestDecision()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "student-race-actor", ActorPassword, ct);
        var email = UniqueEmail("student-race-applicant");
        var studentNumber = UniqueStudentNumber();
        var (requestId, originalHash) = await SeedRequestAsync(email, studentNumber, ct);
        await using var writer = Context();
        var work = new UnitOfWork(writer);
        var users = new UserRepository(work);
        var innerMembers = new MemberRepository(work);
        var racingMembers = new MemberInsertRaceRepository(innerMembers, async (candidate, token) =>
        {
            await using var winnerContext = Context();
            winnerContext.Members.Add(new Member
            {
                UserId = actor.Id,
                StudentNumber = candidate.StudentNumber,
                Course = "BSIT",
                YearLevel = 1,
                Section = "RACE",
                MembershipStatusId = MembershipStatusCode.Inactive,
                CreatedBy = actor.Id
            });
            await winnerContext.SaveChangesAsync(token);
        });
        var service = CreateService(writer, new TestActor(actor.Id, RoleCodes.Officer), work, users, racingMembers);

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.ApproveAsync(requestId, actor.Id, ct));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        await using var observer = Context();
        var request = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct);
        Assert.Equal(AccountRequestStatusCode.Pending, request.AccountRequestStatusId);
        Assert.Equal(originalHash, request.PasswordHash);
        Assert.False(await observer.Users.AnyAsync(item => item.Email == email, ct));
        Assert.Equal(1, await observer.Members.CountAsync(item => item.StudentNumber == studentNumber, ct));
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
    }

    [Fact]
    public async Task ManagementDecisionPostEnforcesAntiforgeryAndAllowsOfficerApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "endpoint-officer", ActorPassword, ct);
        var email = UniqueEmail("endpoint-applicant");
        var studentNumber = UniqueStudentNumber();
        var (requestId, _) = await SeedRequestAsync(email, studentNumber, ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);

        using (var noToken = await client.PostAsync($"/ManagementAccountRequest/Approve?id={requestId}", new FormUrlEncodedContent(new Dictionary<string, string>()), ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        }

        using var post = new HttpRequestMessage(HttpMethod.Post, $"/ManagementAccountRequest/Approve?id={requestId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token })
        };
        post.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.SendAsync(post, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("Approved", body.RootElement.GetProperty("outcome").GetString());

        await using var observer = Context();
        Assert.Equal(AccountRequestStatusCode.Approved,
            (await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct)).AccountRequestStatusId);
    }

    [Fact]
    public async Task MemberCannotPostManagementDecision()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Member, "endpoint-member", ActorPassword, ct);
        var (requestId, _) = await SeedRequestAsync(UniqueEmail("forbidden-applicant"), UniqueStudentNumber(), ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/ManagementAccountRequest/Reject?id={requestId}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["reason"] = "Not allowed",
                ["__RequestVerificationToken"] = token
            })
        };

        using var response = await client.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location is { } location
            ? new Uri(location.ToString()).AbsolutePath
            : null);
        await using var observer = Context();
        var requestState = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct);
        Assert.Equal(AccountRequestStatusCode.Pending, requestState.AccountRequestStatusId);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
    }

    [Fact]
    public async Task BrowserDecisionsShowFeedbackValidateReasonAndRejectStalePosts()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "browser-officer", ActorPassword, ct);
        var email = UniqueEmail("browser-approval");
        var (approvedId, _) = await SeedRequestAsync(email, UniqueStudentNumber(), ct);
        var (rejectedId, _) = await SeedRequestAsync(UniqueEmail("browser-rejection"), UniqueStudentNumber(), ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);

        async Task<HttpResponseMessage> Post(string action, int id, string? reason = null, bool includeToken = true)
        {
            var values = new Dictionary<string, string> { ["id"] = id.ToString() };
            if (includeToken) values["__RequestVerificationToken"] = token;
            if (reason is not null) values["reason"] = reason;
            return await client.PostAsync($"/AccountRequest/{action}?status=pending&page=1&keep=value", new FormUrlEncodedContent(values), ct);
        }

        using (var noToken = await Post("Approve", approvedId, includeToken: false))
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using (var invalid = await Post("Reject", rejectedId, "   "))
        {
            var html = await invalid.Content.ReadAsStringAsync(ct);
            Assert.Contains("A rejection reason is required.", html, StringComparison.Ordinal);
            Assert.Contains($"reason-error-{rejectedId}", html, StringComparison.Ordinal);
        }
        using (var approve = await Post("Approve", approvedId))
        {
            Assert.Equal(HttpStatusCode.Redirect, approve.StatusCode);
            Assert.Contains("keep=value", approve.Headers.Location!.ToString(), StringComparison.Ordinal);
            var html = await client.GetStringAsync(approve.Headers.Location, ct);
            Assert.Contains("Account request approved.", html, StringComparison.Ordinal);
        }
        using (var stale = await Post("Approve", approvedId))
            Assert.Contains("already been decided", await stale.Content.ReadAsStringAsync(ct), StringComparison.OrdinalIgnoreCase);
        using (var staleRejection = await Post("Reject", approvedId, "Keep this reason <safe>"))
        {
            var html = await staleRejection.Content.ReadAsStringAsync(ct);
            Assert.Contains("saved-rejection-reason", html, StringComparison.Ordinal);
            Assert.Contains("Keep this reason &lt;safe&gt;", html, StringComparison.Ordinal);
            Assert.Contains("Refresh the queue", html, StringComparison.Ordinal);
        }
        using (var reject = await Post("Reject", rejectedId, "Missing documentation"))
        {
            Assert.Equal(HttpStatusCode.Redirect, reject.StatusCode);
            Assert.Contains("Account request rejected.", await client.GetStringAsync(reject.Headers.Location, ct), StringComparison.Ordinal);
        }

        await using var observer = Context();
        var approved = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == approvedId, ct);
        var rejected = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == rejectedId, ct);
        Assert.Equal(AccountRequestStatusCode.Approved, approved.AccountRequestStatusId);
        Assert.Equal(AccountRequestStatusCode.Rejected, rejected.AccountRequestStatusId);
        Assert.Equal("Missing documentation", rejected.ReviewNotes);
        Assert.Equal(1, await observer.Members.CountAsync(item => item.UserId == approved.CreatedUserId, ct));
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(item => item.EntityType == "account_requests" && item.EntityId == approvedId, ct));
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(item => item.EntityType == "account_requests" && item.EntityId == rejectedId, ct));
    }

    [Fact]
    public async Task BrowserApprovalDisplaysIdentityConflictWithoutDecision()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Admin, "browser-conflict-admin", ActorPassword, ct);
        var occupied = await SeedUserAsync(RoleCode.Member, "browser-conflict-user", ActorPassword, ct);
        var (id, _) = await SeedRequestAsync(occupied.Email, UniqueStudentNumber(), ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);
        using var response = await client.PostAsync("/AccountRequest/Approve?status=pending", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = id.ToString(), ["__RequestVerificationToken"] = token
        }), ct);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("This email is already in use.", html, StringComparison.Ordinal);
        Assert.Contains("Refresh the queue", html, StringComparison.Ordinal);
        await using var observer = Context();
        Assert.Equal(AccountRequestStatusCode.Pending, (await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == id, ct)).AccountRequestStatusId);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == id, ct));
    }

    [Fact]
    public async Task BrowserDecisionRejectsAnonymousAndMemberRequests()
    {
        var ct = TestContext.Current.CancellationToken;
        var member = await SeedUserAsync(RoleCode.Member, "browser-member", ActorPassword, ct);
        var (id, _) = await SeedRequestAsync(UniqueEmail("browser-forbidden"), UniqueStudentNumber(), ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var response = await anonymous.PostAsync($"/AccountRequest/Approve?id={id}", new FormUrlEncodedContent(new Dictionary<string, string>()), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, member.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);
        using var denied = await client.PostAsync($"/AccountRequest/Reject?id={id}", new FormUrlEncodedContent(new Dictionary<string, string> { ["reason"] = "Not allowed", ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location!.ToString(), StringComparison.Ordinal);
        await using var observer = Context();
        Assert.Equal(AccountRequestStatusCode.Pending, (await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == id, ct)).AccountRequestStatusId);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == id, ct));
    }

    [Fact]
    public async Task BulkApprovalApprovesEveryValidRecordAndReportsTheRefusedOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "bulk-mixed-actor", ActorPassword, ct);
        var takenNumber = UniqueStudentNumber();
        var first = await SeedRequestAsync(UniqueEmail("bulk-first"), UniqueStudentNumber(), ct);
        var second = await SeedRequestAsync(UniqueEmail("bulk-second"), UniqueStudentNumber(), ct);
        var refused = await SeedRequestAsync(UniqueEmail("bulk-refused"), takenNumber, ct);

        await using (var seed = Context())
        {
            seed.Members.Add(new Member
            {
                UserId = actor.Id,
                StudentNumber = takenNumber,
                Course = "BSIT",
                YearLevel = 1,
                Section = "A",
                MembershipStatusId = MembershipStatusCode.Active,
                CreatedBy = actor.Id
            });
            await seed.SaveChangesAsync(ct);
        }

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);

        using var response = await client.PostAsync(
            "/AccountRequest/ApproveSelected?status=pending",
            BulkForm(new[] { first.RequestId, second.RequestId, refused.RequestId }, token),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("Approval results", html, StringComparison.Ordinal);
        Assert.Contains("2 of 3 succeeded", html, StringComparison.Ordinal);
        Assert.Contains("1 failed", html, StringComparison.Ordinal);
        Assert.Contains("<details class=\"bulk-result-details\">", html, StringComparison.Ordinal);
        Assert.Contains("<summary>View all 3 results</summary>", html, StringComparison.Ordinal);
        Assert.Contains("This student number is already in use.", html, StringComparison.Ordinal);
        Assert.Contains($"data-request-id=\"{refused.RequestId}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-request-id=\"{first.RequestId}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-request-id=\"{second.RequestId}\"", html, StringComparison.Ordinal);

        await using var observer = Context();
        foreach (var approved in new[] { first, second })
        {
            var request = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == approved.RequestId, ct);
            Assert.Equal(AccountRequestStatusCode.Approved, request.AccountRequestStatusId);
            Assert.NotNull(request.CreatedUserId);
            Assert.Equal(1, await observer.Users.CountAsync(user => user.Id == request.CreatedUserId, ct));
            Assert.Equal(1, await observer.Members.CountAsync(member => member.UserId == request.CreatedUserId, ct));
            Assert.Equal(1, await observer.ActivityLogs.CountAsync(log => log.EntityType == "account_requests" && log.EntityId == approved.RequestId, ct));
        }

        var stillPending = await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == refused.RequestId, ct);
        Assert.Equal(AccountRequestStatusCode.Pending, stillPending.AccountRequestStatusId);
        Assert.False(await observer.ActivityLogs.AnyAsync(log => log.EntityType == "account_requests" && log.EntityId == refused.RequestId, ct));
    }

    [Fact]
    public async Task BulkApprovalRefusesARecordDecidedAfterThePageRendered()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "bulk-stale-actor", ActorPassword, ct);
        var email = UniqueEmail("bulk-stale");
        var studentNumber = UniqueStudentNumber();
        var (requestId, _) = await SeedRequestAsync(email, studentNumber, ct);

        await using (var writer = Context())
        {
            await CreateService(writer, new TestActor(actor.Id, RoleCodes.Officer)).ApproveAsync(requestId, actor.Id, ct);
        }

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);

        using var response = await client.PostAsync(
            "/AccountRequest/ApproveSelected?status=pending",
            BulkForm(new[] { requestId }, token),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("0 of 1 succeeded", html, StringComparison.Ordinal);
        Assert.Contains("1 failed", html, StringComparison.Ordinal);
        Assert.Contains("<summary>View 1 result</summary>", html, StringComparison.Ordinal);
        Assert.Contains("This account request has already been decided.", html, StringComparison.Ordinal);

        await using var observer = Context();
        Assert.Equal(1, await observer.Users.CountAsync(item => item.Email == email, ct));
        Assert.Equal(1, await observer.Members.CountAsync(item => item.StudentNumber == studentNumber, ct));
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
    }

    [Fact]
    public async Task BulkApprovalSubmittedTwiceCreatesNoDuplicateRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "bulk-repeat-actor", ActorPassword, ct);
        var firstEmail = UniqueEmail("bulk-repeat-first");
        var secondEmail = UniqueEmail("bulk-repeat-second");
        var firstNumber = UniqueStudentNumber();
        var secondNumber = UniqueStudentNumber();
        var first = await SeedRequestAsync(firstEmail, firstNumber, ct);
        var second = await SeedRequestAsync(secondEmail, secondNumber, ct);
        var ids = new[] { first.RequestId, second.RequestId };

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);

        using (var accepted = await client.PostAsync("/AccountRequest/ApproveSelected?status=pending", BulkForm(ids, token), ct))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            Assert.Contains("2 of 2 succeeded", await accepted.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        }

        using (var repeated = await client.PostAsync("/AccountRequest/ApproveSelected?status=pending", BulkForm(ids, token), ct))
        {
            Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
            var html = await repeated.Content.ReadAsStringAsync(ct);
            Assert.Contains("0 of 2 succeeded", html, StringComparison.Ordinal);
            Assert.Contains("This account request has already been decided.", html, StringComparison.Ordinal);
        }

        await using var observer = Context();
        Assert.Equal(2, await observer.Users.CountAsync(item => item.Email == firstEmail || item.Email == secondEmail, ct));
        Assert.Equal(2, await observer.Members.CountAsync(item => item.StudentNumber == firstNumber || item.StudentNumber == secondNumber, ct));
        Assert.Equal(2, await observer.ActivityLogs.CountAsync(item => item.EntityType == "account_requests" && (item.EntityId == first.RequestId || item.EntityId == second.RequestId), ct));
    }

    [Fact]
    public async Task BulkApprovalOverTheBoundDecidesNothingAndStatesTheLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "bulk-bound-actor", ActorPassword, ct);
        var (requestId, _) = await SeedRequestAsync(UniqueEmail("bulk-bound"), UniqueStudentNumber(), ct);
        var ids = new[] { requestId }.Concat(Enumerable.Range(100000, BulkApproval.MaxBatchSize)).ToArray();

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);

        using var response = await client.PostAsync("/AccountRequest/ApproveSelected?status=pending", BulkForm(ids, token), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains($"You can approve up to {BulkApproval.MaxBatchSize} account requests at a time.", html, StringComparison.Ordinal);

        await using var observer = Context();
        Assert.Equal(AccountRequestStatusCode.Pending,
            (await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct)).AccountRequestStatusId);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
    }

    [Fact]
    public async Task BulkApprovalEndpointRequiresAntiforgeryAndManagementPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        var officer = await SeedUserAsync(RoleCode.Officer, "bulk-guard-officer", ActorPassword, ct);
        var member = await SeedUserAsync(RoleCode.Member, "bulk-guard-member", ActorPassword, ct);
        var (requestId, _) = await SeedRequestAsync(UniqueEmail("bulk-guard"), UniqueStudentNumber(), ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);

        using (var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        using (var response = await anonymous.PostAsync("/AccountRequest/ApproveSelected?status=pending", BulkForm(new[] { requestId }, null), ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        using (var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        {
            await SignInAsync(client, member.Email, ActorPassword, ct);
            var token = await GetLayoutAntiforgeryTokenAsync(client, ct);
            using var response = await client.PostAsync("/AccountRequest/ApproveSelected?status=pending", BulkForm(new[] { requestId }, token), ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("AccessDenied", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        }

        using (var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }))
        {
            await SignInAsync(client, officer.Email, ActorPassword, ct);
            using var response = await client.PostAsync("/AccountRequest/ApproveSelected?status=pending", BulkForm(new[] { requestId }, null), ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await using var observer = Context();
        Assert.Equal(AccountRequestStatusCode.Pending,
            (await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct)).AccountRequestStatusId);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
    }

    [Fact]
    public async Task BulkApprovalWithoutASelectionDecidesNothingAndAsksForOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "bulk-empty-actor", ActorPassword, ct);
        var (requestId, _) = await SeedRequestAsync(UniqueEmail("bulk-empty"), UniqueStudentNumber(), ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);
        var token = await GetLayoutAntiforgeryTokenAsync(client, ct);

        using var response = await client.PostAsync(
            "/AccountRequest/ApproveSelected?status=pending",
            BulkForm(Array.Empty<int>(), token),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Select at least one account request to approve.", await response.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        await using var observer = Context();
        Assert.Equal(AccountRequestStatusCode.Pending,
            (await observer.AccountRequests.AsNoTracking().SingleAsync(item => item.RequestId == requestId, ct)).AccountRequestStatusId);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "account_requests" && item.EntityId == requestId, ct));
    }

    [Fact]
    public async Task BulkSelectionAppearsOnlyWherePendingRowsCanBeSelected()
    {
        var ct = TestContext.Current.CancellationToken;
        var actor = await SeedUserAsync(RoleCode.Officer, "bulk-surface-actor", ActorPassword, ct);
        var email = UniqueEmail("bulk-surface");
        await SeedRequestAsync(email, UniqueStudentNumber(), ct);

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, actor.Email, ActorPassword, ct);

        foreach (var url in new[]
        {
            "/AccountRequest?status=pending",
            $"/AccountRequest?status=pending&search={Uri.EscapeDataString(email)}",
            "/AccountRequest?status=pending&sort=oldest",
            "/AccountRequest?status=all"
        })
        {
            var html = await client.GetStringAsync(url, ct);

            // Every navigation is a fresh server render: the shared bar and dialog are present,
            // the count starts at zero and the bulk action starts disabled.
            Assert.Contains("data-bulk-root", html, StringComparison.Ordinal);
            Assert.Contains("data-bulk-dialog", html, StringComparison.Ordinal);
            Assert.Contains("0 selected", html, StringComparison.Ordinal);
            Assert.Matches(new Regex("<button[^>]*data-bulk-open=\"bulk-approve-dialog\"[^>]*disabled"), html);

            // The dialog posts back to the tab the reader is on, not to Pending.
            var expectedStatus = url.Contains("status=all", StringComparison.Ordinal) ? "all" : "pending";
            Assert.Contains($"action=\"/AccountRequest/ApproveSelected?status={expectedStatus}", html, StringComparison.Ordinal);

            // Pending rows are selectable, but nothing arrives already ticked.
            var rowInput = Regex.Match(html, "<input[^>]*data-bulk-row[^>]*>").Value;
            Assert.False(string.IsNullOrWhiteSpace(rowInput));
            Assert.DoesNotContain("checked", rowInput, StringComparison.OrdinalIgnoreCase);
        }

        // A tab with nothing selectable renders the table alone: no bar, no dialog, no column.
        var decided = await client.GetStringAsync("/AccountRequest?status=approved", ct);
        Assert.DoesNotContain("data-bulk-open", decided, StringComparison.Ordinal);
        Assert.DoesNotContain("data-bulk-dialog", decided, StringComparison.Ordinal);
        Assert.DoesNotContain("data-bulk-row", decided, StringComparison.Ordinal);
        Assert.DoesNotContain("select-column", decided, StringComparison.Ordinal);
    }

    private static FormUrlEncodedContent BulkForm(IEnumerable<int> ids, string? token, string status = "pending")
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("status", status)
        };
        values.AddRange(ids.Select(id => new KeyValuePair<string, string>("ids", id.ToString(CultureInfo.InvariantCulture))));
        if (token is not null)
        {
            values.Add(new KeyValuePair<string, string>("__RequestVerificationToken", token));
        }

        return new FormUrlEncodedContent(values);
    }

    private AccountRequestService CreateService(
        AsiBasecodeDBContext context,
        ICurrentActor actor,
        UnitOfWork? unitOfWork = null,
        IUserRepository? users = null,
        IMemberRepository? members = null)
    {
        var work = unitOfWork ?? new UnitOfWork(context);
        var userRepository = users ?? new UserRepository(work);
        var memberRepository = members ?? new MemberRepository(work);
        var auditWriter = new ActivityLogWriter(context, actor, TimeProvider.System);
        var memberService = new MemberService(memberRepository, userRepository, work, TimeProvider.System, actor, auditWriter);
        return new AccountRequestService(
            new AccountRequestRepository(work),
            userRepository,
            memberRepository,
            work,
            hasher,
            TimeProvider.System,
            actor,
            memberService,
            auditWriter);
    }

    private AsiBasecodeDBContext Context() => new(new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    private async Task<UserSeed> SeedUserAsync(RoleCode role, string prefix, string password, CancellationToken cancellationToken)
    {
        await using var context = Context();
        var email = UniqueEmail(prefix);
        var user = new User
        {
            Email = email,
            Name = "Management Actor",
            Password = hasher.Hash(password),
            RoleId = role,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return new UserSeed(user.Id, email);
    }

    private async Task<(int RequestId, string PasswordHash)> SeedRequestAsync(string email, string studentNumber, CancellationToken cancellationToken, string course = "BSIT")
    {
        await using var context = Context();
        var hash = hasher.Hash(ApplicantPassword);
        var request = new AccountRequest
        {
            Email = email,
            StudentNumber = studentNumber,
            FullName = "Ana Reyes",
            PasswordHash = hash,
            Course = course,
            YearLevel = 2,
            Section = "A",
            MobileNumber = "09171234567",
            AccountRequestStatusId = AccountRequestStatusCode.Pending,
            RequestedAt = DateTime.UtcNow
        };
        context.AccountRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);
        return (request.RequestId, hash);
    }

    private async Task SignInAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        using var page = await client.GetAsync("/Account/Login", cancellationToken);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync(cancellationToken);
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> GetLayoutAntiforgeryTokenAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/Home/Index", cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var token = Regex.Match(html, "<meta name=\"RequestVerificationToken\" content=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@attendta.test";

    private static string UniqueStudentNumber() => ProfileFixtures.NewStudentNumber();

    private sealed record UserSeed(int Id, string Email);

    private sealed class TestActor(int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => role;
    }

    private sealed class UserInsertRaceRepository(IUserRepository inner, Func<User, CancellationToken, Task> insertWinner) : IUserRepository
    {
        private int inserted;
        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) => inner.GetUsersAsync(cancellationToken);
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => inner.GetByEmailAsync(email, cancellationToken);
        public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetByIdAsync(userId, cancellationToken);
        public Task<User?> LockUserAsync(int userId, CancellationToken cancellationToken = default) => inner.LockUserAsync(userId, cancellationToken);
        public Task<string?> GetRoleCodeAsync(RoleCode roleId, CancellationToken cancellationToken = default) => inner.GetRoleCodeAsync(roleId, cancellationToken);

        public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref inserted, 1) == 0)
            {
                await insertWinner(user, cancellationToken);
            }

            await inner.AddAsync(user, cancellationToken);
        }
    }

    private sealed class MemberInsertRaceRepository(IMemberRepository inner, Func<Member, CancellationToken, Task> insertWinner) : IMemberRepository
    {
        private int inserted;
        public IQueryable<MemberRecord> GetMemberRecords() => inner.GetMemberRecords();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => inner.CountAsync(search, status, cancellationToken);
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => inner.GetPageAsync(search, status, skip, take, cancellationToken);
        public Task<MemberRecord?> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default) => inner.GetMemberRecordAsync(memberId, cancellationToken);
        public Task<MemberRecord?> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetMemberRecordByUserIdAsync(userId, cancellationToken);
        public Task<Member?> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetMemberByUserIdAsync(userId, cancellationToken);
        public Task<Member?> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) => inner.GetMemberAsync(memberId, cancellationToken);
        public void EnsureMemberCanRefresh(int memberId) => inner.EnsureMemberCanRefresh(memberId);
        public Task<Member?> GetMemberForEditAsync(int memberId, CancellationToken cancellationToken = default) => inner.GetMemberForEditAsync(memberId, cancellationToken);
        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default) => inner.UserHasMemberProfileAsync(userId, cancellationToken);
        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default) => inner.StudentNumberExistsAsync(studentNumber, excludeId, cancellationToken);
        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default) => inner.UpdateMemberAsync(member, cancellationToken);

        public async Task AddMemberAsync(Member member, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref inserted, 1) == 0)
            {
                await insertWinner(member, cancellationToken);
            }

            await inner.AddMemberAsync(member, cancellationToken);
        }
    }
}
