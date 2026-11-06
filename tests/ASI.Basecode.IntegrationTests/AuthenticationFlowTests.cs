using System.Net;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// End-to-end cookie authentication and policy checks through the real MVC pipeline.
/// </summary>
public class AuthenticationFlowTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string AdminEmail = "flow-admin@attendta.test";
    private const string OfficerEmail = "flow-officer@attendta.test";
    private const string MemberEmail = "flow-member@attendta.test";
    private const string SecondMemberEmail = "flow-member-two@attendta.test";
    private const string MissingProfileEmail = "flow-member-no-profile@attendta.test";
    private const string InactiveEmail = "flow-inactive@attendta.test";
    private const string Password = "FlowPassword#2026";
    private const string CookieName = "AttendTa.Auth";

    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory;
    private HttpClient client;

    public AuthenticationFlowTests(SqlServerDatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await SeedUsersAsync();

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnonymousRequestToMemberRedirectsToLoginWithReturnUrl()
    {
        var response = await client.GetAsync("/Member", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = GetLocationPath(response);
        Assert.NotNull(location);
        Assert.StartsWith("/Account/Login", location, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2FMember", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdministratorSignsInAndOpensMemberManagement()
    {
        var login = await SignInAsync(AdminEmail, Password);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Home/Index", GetLocationPath(login));
        Assert.Contains($"{CookieName}=", GetSetCookieHeader(login), StringComparison.Ordinal);

        var member = await client.GetAsync("/Member", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, member.StatusCode);
    }

    [Fact]
    public async Task OfficerOpensMemberManagement()
    {
        var login = await SignInAsync(OfficerEmail, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var member = await client.GetAsync("/Member", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, member.StatusCode);
    }

    [Fact]
    public async Task MemberIsRedirectedToAccessDenied()
    {
        var login = await SignInAsync(MemberEmail, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        await EnsureMemberProfilesAsync();
        var anotherMemberId = await GetMemberIdAsync(SecondMemberEmail);
        try
        {
            foreach (var path in new[] { "/Member", $"/Member/Edit/{anotherMemberId}" })
            {
                var member = await client.GetAsync(path, TestContext.Current.CancellationToken);

                Assert.True(member.StatusCode == HttpStatusCode.Redirect, $"Expected {path} to redirect, received {member.StatusCode}.");
                Assert.StartsWith("/Account/AccessDenied", GetLocationPath(member), StringComparison.Ordinal);
            }

            var accessDenied = await client.GetAsync("/Account/AccessDenied", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, accessDenied.StatusCode);
            Assert.Contains("Access denied", await accessDenied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        finally
        {
            await RemoveMemberProfilesAsync();
        }
    }

    [Fact]
    public async Task AnonymousProfileRequestRedirectsToLoginWithReturnUrl()
    {
        var response = await client.GetAsync("/Profile", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = GetLocationPath(response);
        Assert.NotNull(location);
        Assert.StartsWith("/Account/Login", location, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2FProfile", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MemberProfileShowsOnlyTheAuthenticatedOwnersReadOnlyDetails()
    {
        await EnsureMemberProfilesAsync();
        try
        {
            var firstLogin = await SignInAsync(MemberEmail, Password);
            Assert.Equal(HttpStatusCode.Redirect, firstLogin.StatusCode);

            var firstProfile = await client.GetAsync("/Profile", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, firstProfile.StatusCode);
            var firstHtml = await firstProfile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("Flow Member", firstHtml, StringComparison.Ordinal);
            Assert.Contains(MemberEmail, firstHtml, StringComparison.Ordinal);
            Assert.Contains("FLOW-IT-1001", firstHtml, StringComparison.Ordinal);
            Assert.Contains("Computer Science", firstHtml, StringComparison.Ordinal);
            Assert.Contains("CS-A", firstHtml, StringComparison.Ordinal);
            Assert.Contains("555-0101", firstHtml, StringComparison.Ordinal);
            Assert.Contains("Regular", firstHtml, StringComparison.Ordinal);
            Assert.Contains("Membership status", firstHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Flow Second Member", firstHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("FLOW-IT-2002", firstHtml, StringComparison.Ordinal);
            AssertReadOnlyProfile(firstHtml);

            using var secondClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            var secondLogin = await SignInAsync(secondClient, SecondMemberEmail, Password);
            Assert.Equal(HttpStatusCode.Redirect, secondLogin.StatusCode);

            var secondProfile = await secondClient.GetAsync("/Profile", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, secondProfile.StatusCode);
            var secondHtml = await secondProfile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("Flow Second Member", secondHtml, StringComparison.Ordinal);
            Assert.Contains(SecondMemberEmail, secondHtml, StringComparison.Ordinal);
            Assert.Contains("FLOW-IT-2002", secondHtml, StringComparison.Ordinal);
            Assert.Contains("Information Systems", secondHtml, StringComparison.Ordinal);
            Assert.Contains("IS-B", secondHtml, StringComparison.Ordinal);
            Assert.Contains("555-0202", secondHtml, StringComparison.Ordinal);
            Assert.Contains("Associate", secondHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Flow Member", secondHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("FLOW-IT-1001", secondHtml, StringComparison.Ordinal);
            AssertReadOnlyProfile(secondHtml);
        }
        finally
        {
            await RemoveMemberProfilesAsync();
        }
    }

    [Fact]
    public async Task MemberWithoutLinkedProfileGetsNotFoundWithoutOtherMemberData()
    {
        var login = await SignInAsync(MissingProfileEmail, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/Profile", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Flow Member", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Flow Second Member", body, StringComparison.Ordinal);
        Assert.DoesNotContain("FLOW-IT-1001", body, StringComparison.Ordinal);
        Assert.DoesNotContain("FLOW-IT-2002", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignInIssuesANonPersistentEightHourCookie()
    {
        var login = await SignInAsync(AdminEmail, Password);

        var setCookie = GetSetCookieHeader(login);
        Assert.Contains($"{CookieName}=", setCookie, StringComparison.Ordinal);
        Assert.DoesNotContain("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongPasswordShowsTheGenericMessageAndIssuesNoCookie()
    {
        var response = await SignInAsync(AdminEmail, "wrong-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain($"{CookieName}=", GetSetCookieHeader(response), StringComparison.Ordinal);
        Assert.Contains("Incorrect email or password.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownEmailShowsTheSameGenericMessageAsAWrongPassword()
    {
        var wrongPassword = await SignInAsync(AdminEmail, "wrong-password");
        var wrongPasswordHtml = await wrongPassword.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var unknownEmail = await SignInAsync("nobody@attendta.test", Password);
        var unknownEmailHtml = await unknownEmail.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, unknownEmail.StatusCode);
        Assert.DoesNotContain($"{CookieName}=", GetSetCookieHeader(unknownEmail), StringComparison.Ordinal);
        Assert.Contains("Incorrect email or password.", unknownEmailHtml, StringComparison.Ordinal);
        Assert.Equal(ExtractGenericFailure(wrongPasswordHtml), ExtractGenericFailure(unknownEmailHtml));
    }

    [Fact]
    public async Task InactiveAccountCannotSignIn()
    {
        var response = await SignInAsync(InactiveEmail, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain($"{CookieName}=", GetSetCookieHeader(response), StringComparison.Ordinal);
        Assert.Contains("Incorrect email or password.", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeactivatedSessionIsRejectedOnTheNextRequest()
    {
        var login = await SignInAsync(AdminEmail, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Member", TestContext.Current.CancellationToken)).StatusCode);

        await SetActiveAsync(AdminEmail, false);
        try
        {
            var afterDeactivation = await client.GetAsync("/Member", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Redirect, afterDeactivation.StatusCode);
            Assert.StartsWith("/Account/Login", GetLocationPath(afterDeactivation), StringComparison.Ordinal);
        }
        finally
        {
            await SetActiveAsync(AdminEmail, true);
        }
    }

    [Fact]
    public async Task DemotedSessionIsRejectedOnTheNextRequest()
    {
        var login = await SignInAsync(AdminEmail, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        await SetRoleAsync(AdminEmail, RoleCode.Member);
        try
        {
            var afterDemotion = await client.GetAsync("/Member", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Redirect, afterDemotion.StatusCode);
            Assert.StartsWith("/Account/Login", GetLocationPath(afterDemotion), StringComparison.Ordinal);
        }
        finally
        {
            await SetRoleAsync(AdminEmail, RoleCode.Admin);
        }
    }

    [Fact]
    public async Task GetLogoutIsNotAllowed()
    {
        var login = await SignInAsync(AdminEmail, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/Account/Logout", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task PostLogoutWithAntiforgeryTokenSignsTheUserOut()
    {
        var login = await SignInAsync(AdminEmail, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var token = await GetAntiforgeryTokenAsync("/Member");
        var logout = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        // Account/Login are the route defaults, so the generated login URL is "/".
        Assert.Equal("/", GetLocationPath(logout));

        var loginPage = await client.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        Assert.Contains("Sign in", await loginPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        var afterLogout = await client.GetAsync("/Member", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.StartsWith("/Account/Login", GetLocationPath(afterLogout), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostLogoutWithoutAntiforgeryTokenIsRejected()
    {
        var login = await SignInAsync(AdminEmail, Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var logout = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>()),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
    }

    [Fact]
    public async Task AnonymousShellHasNoAuthenticatedNavigation()
    {
        var html = await (await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Mobile navigation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/Member", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign out", html, StringComparison.Ordinal);
        Assert.Contains("name=\"RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AdminEmail, true)]
    [InlineData(OfficerEmail, true)]
    [InlineData(MemberEmail, false)]
    public async Task ShellShowsOnlyAvailableRoleNavigation(string email, bool management)
    {
        await SignInAsync(email, Password);
        var html = await (await client.GetAsync("/Home/Index", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Mobile navigation", html, StringComparison.Ordinal);
        Assert.Contains("Sign out", html, StringComparison.Ordinal);
        Assert.Contains("/css/app.css", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
        if (management) { Assert.Contains("class=\"management-topbar\"", html, StringComparison.Ordinal); Assert.Contains("/Member", html, StringComparison.Ordinal); Assert.Contains("/EventType", html, StringComparison.Ordinal); Assert.Contains("/Venue", html, StringComparison.Ordinal); Assert.DoesNotContain("/Profile", html, StringComparison.Ordinal); }
        else { Assert.DoesNotContain("class=\"management-topbar\"", html, StringComparison.Ordinal); Assert.DoesNotContain("/Member", html, StringComparison.Ordinal); Assert.DoesNotContain("/EventType", html, StringComparison.Ordinal); Assert.DoesNotContain("/Venue", html, StringComparison.Ordinal); Assert.Contains("/Profile", html, StringComparison.Ordinal); }
    }

    [Fact]
    public async Task AjaxHeaderAcceptsTokenAndFeedbackAppearsOnce()
    {
        await SignInAsync(AdminEmail, Password);
        var page = await client.GetStringAsync("/EventType/Index", TestContext.Current.CancellationToken);
        var token = Regex.Match(page, "<meta name=\"RequestVerificationToken\" content=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/EventType/ToggleActive?id=0");
        invalid.Headers.Add("RequestVerificationToken", "invalid");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalid, TestContext.Current.CancellationToken)).StatusCode);
        using var valid = new HttpRequestMessage(HttpMethod.Post, "/EventType/ToggleActive?id=0");
        valid.Headers.Add("RequestVerificationToken", token);
        Assert.Equal(HttpStatusCode.Redirect, (await client.SendAsync(valid, TestContext.Current.CancellationToken)).StatusCode);
        var result = await client.GetStringAsync("/EventType/Index", TestContext.Current.CancellationToken);
        Assert.Contains("Event type not found.", result, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", result, StringComparison.Ordinal);
        var next = await client.GetStringAsync("/EventType/Index", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Event type not found.", next, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulMutationShowsEncodedDataAndSuccessOnce()
    {
        await SignInAsync(AdminEmail, Password);
        var token = await GetAntiforgeryTokenAsync("/EventType/Create");
        var name = "<script>" + Guid.NewGuid().ToString("N") + "</script>";
        var response = await client.PostAsync("/EventType/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TypeName"] = name,
            ["Description"] = "Test description",
            ["__RequestVerificationToken"] = token
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await client.GetStringAsync("/EventType/Index", TestContext.Current.CancellationToken);
        Assert.Contains("Event type added successfully.", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain(name, html, StringComparison.Ordinal);
        var next = await client.GetStringAsync("/EventType/Index", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Event type added successfully.", next, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("success", "notice-success", "status")]
    [InlineData("error", "notice-error", "alert")]
    public async Task TempDataFeedbackIsEncodedInTheRenderedLayout(string scenario, string noticeClass, string role)
    {
        await using var feedbackFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<MvcOptions>(options => options.Filters.Add(new FeedbackTestFilter()))));
        using var feedbackClient = feedbackFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.Add("X-Feedback-Test", scenario);

        using var response = await feedbackClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains($"class=\"notice {noticeClass}\" role=\"{role}\"", html, StringComparison.Ordinal);
        Assert.Contains(HtmlEncoder.Default.Encode(FeedbackTestFilter.Payload), html, StringComparison.Ordinal);
        Assert.DoesNotContain(FeedbackTestFilter.Payload, html, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(html, "class=\"notice ").Cast<Match>());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("whitespace")]
    public async Task EmptyTempDataDoesNotRenderANotice(string scenario)
    {
        await using var feedbackFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<MvcOptions>(options => options.Filters.Add(new FeedbackTestFilter()))));
        using var feedbackClient = feedbackFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.Add("X-Feedback-Test", scenario);

        using var response = await feedbackClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("class=\"notice ", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/Account/Login", "/Account/Login", false, "Email", "Password")]
    [InlineData("/EventType/Create", "/EventType/Create", true, "TypeName", "Description")]
    [InlineData("/Venue/Create", "/Venue/Create", true, "RoomName", "Capacity")]
    [InlineData("/Home/Index", "/Member/Edit", true, "StudentNumber", "Course")]
    public async Task InvalidPostAssociatesFieldErrorsWithControls(string tokenPath, string postPath, bool signIn, string first, string second)
    {
        if (signIn) await SignInAsync(AdminEmail, Password);
        var token = await GetAntiforgeryTokenAsync(tokenPath);
        using var response = await client.PostAsync(postPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Capacity"] = "not-a-number"
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        AssertErrorAssociation(html, first);
        AssertErrorAssociation(html, second);
    }

    [Theory]
    [InlineData("/Member", "No members yet. Members are created when an account request is approved.")]
    [InlineData("/Member?search=%20%20", "No members yet. Members are created when an account request is approved.")]
    [InlineData("/Member?search=unmatched", "No members match your search.")]
    public async Task EmptyMemberDirectoryExplainsSearchState(string path, string message)
    {
        await SignInAsync(AdminEmail, Password);
        var html = await client.GetStringAsync(path, TestContext.Current.CancellationToken);
        Assert.Contains(message, html, StringComparison.Ordinal);
        Assert.Contains("data-table", html, StringComparison.Ordinal);
    }

    private static void AssertErrorAssociation(string html, string field)
    {
        var input = Regex.Match(html, $"<(?:input|select|textarea)\\b[^>]*\\bname=\"{field}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"No control for {field}.");
        Assert.Contains($"aria-describedby=\"{field}-validation\"", input.Value, StringComparison.Ordinal);
        var error = Regex.Match(html, $"<span[^>]*id=\"{field}-validation\"[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(error.Success, $"No error container for {field}.");
        Assert.False(string.IsNullOrWhiteSpace(Regex.Replace(error.Groups[1].Value, "<[^>]+>", "")), $"No server error for {field}.");
    }

    // Test-only filter supplies otherwise-unreachable values to the real layout without a production endpoint.
    private sealed class FeedbackTestFilter : IAsyncResultFilter
    {
        public const string Payload = "<script>alert(\"feedback\")</script>";

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Controller is Controller controller)
            {
                switch (context.HttpContext.Request.Headers["X-Feedback-Test"].ToString())
                {
                    case "success": controller.TempData["SuccessMessage"] = Payload; break;
                    case "error": controller.TempData["ErrorMessage"] = Payload; break;
                    case "empty": controller.TempData["SuccessMessage"] = ""; controller.TempData["ErrorMessage"] = ""; break;
                    case "whitespace": controller.TempData["SuccessMessage"] = "  "; controller.TempData["ErrorMessage"] = "  "; break;
                }
            }
            await next();
        }
    }

    private async Task<HttpResponseMessage> SignInAsync(string email, string password)
    {
        return await SignInAsync(client, email, password);
    }

    private async Task<HttpResponseMessage> SignInAsync(HttpClient targetClient, string email, string password)
    {
        var token = await GetAntiforgeryTokenAsync(targetClient, "/Account/Login");
        return await targetClient.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["__RequestVerificationToken"] = token
            }),
            TestContext.Current.CancellationToken);
    }

    private async Task<string> GetAntiforgeryTokenAsync(string path)
    {
        return await GetAntiforgeryTokenAsync(client, path);
    }

    private async Task<string> GetAntiforgeryTokenAsync(HttpClient targetClient, string path)
    {
        var response = await targetClient.GetAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"No antiforgery token was rendered on {path}.");
        return match.Groups[1].Value;
    }

    private async Task<int> GetMemberIdAsync(string email)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);
        var userId = await context.Users
            .Where(user => user.UserId == email)
            .Select(user => user.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        return await context.Members
            .Where(member => member.UserId == userId)
            .Select(member => member.MemberId)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task EnsureMemberProfilesAsync()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);
        var profileSeeds = new[]
        {
            (Email: MemberEmail, StudentNumber: "FLOW-IT-1001", Course: "Computer Science", YearLevel: 2, Section: "CS-A", Mobile: "555-0101", MembershipType: "Regular"),
            (Email: SecondMemberEmail, StudentNumber: "FLOW-IT-2002", Course: "Information Systems", YearLevel: 3, Section: "IS-B", Mobile: "555-0202", MembershipType: "Associate")
        };

        foreach (var profile in profileSeeds)
        {
            var user = await context.Users.SingleAsync(item => item.UserId == profile.Email, TestContext.Current.CancellationToken);
            if (!await context.Members.AnyAsync(item => item.UserId == user.Id, TestContext.Current.CancellationToken))
            {
                context.Members.Add(new Member
                {
                    UserId = user.Id,
                    StudentNumber = profile.StudentNumber,
                    Course = profile.Course,
                    YearLevel = profile.YearLevel,
                    Section = profile.Section,
                    MobileNumber = profile.Mobile,
                    MembershipType = profile.MembershipType,
                    Status = "active",
                    CreatedBy = user.Id
                });
            }
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task RemoveMemberProfilesAsync()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);
        var studentNumbers = new[] { "FLOW-IT-1001", "FLOW-IT-2002" };
        var profiles = await context.Members
            .Where(member => studentNumbers.Contains(member.StudentNumber))
            .ToListAsync(TestContext.Current.CancellationToken);
        context.Members.RemoveRange(profiles);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertReadOnlyProfile(string html)
    {
        Assert.Contains("<dl class=\"profile-details\">", html, StringComparison.Ordinal);
        var sections = Regex.Matches(html, "<section class=\"panel\".*?</section>", RegexOptions.Singleline);
        Assert.True(sections.Count > 0, "Profile details section was not rendered.");
        foreach (Match section in sections)
        {
            var profileMarkup = section.Value;
            Assert.False(Regex.IsMatch(profileMarkup, "<(input|select|textarea)\\b", RegexOptions.IgnoreCase));
            Assert.DoesNotContain("Save", profileMarkup, StringComparison.Ordinal);
            Assert.DoesNotContain("Edit profile", profileMarkup, StringComparison.Ordinal);
        }
    }

    private static string? GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        if (location is null)
        {
            return null;
        }

        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }

    private static string GetSetCookieHeader(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("; ", values)
            : string.Empty;
    }

    private static string ExtractGenericFailure(string html)
    {
        var match = Regex.Match(html, "Incorrect email or password\\.");
        return match.Success ? match.Value : string.Empty;
    }

    private async Task SeedUsersAsync()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);

        var existingEmails = await context.Users
            .Select(user => user.UserId)
            .ToListAsync(TestContext.Current.CancellationToken);

        var users = new[]
        {
            CreateUser(AdminEmail, "Flow Admin", RoleCode.Admin, isActive: true),
            CreateUser(OfficerEmail, "Flow Officer", RoleCode.Officer, isActive: true),
            CreateUser(MemberEmail, "Flow Member", RoleCode.Member, isActive: true),
            CreateUser(SecondMemberEmail, "Flow Second Member", RoleCode.Member, isActive: true),
            CreateUser(MissingProfileEmail, "Flow Member Without Profile", RoleCode.Member, isActive: true),
            CreateUser(InactiveEmail, "Flow Inactive", RoleCode.Admin, isActive: false)
        };

        context.Users.AddRange(users.Where(user => !existingEmails.Contains(user.UserId)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private User CreateUser(string email, string name, RoleCode role, bool isActive)
    {
        return new User
        {
            UserId = email,
            Name = name,
            Password = hasher.Hash(Password),
            RoleId = role,
            IsActive = isActive
        };
    }

    private async Task SetActiveAsync(string email, bool isActive)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);
        var user = await context.Users.SingleAsync(item => item.UserId == email, TestContext.Current.CancellationToken);
        user.IsActive = isActive;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SetRoleAsync(string email, RoleCode role)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;
        await using var context = new AsiBasecodeDBContext(options);
        var user = await context.Users.SingleAsync(item => item.UserId == email, TestContext.Current.CancellationToken);
        user.RoleId = role;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
