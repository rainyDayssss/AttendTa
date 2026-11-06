using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class MemberManagementTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private const string ManagementPassword = "MemberManagement#2026";
    private const string MemberPassword = "MemberProfile#2026";

    private AsiBasecodeDBContext Context(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(interceptors).Options);

    [Fact]
    public async Task CorrectionUpdatesLinkedProfileAuditsSafeValuesAndPreservesHistoryAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Inactive, ct);
        int registrationId;
        int certificateId;
        await using (var context = Context())
        {
            var type = new EventType
            {
                TypeName = $"Member test type {Guid.NewGuid():N}",
                Description = "Test event type",
                IsActive = true,
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow
            };
            var venue = new Venue
            {
                RoomName = $"Member test room {Guid.NewGuid():N}",
                Capacity = 30,
                ImagePath = "uploads/venues/member-test.jpg",
                IsActive = true,
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow
            };
            context.EventTypes.Add(type);
            context.Venues.Add(venue);
            await context.SaveChangesAsync(ct);

            var eventItem = new Event
            {
                Title = "Member profile historical event",
                TypeId = type.TypeId,
                VenueId = venue.VenueId,
                StartsAt = DateTime.UtcNow.AddDays(2),
                RegistrationDeadline = DateTime.UtcNow.AddDays(1),
                ParticipantLimit = 20,
                EventStatusId = EventStatusCode.Completed,
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow
            };
            context.Events.Add(eventItem);
            await context.SaveChangesAsync(ct);

            var registration = new Registration
            {
                MemberId = seeded.MemberId,
                EventId = eventItem.EventId,
                RegistrationStatusId = RegistrationStatusCode.Confirmed,
                CourseSnapshot = "Original course snapshot",
                YearLevelSnapshot = 2,
                SectionSnapshot = "OLD-A",
                SubmittedAt = DateTime.UtcNow.AddDays(-2),
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };
            context.Registrations.Add(registration);
            await context.SaveChangesAsync(ct);
            registrationId = registration.RegistrationId;

            var certificate = new Certificate
            {
                RegistrationId = registration.RegistrationId,
                CertificateStatusId = CertificateStatusCode.Issued,
                CertificateNumber = $"CERT-{Guid.NewGuid():N}",
                RecipientNameSnapshot = "Original member name",
                EventTitleSnapshot = eventItem.Title,
                EventStartsAtSnapshot = eventItem.StartsAt,
                IssuerNameSnapshot = "AttendTa",
                FilePath = "uploads/certificates/member-test.pdf",
                IssuedBy = seeded.ActorId,
                IssuedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            context.Certificates.Add(certificate);
            await context.SaveChangesAsync(ct);
            certificateId = certificate.CertificateId;
        }

        var correctedNumber = UniqueStudentNumber();
        await using (var writer = Context())
        {
            await CreateService(writer, seeded.ActorId).UpdateMemberAsync(new MemberViewModel
            {
                MemberId = seeded.MemberId,
                FullName = "Corrected Member Name",
                Email = "tampered@example.invalid",
                StudentNumber = correctedNumber,
                StudentNumberCorrectionReason = "Verified against the registrar's record.",
                Course = "Updated Computing",
                YearLevel = 3,
                Section = "NEW-B",
                MobileNumber = "555-0111",
                MembershipType = "Honors"
            }, ct);
        }

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var audit = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct);
        var registrationAfter = await observer.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registrationId, ct);
        var certificateAfter = await observer.Certificates.AsNoTracking().SingleAsync(item => item.CertificateId == certificateId, ct);

        Assert.Equal("Corrected Member Name", user.Name);
        Assert.Equal(seeded.MemberEmail, user.UserId);
        Assert.Equal(seeded.PasswordHash, user.Password);
        Assert.Equal(correctedNumber, member.StudentNumber);
        Assert.Equal("Updated Computing", member.Course);
        Assert.Equal(3, member.YearLevel);
        Assert.Equal("NEW-B", member.Section);
        Assert.Equal("555-0111", member.MobileNumber);
        Assert.Equal("Honors", member.MembershipType);
        Assert.Equal(MembershipStatusCode.Inactive, member.MembershipStatusId);
        Assert.Equal(seeded.ActorId, member.UpdatedBy);
        Assert.Equal(AuditActions.MemberStudentNumberCorrected, audit.Action);
        Assert.Equal("Verified against the registrar's record.", audit.Reason);
        Assert.Contains("\"FullName\":\"Original Member Name\"", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains($"\"StudentNumber\":\"{seeded.StudentNumber}\"", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains($"\"StudentNumber\":\"{correctedNumber}\"", audit.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"Status\":\"inactive\"", audit.NewValues, StringComparison.Ordinal);
        Assert.DoesNotContain("password", audit.OldValues + audit.NewValues, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seeded.PasswordHash, audit.OldValues + audit.NewValues, StringComparison.Ordinal);

        Assert.Equal("Original course snapshot", registrationAfter.CourseSnapshot);
        Assert.Equal(2, registrationAfter.YearLevelSnapshot);
        Assert.Equal("OLD-A", registrationAfter.SectionSnapshot);
        Assert.Equal("Original member name", certificateAfter.RecipientNameSnapshot);
        Assert.Equal("Member profile historical event", certificateAfter.EventTitleSnapshot);
        Assert.Equal(CertificateStatusCode.Issued, certificateAfter.CertificateStatusId);
    }

    [Fact]
    public async Task UnchangedStudentNumberNeedsNoReasonAndStillAuditsTheProfileEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using (var writer = Context())
        {
            await CreateService(writer, seeded.ActorId).UpdateMemberAsync(new MemberViewModel
            {
                MemberId = seeded.MemberId,
                FullName = "Renamed Member",
                StudentNumber = seeded.StudentNumber,
                Course = "Computing",
                YearLevel = 2,
                Section = "A",
                MembershipType = "Regular"
            }, ct);
        }

        await using var observer = Context();
        var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var audit = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct);
        Assert.Equal(MembershipStatusCode.Active, member.MembershipStatusId);
        Assert.Equal(AuditActions.MemberEdited, audit.Action);
        Assert.Null(audit.Reason);
    }

    [Fact]
    public async Task StudentNumberRaceReturnsAFieldSpecificDuplicateAndRollsBackWithoutAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using var writer = Context();
        var work = new UnitOfWork(writer);
        var repository = new InsertCompetingMemberRepository(
            new MemberRepository(work),
            (userId, studentNumber, token) => InsertCompetingMemberAsync(userId, studentNumber, token));
        var service = CreateService(writer, seeded.ActorId, work, repository);
        var candidateNumber = UniqueStudentNumber();
        repository.StudentNumberForRace = candidateNumber;

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.UpdateMemberAsync(new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "Attempted change",
            StudentNumber = candidateNumber,
            StudentNumberCorrectionReason = "Correction evidence",
            Course = "Computing",
            YearLevel = 2,
            Section = "A"
        }, ct));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(MemberViewModel.StudentNumber), exception.Field);
        await using var observer = Context();
        var unchanged = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var unchangedUser = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        Assert.Equal(seeded.StudentNumber, unchanged.StudentNumber);
        Assert.Equal("Original Member Name", unchangedUser.Name);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task ExistingDuplicateStudentNumberReturnsAFieldErrorWithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        var other = await SeedMemberAsync(MembershipStatusCode.Inactive, ct);
        await using var writer = Context();
        var service = CreateService(writer, seeded.ActorId);

        var exception = await Assert.ThrowsAsync<DomainException>(() => service.UpdateMemberAsync(new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "Must not change",
            StudentNumber = other.StudentNumber,
            StudentNumberCorrectionReason = "Incorrect duplicate correction",
            Course = "Must not change",
            YearLevel = 2,
            Section = "A"
        }, ct));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(MemberViewModel.StudentNumber), exception.Field);
        await using var observer = Context();
        var unchanged = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var unchangedUser = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        Assert.Equal(seeded.StudentNumber, unchanged.StudentNumber);
        Assert.Equal("Original course", unchanged.Course);
        Assert.Equal("Original Member Name", unchangedUser.Name);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task AuditInsertFailureRollsBackTheMemberAndUserChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using var writer = Context(new FailWhenActivityLogIsSaved());
        var service = CreateService(writer, seeded.ActorId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateMemberAsync(new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "Should roll back",
            StudentNumber = seeded.StudentNumber,
            Course = "Changed course",
            YearLevel = 4,
            Section = "ROLLBACK"
        }, ct));

        await using var observer = Context();
        var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        Assert.Equal(seeded.StudentNumber, member.StudentNumber);
        Assert.Equal("Original course", member.Course);
        Assert.Equal(2, member.YearLevel);
        Assert.Equal("Original Member Name", user.Name);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task EditWaitsForTheLinkedUserRowLock()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        var signal = new UserLockCommandSignal();
        await using var writer = Context(signal);
        var service = CreateService(writer, seeded.ActorId);
        await using var lockOwner = Context();
        await using var transaction = await lockOwner.Database.BeginTransactionAsync(ct);
        Assert.NotNull(await new UserRepository(new UnitOfWork(lockOwner)).LockUserAsync(seeded.UserId, ct));

        var pending = service.UpdateMemberAsync(new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "After lock release",
            StudentNumber = seeded.StudentNumber,
            Course = "Original course",
            YearLevel = 2,
            Section = "A"
        }, ct);
        var committed = false;
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(300, ct)));
            await transaction.CommitAsync(ct);
            committed = true;
            await pending;
        }
        finally
        {
            if (!committed) await transaction.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
        }

        await using var observer = Context();
        Assert.Equal("After lock release", (await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct)).Name);
    }

    [Fact]
    public async Task EditEndpointShowsReadOnlyEmailAndCorrectionValidationAndHasNoStatusToggle()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Inactive, ct, withLoginPasswords: true);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var managementClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(managementClient, seeded.ActorEmail, ManagementPassword, ct);

        using var page = await managementClient.GetAsync($"/Member/Edit/{seeded.MemberId}", ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(ct);
        Assert.Contains("name=\"Email\"", html, StringComparison.Ordinal);
        Assert.Contains("readonly", html, StringComparison.Ordinal);
        Assert.Contains("name=\"FullName\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"StudentNumberCorrectionReason\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Status\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleStatus", html, StringComparison.Ordinal);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));

        using (var missingToken = await managementClient.PostAsync("/Member/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["MemberId"] = seeded.MemberId.ToString()
        }), ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        }

        using var response = await managementClient.PostAsync("/Member/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["MemberId"] = seeded.MemberId.ToString(),
            ["FullName"] = "Tampered attempt",
            ["Email"] = "attacker@example.invalid",
            ["StudentNumber"] = UniqueStudentNumber(),
            ["StudentNumberCorrectionReason"] = " ",
            ["Course"] = "Changed course",
            ["YearLevel"] = "3",
            ["Section"] = "B",
            ["Status"] = "active"
        }), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseHtml = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("A reason is required when correcting the student number.", responseHtml, StringComparison.Ordinal);
        Assert.Contains($"value=\"{seeded.MemberEmail}\"", responseHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker@example.invalid", responseHtml, StringComparison.Ordinal);

        using var invalidModelResponse = await managementClient.PostAsync("/Member/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["MemberId"] = seeded.MemberId.ToString(),
            ["FullName"] = "",
            ["Email"] = "invalid-model@example.invalid",
            ["StudentNumber"] = seeded.StudentNumber,
            ["StudentNumberCorrectionReason"] = "",
            ["Course"] = "",
            ["YearLevel"] = "3",
            ["Section"] = "B"
        }), ct);
        Assert.Equal(HttpStatusCode.OK, invalidModelResponse.StatusCode);
        var invalidModelHtml = await invalidModelResponse.Content.ReadAsStringAsync(ct);
        Assert.Contains($"value=\"{seeded.MemberEmail}\"", invalidModelHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid-model@example.invalid", invalidModelHtml, StringComparison.Ordinal);

        await using (var observer = Context())
        {
            var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
            Assert.Equal(seeded.StudentNumber, member.StudentNumber);
            Assert.Equal(MembershipStatusCode.Inactive, member.MembershipStatusId);
            Assert.Equal("Original Member Name", user.Name);
            Assert.Equal(seeded.MemberEmail, user.UserId);
            Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
        }

        using var togglePost = await managementClient.PostAsync($"/Member/ToggleStatus?id={seeded.MemberId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.NotFound, togglePost.StatusCode);

        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(memberClient, seeded.MemberEmail, MemberPassword, ct);
        using var denied = await memberClient.GetAsync($"/Member/Edit/{seeded.MemberId}", ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SeededMember> SeedMemberAsync(MembershipStatusCode status, CancellationToken cancellationToken, bool withLoginPasswords = false)
    {
        await using var context = Context();
        var actorEmail = UniqueEmail("management");
        var memberEmail = UniqueEmail("member-profile");
        var hasher = new Argon2PasswordHasher();
        var actor = new User
        {
            UserId = actorEmail,
            Name = "Management Actor",
            Password = withLoginPasswords ? hasher.Hash(ManagementPassword) : "integration-password-hash",
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        var linkedUser = new User
        {
            UserId = memberEmail,
            Name = "Original Member Name",
            Password = withLoginPasswords ? hasher.Hash(MemberPassword) : "member-password-hash",
            RoleId = RoleCode.Member,
            IsActive = true
        };
        context.Users.AddRange(actor, linkedUser);
        await context.SaveChangesAsync(cancellationToken);

        var member = new Member
        {
            UserId = linkedUser.Id,
            StudentNumber = $"ORIGINAL-{Guid.NewGuid():N}",
            Course = "Original course",
            YearLevel = 2,
            Section = "A",
            MobileNumber = "555-0100",
            MembershipType = "Regular",
            MembershipStatusId = status,
            CreatedBy = actor.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(cancellationToken);
        return new SeededMember(actor.Id, actorEmail, member.MemberId, linkedUser.Id, memberEmail, linkedUser.Password, member.StudentNumber);
    }

    private async Task InsertCompetingMemberAsync(int userId, string studentNumber, CancellationToken cancellationToken)
    {
        await using var context = Context();
        context.Members.Add(new Member
        {
            UserId = userId,
            StudentNumber = studentNumber,
            Course = "Competitor course",
            YearLevel = 1,
            Section = "RACE",
            MembershipStatusId = MembershipStatusCode.Active,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static MemberService CreateService(
        AsiBasecodeDBContext context,
        int actorId,
        UnitOfWork? unitOfWork = null,
        IMemberRepository? memberRepository = null)
    {
        var work = unitOfWork ?? new UnitOfWork(context);
        var actor = new TestActor(actorId, "officer");
        return new MemberService(
            memberRepository ?? new MemberRepository(work),
            new UserRepository(work),
            work,
            TimeProvider.System,
            actor,
            new ActivityLogWriter(context, actor, TimeProvider.System));
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

    private static string UniqueStudentNumber() => $"CORRECTED-{Guid.NewGuid():N}";
    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@attendta.test";

    private sealed record SeededMember(int ActorId, string ActorEmail, int MemberId, int UserId, string MemberEmail, string PasswordHash, string StudentNumber);

    private sealed class TestActor(int userId, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => userId;
        public string Role => role;
    }

    private sealed class InsertCompetingMemberRepository(
        IMemberRepository inner,
        Func<int, string, CancellationToken, Task> insertWinner) : IMemberRepository
    {
        private int inserted;
        public string StudentNumberForRace { get; set; } = string.Empty;
        public IQueryable<MemberRecord> GetMemberRecords() => inner.GetMemberRecords();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => inner.CountAsync(search, status, cancellationToken);
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => inner.GetPageAsync(search, status, skip, take, cancellationToken);
        public MemberRecord GetMemberRecord(int memberId) => inner.GetMemberRecord(memberId);
        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetMemberRecordByUserIdAsync(userId, cancellationToken);
        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetMemberByUserIdAsync(userId, cancellationToken);
        public Member GetMember(int memberId) => inner.GetMember(memberId);
        public bool UserHasMemberProfile(int userId) => inner.UserHasMemberProfile(userId);
        public bool StudentNumberExists(string studentNumber, int? excludeId = null) => inner.StudentNumberExists(studentNumber, excludeId);

        public async Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref inserted, 1) == 0)
            {
                await insertWinner(member.CreatedBy, StudentNumberForRace, cancellationToken);
            }

            await inner.UpdateMemberAsync(member, cancellationToken);
        }

        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default) => inner.AddMemberAsync(member, cancellationToken);
    }

    private sealed class FailWhenActivityLogIsSaved : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<ActivityLog>().Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new InvalidOperationException("Audit insert failed.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class UserLockCommandSignal : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("dbo.users", StringComparison.OrdinalIgnoreCase))
            {
                Started.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }
}
