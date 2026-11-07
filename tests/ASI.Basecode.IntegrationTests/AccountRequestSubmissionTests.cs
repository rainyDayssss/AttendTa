using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Anonymous membership request submission through the real MVC pipeline and SQL Server schema.
/// </summary>
public class AccountRequestSubmissionTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Password = "Applicant#2026";
    private const string ExistingEmail = "existing-applicant@attendta.test";
    private const string InactiveEmail = "inactive-applicant@attendta.test";
    private const string PendingEmail = "pending-applicant@attendta.test";
    private const string ExistingStudentNumber = "2023-09999";
    private const string PendingStudentNumber = "2023-08888";
    private const string MemberOwnerEmail = "member-owner@attendta.test";
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 5, 8, 30, 0, TimeSpan.Zero);

    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory;
    private WebApplicationFactory<Program> clockFactory;
    private HttpClient client;

    public AccountRequestSubmissionTests(SqlServerDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        clockFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(new FixedClock(FixedNow))));
        client = clockFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (clockFactory is not null)
        {
            await clockFactory.DisposeAsync();
        }

        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnonymousVisitorOpensTheRequestFormWithoutSignIn()
    {
        var response = await client.GetAsync("/AccountRequest/Request", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Request membership", html, StringComparison.Ordinal);
        Assert.Contains("name=\"StudentNumber\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"FullName\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Email\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Course\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"YearLevel\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Section\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"MobileNumber\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Password\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ConfirmPassword\"", html, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", html, StringComparison.Ordinal);
        Assert.Contains("public-wide", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign out", html, StringComparison.Ordinal);
        var yearLevel = Regex.Match(html, "<select[^>]*name=\"YearLevel\"[^>]*>.*?</select>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        Assert.True(yearLevel.Success, "No YearLevel control.");
        Assert.DoesNotContain("value=\"0\"", yearLevel.Value, StringComparison.Ordinal);
        Assert.Contains("value=\"1\"", yearLevel.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginPageLinksToTheRequestForm()
    {
        var html = await client.GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);

        Assert.Contains("Request membership", html, StringComparison.Ordinal);
        Assert.Contains("/AccountRequest/Request", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidSubmissionCreatesOnePendingRequestWithAnArgon2idHashAndNoAccount()
    {
        var email = NewEmail();
        var studentNumber = NewStudentNumber();
        var beforeUsers = await CountUsersAsync();
        var beforeMembers = await CountMembersAsync();
        var token = await GetAntiforgeryTokenAsync();

        var response = await SubmitAsync(ValidFields(email, studentNumber), token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/AccountRequest/Submitted", GetLocationPath(response));

        await using var context = CreateContext();
        var request = await context.AccountRequests.SingleAsync(
            item => item.Email == email,
            TestContext.Current.CancellationToken);
        Assert.Equal(AccountRequestStatusCode.Pending, request.AccountRequestStatusId);
        Assert.Equal(studentNumber, request.StudentNumber);
        Assert.Equal("Ana Reyes", request.FullName);
        Assert.Equal("BSIT", request.Course);
        Assert.Equal(2, request.YearLevel);
        Assert.Equal("A", request.Section);
        Assert.Equal("09171234567", request.MobileNumber);
        Assert.Equal(FixedNow.UtcDateTime, request.RequestedAt);
        Assert.StartsWith("$argon2id$", request.PasswordHash, StringComparison.Ordinal);
        Assert.True(hasher.Verify(Password, request.PasswordHash));
        Assert.NotEqual(Password, request.PasswordHash);
        Assert.Null(request.CreatedUserId);
        Assert.Null(request.ReviewedBy);
        Assert.Null(request.ReviewedAt);
        Assert.Null(request.ReviewNotes);
        Assert.Equal(beforeUsers, await context.Users.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(beforeMembers, await context.Members.CountAsync(TestContext.Current.CancellationToken));
        Assert.False(await context.Users.AnyAsync(user => user.UserId == email, TestContext.Current.CancellationToken));
        Assert.False(await context.Members.AnyAsync(member => member.StudentNumber == studentNumber, TestContext.Current.CancellationToken));

        var acknowledgment = await client.GetAsync("/AccountRequest/Submitted", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, acknowledgment.StatusCode);
        var html = await acknowledgment.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Request submitted", html, StringComparison.Ordinal);
        Assert.Contains("pending review", html, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateEmailForAnExistingUserRedisplaysTheEmailError()
    {
        var token = await GetAntiforgeryTokenAsync();

        var response = await SubmitAsync(ValidFields(ExistingEmail, NewStudentNumber()), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        AssertFieldError(html, "Email");
        Assert.DoesNotContain(Password, html, StringComparison.Ordinal);

        await using var context = CreateContext();
        Assert.False(await context.AccountRequests.AnyAsync(
            request => request.Email == ExistingEmail,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DuplicateEmailForAnInactiveUserRedisplaysTheEmailError()
    {
        var token = await GetAntiforgeryTokenAsync();

        var response = await SubmitAsync(ValidFields(InactiveEmail, NewStudentNumber()), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        AssertFieldError(html, "Email");

        await using var context = CreateContext();
        Assert.False(await context.AccountRequests.AnyAsync(
            request => request.Email == InactiveEmail,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DuplicateStudentNumberForAnInactiveMemberRedisplaysTheStudentNumberError()
    {
        var token = await GetAntiforgeryTokenAsync();

        var response = await SubmitAsync(ValidFields(NewEmail(), ExistingStudentNumber), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        AssertFieldError(html, "StudentNumber");

        await using var context = CreateContext();
        Assert.False(await context.AccountRequests.AnyAsync(
            request => request.StudentNumber == ExistingStudentNumber,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DuplicatePendingRequestRedisplaysTheMatchingFieldError()
    {
        var token = await GetAntiforgeryTokenAsync();
        await using var context = CreateContext();
        var before = await context.AccountRequests.CountAsync(TestContext.Current.CancellationToken);

        var duplicateEmail = await SubmitAsync(ValidFields(PendingEmail, NewStudentNumber()), token);
        Assert.Equal(HttpStatusCode.OK, duplicateEmail.StatusCode);
        AssertFieldError(await duplicateEmail.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), "Email");

        var duplicateStudentNumber = await SubmitAsync(ValidFields(NewEmail(), PendingStudentNumber), token);
        Assert.Equal(HttpStatusCode.OK, duplicateStudentNumber.StatusCode);
        AssertFieldError(await duplicateStudentNumber.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), "StudentNumber");

        Assert.Equal(before, await context.AccountRequests.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvalidFieldsRedisplayWithFieldErrorsAndClearPasswords()
    {
        var token = await GetAntiforgeryTokenAsync();
        await using var context = CreateContext();
        var before = await context.AccountRequests.CountAsync(TestContext.Current.CancellationToken);

        var empty = await SubmitAsync(new Dictionary<string, string>(), token);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        var emptyHtml = await empty.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        AssertFieldError(emptyHtml, "StudentNumber");
        AssertFieldError(emptyHtml, "FullName");
        AssertFieldError(emptyHtml, "Email");
        AssertFieldError(emptyHtml, "Course");
        AssertFieldError(emptyHtml, "YearLevel");
        AssertFieldError(emptyHtml, "Section");
        AssertFieldError(emptyHtml, "Password");

        var fields = ValidFields(NewEmail(), NewStudentNumber());
        fields["YearLevel"] = "0";
        fields["ConfirmPassword"] = "Different#2026";
        var invalid = await SubmitAsync(fields, token);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidHtml = await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        AssertFieldError(invalidHtml, "YearLevel");
        AssertFieldError(invalidHtml, "ConfirmPassword");
        Assert.DoesNotContain(Password, invalidHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Different#2026", invalidHtml, StringComparison.Ordinal);

        Assert.Equal(before, await context.AccountRequests.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AjaxDuplicateReturnsConflictJson()
    {
        var token = await GetAntiforgeryTokenAsync();

        var response = await SubmitAsync(ValidFields(ExistingEmail, NewStudentNumber()), token, ajax: true);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Duplicate", json.RootElement.GetProperty("outcome").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task AcceptJsonHeaderAlsoReturnsJson()
    {
        var token = await GetAntiforgeryTokenAsync();

        var response = await SubmitAsync(ValidFields(ExistingEmail, NewStudentNumber()), token, acceptJson: true);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Duplicate", json.RootElement.GetProperty("outcome").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task AjaxInvalidModelReturnsBadRequestJson()
    {
        var token = await GetAntiforgeryTokenAsync();

        var response = await SubmitAsync(new Dictionary<string, string>(), token, ajax: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Invalid", json.RootElement.GetProperty("outcome").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task PostWithoutAntiforgeryTokenIsRejected()
    {
        var email = NewEmail();

        var response = await client.PostAsync(
            "/AccountRequest/Request",
            new FormUrlEncodedContent(ValidFields(email, NewStudentNumber())),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var context = CreateContext();
        Assert.False(await context.AccountRequests.AnyAsync(
            request => request.Email == email,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentIdenticalSubmissionsCreateExactlyOnePendingRequest()
    {
        var email = NewEmail();
        var studentNumber = NewStudentNumber();
        var token = await GetAntiforgeryTokenAsync();
        var fields = ValidFields(email, studentNumber);

        var responses = await Task.WhenAll(SubmitAsync(fields, token), SubmitAsync(fields, token));

        try
        {
            Assert.Equal(1, responses.Count(response =>
                response.StatusCode == HttpStatusCode.Redirect
                && GetLocationPath(response) == "/AccountRequest/Submitted"));
            Assert.Equal(1, responses.Count(response =>
                response.StatusCode == HttpStatusCode.OK
                || (response.StatusCode == HttpStatusCode.Redirect
                    && GetLocationPath(response) == "/AccountRequest/Request")));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var context = CreateContext();
        Assert.Equal(1, await context.AccountRequests.CountAsync(
            request => request.Email == email,
            TestContext.Current.CancellationToken));
    }

    private static Dictionary<string, string> ValidFields(string email, string studentNumber)
    {
        return new Dictionary<string, string>
        {
            ["StudentNumber"] = studentNumber,
            ["FullName"] = "Ana Reyes",
            ["Email"] = email,
            ["Course"] = "BSIT",
            ["YearLevel"] = "2",
            ["Section"] = "A",
            ["MobileNumber"] = "09171234567",
            ["Password"] = Password,
            ["ConfirmPassword"] = Password
        };
    }

    private static string NewEmail() => $"applicant-{Guid.NewGuid():N}@attendta.test";

    private static string NewStudentNumber() => $"2026-{Guid.NewGuid():N}";

    private static string? GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        if (location is null)
        {
            return null;
        }

        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }

    private static void AssertFieldError(string html, string field)
    {
        var input = Regex.Match(html, $"<(?:input|select|textarea)\\b[^>]*\\bname=\"{field}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"No control for {field}.");
        Assert.Contains($"aria-describedby=\"{field}-validation\"", input.Value, StringComparison.Ordinal);
        var error = Regex.Match(html, $"<span[^>]*id=\"{field}-validation\"[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(error.Success, $"No error container for {field}.");
        Assert.False(string.IsNullOrWhiteSpace(Regex.Replace(error.Groups[1].Value, "<[^>]+>", "")), $"No server error for {field}.");
    }

    private async Task<HttpResponseMessage> SubmitAsync(IReadOnlyDictionary<string, string> fields, string token, bool ajax = false, bool acceptJson = false)
    {
        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = token
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/AccountRequest/Request")
        {
            Content = new FormUrlEncodedContent(form)
        };
        if (ajax)
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }

        if (acceptJson)
        {
            request.Headers.Accept.ParseAdd("application/json");
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> GetAntiforgeryTokenAsync()
    {
        var response = await client.GetAsync("/AccountRequest/Request", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, "No antiforgery token was rendered on the request form.");
        return match.Groups[1].Value;
    }

    private AsiBasecodeDBContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        return new AsiBasecodeDBContext(options);
    }

    private async Task<int> CountUsersAsync()
    {
        await using var context = CreateContext();
        return await context.Users.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> CountMembersAsync()
    {
        await using var context = CreateContext();
        return await context.Members.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedAsync()
    {
        await using var context = CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        if (!await context.Users.AnyAsync(user => user.UserId == ExistingEmail, cancellationToken))
        {
            context.Users.AddRange(
                CreateUser(ExistingEmail, "Existing Applicant", isActive: true),
                CreateUser(InactiveEmail, "Inactive Applicant", isActive: false),
                CreateUser(MemberOwnerEmail, "Member Owner", isActive: true));
            await context.SaveChangesAsync(cancellationToken);
        }

        var ownerId = await context.Users
            .Where(user => user.UserId == MemberOwnerEmail)
            .Select(user => user.Id)
            .SingleAsync(cancellationToken);

        if (!await context.Members.AnyAsync(member => member.StudentNumber == ExistingStudentNumber, cancellationToken))
        {
            context.Members.Add(new Member
            {
                UserId = ownerId,
                StudentNumber = ExistingStudentNumber,
                Course = "BSIT",
                YearLevel = 2,
                Section = "A",
                MembershipStatusId = MembershipStatusCode.Inactive,
                CreatedBy = ownerId,
                CreatedAt = FixedNow.UtcDateTime
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        if (!await context.AccountRequests.AnyAsync(request => request.Email == PendingEmail, cancellationToken))
        {
            context.AccountRequests.Add(new AccountRequest
            {
                StudentNumber = PendingStudentNumber,
                FullName = "Pending Applicant",
                Email = PendingEmail,
                PasswordHash = hasher.Hash(Password),
                Course = "BSIT",
                YearLevel = 1,
                Section = "B",
                AccountRequestStatusId = AccountRequestStatusCode.Pending,
                RequestedAt = FixedNow.UtcDateTime.AddDays(-1)
            });
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private User CreateUser(string email, string name, bool isActive)
    {
        return new User
        {
            UserId = email,
            Name = name,
            Password = hasher.Hash(Password),
            RoleId = RoleCode.Member,
            IsActive = isActive
        };
    }

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset now;

        public FixedClock(DateTimeOffset now)
        {
            this.now = now;
        }

        public override DateTimeOffset GetUtcNow() => now;
    }
}
