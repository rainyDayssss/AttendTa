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
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Role assignment against SQL Server: the last-administrator app lock, the audit row, the
/// console badge, and the real MVC endpoints with antiforgery and policies.
/// </summary>
public sealed class AdminRoleAssignmentTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Password = "RoleAssignment#2026";

    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    public AdminRoleAssignmentTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public ValueTask InitializeAsync()
    {
        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        return ValueTask.CompletedTask;
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
    public async Task AdministratorRoleChangeWritesAuditAndShowsTheNewBadge()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "role-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "role-target", ct);

        await SignInAsync(client, actor.Email, ct);

        var form = await client.GetStringAsync($"/admin/accounts/{target.Id}/role", ct);
        Assert.Contains("Change role", form, StringComparison.Ordinal);
        Assert.Contains(target.Email, form, StringComparison.Ordinal);
        Assert.Contains(">Select a role</option>", form, StringComparison.Ordinal);
        Assert.Contains("badge badge-member\">Member</span>", form, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"member\"", form, StringComparison.Ordinal);
        Assert.Contains("value=\"officer\"", form, StringComparison.Ordinal);
        Assert.Contains("value=\"admin\"", form, StringComparison.Ordinal);

        var token = ExtractAntiforgeryToken(form);
        using var response = await client.PostAsync(
            $"/admin/accounts/{target.Id}/role",
            RoleForm("officer", "  Promoted to officer.  ", token),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/accounts", GetLocationPath(response));

        await using (var observer = Context())
        {
            var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
            Assert.Equal(RoleCode.Officer, user.RoleId);
            Assert.Equal(actor.Id, user.UpdatedById);
            Assert.NotNull(user.UpdatedTime);

            var audit = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == target.Id, ct);
            Assert.Equal("users", audit.EntityType);
            Assert.Equal(actor.Id, audit.ActorUserId);
            Assert.Equal(ActorTypeCode.User, audit.ActorTypeId);
            Assert.Equal("Promoted to officer.", audit.Reason);
            Assert.Contains("\"RoleCode\":\"member\"", audit.OldValues ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("\"RoleCode\":\"officer\"", audit.NewValues ?? string.Empty, StringComparison.Ordinal);
            Assert.Equal(1, await observer.ActivityLogs.CountAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == target.Id, ct));
        }

        var list = await client.GetStringAsync($"/admin/accounts?search={Uri.EscapeDataString(target.Email)}", ct);
        Assert.Contains("class=\"badge badge-officer\">Officer</span>", list, StringComparison.Ordinal);
        Assert.Contains($"/admin/accounts/{target.Id}/role", list, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParallelDemotionsCommitExactlyOneAndKeepOneActiveAdministrator()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var first = await SeedUserAsync(RoleCode.Admin, "parallel-admin-a", ct);
        var second = await SeedUserAsync(RoleCode.Admin, "parallel-admin-b", ct);

        async Task<(OutcomeCode? Outcome, string? Message)> DemoteAsync(int actorId, int targetId)
        {
            await using var context = Context();
            var service = CreateService(context, new TestActor(actorId, RoleCodes.Admin));
            try
            {
                await service.ChangeRoleAsync(new ChangeAccountRoleViewModel
                {
                    AccountId = targetId,
                    RoleCode = RoleCodes.Member,
                    Reason = "Concurrent demotion."
                }, ct);
                return (null, null);
            }
            catch (DomainException exception)
            {
                return (exception.Outcome, exception.Message);
            }
        }

        var outcomes = await Task.WhenAll(
            DemoteAsync(first.Id, second.Id),
            DemoteAsync(second.Id, first.Id));

        Assert.Single(outcomes, outcome => outcome.Outcome is null);
        var loser = Assert.Single(outcomes, outcome => outcome.Outcome == OutcomeCode.Stale);
        Assert.Contains("last active Administrator", loser.Message ?? string.Empty, StringComparison.Ordinal);

        await using var observer = Context();
        Assert.Equal(1, await observer.Users.CountAsync(item => item.IsActive && item.RoleId == RoleCode.Admin, ct));
        Assert.Equal(1, await observer.Users.CountAsync(item => (item.Id == first.Id || item.Id == second.Id) && item.RoleId == RoleCode.Admin, ct));
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(
            item => item.Action == AuditActions.UserRoleAssigned && (item.EntityId == first.Id || item.EntityId == second.Id), ct));
    }

    [Fact]
    public async Task LastActiveAdministratorCannotBeDemoted()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "solo-admin", ct);

        await SignInAsync(client, actor.Email, ct);
        var token = ExtractAntiforgeryToken(await client.GetStringAsync($"/admin/accounts/{actor.Id}/role", ct));

        using var response = await client.PostAsync(
            $"/admin/accounts/{actor.Id}/role",
            RoleForm("member", "Stepping down.", token),
            ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/accounts", GetLocationPath(response));

        var list = await client.GetStringAsync("/admin/accounts", ct);
        Assert.Contains("The last active Administrator cannot be demoted.", list, StringComparison.Ordinal);

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == actor.Id, ct);
        Assert.Equal(RoleCode.Admin, user.RoleId);
        Assert.Null(user.UpdatedById);
        Assert.Null(user.UpdatedTime);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == actor.Id, ct));
    }

    [Fact]
    public async Task OfficerAndAnonymousCallersCannotChangeRoles()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var officer = await SeedUserAsync(RoleCode.Officer, "role-officer", ct);
        var target = await SeedUserAsync(RoleCode.Member, "role-forbidden-target", ct);

        using (var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        }))
        {
            await SignInAsync(officerClient, officer.Email, ct);
            var token = ExtractAntiforgeryToken(await officerClient.GetStringAsync("/Home/Index", ct));
            using var response = await officerClient.PostAsync(
                $"/admin/accounts/{target.Id}/role",
                RoleForm("admin", "Escalation attempt.", token),
                ct);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/AccessDenied", GetLocationPath(response), StringComparison.Ordinal);
        }

        using (var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        }))
        {
            var loginPage = await anonymousClient.GetStringAsync("/Account/Login", ct);
            var token = ExtractAntiforgeryToken(loginPage);
            using var response = await anonymousClient.PostAsync(
                $"/admin/accounts/{target.Id}/role",
                RoleForm("admin", "Anonymous escalation attempt.", token),
                ct);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Account/Login", GetLocationPath(response), StringComparison.Ordinal);
        }

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
        Assert.Equal(RoleCode.Member, user.RoleId);
        Assert.Null(user.UpdatedById);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == target.Id, ct));
    }

    [Fact]
    public async Task BlankAndOverlongReasonsRedisplayTheFormWithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "validation-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "validation-target", ct);

        await SignInAsync(client, actor.Email, ct);
        var token = ExtractAntiforgeryToken(await client.GetStringAsync($"/admin/accounts/{target.Id}/role", ct));

        using (var blank = await client.PostAsync($"/admin/accounts/{target.Id}/role", RoleForm("officer", "   ", token), ct))
        {
            Assert.Equal(HttpStatusCode.OK, blank.StatusCode);
            var html = await blank.Content.ReadAsStringAsync(ct);
            Assert.Contains("A reason is required.", html, StringComparison.Ordinal);
            Assert.Contains("Change role", html, StringComparison.Ordinal);
        }

        using (var overlong = await client.PostAsync($"/admin/accounts/{target.Id}/role", RoleForm("officer", new string('x', 1001), token), ct))
        {
            Assert.Equal(HttpStatusCode.OK, overlong.StatusCode);
            var html = await overlong.Content.ReadAsStringAsync(ct);
            Assert.Contains("The reason must be 1000 characters or fewer.", html, StringComparison.Ordinal);
        }

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
        Assert.Equal(RoleCode.Member, user.RoleId);
        Assert.Null(user.UpdatedById);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == target.Id, ct));
    }

    [Fact]
    public async Task ReasonLimitAppliesToTheTrimmedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "trimmed-reason-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "trimmed-reason-target", ct);

        await SignInAsync(client, actor.Email, ct);
        var token = ExtractAntiforgeryToken(await client.GetStringAsync($"/admin/accounts/{target.Id}/role", ct));

        var trimmedReason = new string('x', 1000);
        using var response = await client.PostAsync(
            $"/admin/accounts/{target.Id}/role",
            RoleForm("officer", $"  {trimmedReason}  ", token),
            ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/accounts", GetLocationPath(response));

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
        Assert.Equal(RoleCode.Officer, user.RoleId);
        var audit = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == target.Id, ct);
        Assert.Equal(trimmedReason, audit.Reason);
    }

    [Fact]
    public async Task RoleChangePostRequiresAntiforgeryAndUnknownAccountsRedirect()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "token-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "token-target", ct);

        await SignInAsync(client, actor.Email, ct);

        using (var noToken = await client.PostAsync(
                   $"/admin/accounts/{target.Id}/role",
                   new FormUrlEncodedContent(new Dictionary<string, string>
                   {
                       ["RoleCode"] = "officer",
                       ["Reason"] = "No token."
                   }),
                   ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        }

        using (var missing = await client.GetAsync("/admin/accounts/2147483647/role", ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, missing.StatusCode);
            Assert.Equal("/admin/accounts", GetLocationPath(missing));
        }

        var list = await client.GetStringAsync("/admin/accounts", ct);
        Assert.Contains("Account not found.", list, StringComparison.Ordinal);

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
        Assert.Equal(RoleCode.Member, user.RoleId);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == target.Id, ct));
    }

    [Fact]
    public async Task SameRoleSubmissionIsRejectedWithoutAnAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "noop-actor", ct);
        var target = await SeedUserAsync(RoleCode.Officer, "noop-target", ct);

        await using (var context = Context())
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                CreateService(context, new TestActor(actor.Id, RoleCodes.Admin)).ChangeRoleAsync(new ChangeAccountRoleViewModel
                {
                    AccountId = target.Id,
                    RoleCode = RoleCodes.Officer,
                    Reason = "Already an officer."
                }, ct));
            Assert.Equal(OutcomeCode.Invalid, error.Outcome);
            Assert.Equal(nameof(ChangeAccountRoleViewModel.RoleCode), error.Field);
        }

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
        Assert.Equal(RoleCode.Officer, user.RoleId);
        Assert.Null(user.UpdatedById);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == target.Id, ct));
    }

    [Fact]
    public async Task StaleSameRolePostIsRefusedAndRetainsTheReason()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "stale-actor", ct);
        var target = await SeedUserAsync(RoleCode.Member, "stale-target", ct);

        await SignInAsync(client, actor.Email, ct);
        var token = ExtractAntiforgeryToken(await client.GetStringAsync($"/admin/accounts/{target.Id}/role", ct));

        // The account becomes an Officer after the form loaded; the crafted submission still
        // asks for Officer, which is now the current role.
        await using (var context = Context())
        {
            await context.Users
                .Where(user => user.Id == target.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.RoleId, RoleCode.Officer), ct);
        }

        using var response = await client.PostAsync(
            $"/admin/accounts/{target.Id}/role",
            RoleForm("officer", "Stale form submission.", token),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("This account already has that role. Choose a different role.", html, StringComparison.Ordinal);
        Assert.Contains("Stale form submission.", html, StringComparison.Ordinal);
        Assert.Contains(">Select a role</option>", html, StringComparison.Ordinal);
        Assert.Contains("badge badge-officer\">Officer</span>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"officer\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"member\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"admin\"", html, StringComparison.Ordinal);
        var roleSelect = Regex.Match(html, "<select[^>]*id=\"RoleCode\"[^>]*>.*?</select>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(roleSelect);
        Assert.DoesNotContain("selected=\"selected\"", roleSelect, StringComparison.Ordinal);

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == target.Id, ct);
        Assert.Equal(RoleCode.Officer, user.RoleId);
        Assert.Null(user.UpdatedById);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.Action == AuditActions.UserRoleAssigned && item.EntityId == target.Id, ct));
    }

    private AccountRoleService CreateService(AsiBasecodeDBContext context, ICurrentActor actor)
    {
        var work = new UnitOfWork(context);
        return new AccountRoleService(
            new AccountAdministrationRepository(work),
            new UserRepository(work),
            work,
            TimeProvider.System,
            actor,
            new ActivityLogWriter(context, actor, TimeProvider.System));
    }

    [Fact]
    public async Task RoleModalRetainsValidationAndSavesThroughTheExistingAuditedCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        await ResetAdministratorsAsync(ct);
        var actor = await SeedUserAsync(RoleCode.Admin, "modal-admin", ct);
        var target = await SeedUserAsync(RoleCode.Member, "modal-target", ct);
        await SignInAsync(client, actor.Email, ct);

        async Task<HttpResponseMessage> ModalAsync(string path, HttpContent? content = null)
        {
            using var request = new HttpRequestMessage(content is null ? HttpMethod.Get : HttpMethod.Post, path) { Content = content };
            request.Headers.Add("X-Record-Dialog", "true");
            return await client.SendAsync(request, ct);
        }

        using var get = await ModalAsync($"/admin/accounts/{target.Id}/role");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var form = await get.Content.ReadAsStringAsync(ct);
        Assert.Contains("data-record-form", form);
        Assert.Contains(target.Email, form);
        Assert.DoesNotContain("<html", form, StringComparison.OrdinalIgnoreCase);
        var token = ExtractAntiforgeryToken(form);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var posting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.RouteAsync("**/*", async route =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(route.Request.Method), route.Request.Url);
            if (route.Request.Headers.TryGetValue("x-record-dialog", out var dialogHeader))
                request.Headers.Add("X-Record-Dialog", dialogHeader);
            if (route.Request.PostDataBuffer is byte[] body)
            {
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", route.Request.Headers["content-type"]);
                posting.TrySetResult();
                await releasePost.Task.WaitAsync(ct);
            }
            using var response = await client.SendAsync(request, ct);
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(header => header.Key is not ("Transfer-Encoding" or "Content-Length" or "Content-Encoding"))
                .ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
            await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = await response.Content.ReadAsByteArrayAsync(ct) });
        });
        var actorTrigger = page.Locator($"[data-record-open='change-role-{actor.Id}']");
        foreach (var width in new[] { 1440, 390 })
        {
            await page.SetViewportSizeAsync(width, 900);
            await page.GotoAsync($"http://localhost/admin/accounts?search={Uri.EscapeDataString(actor.Email)}&fit=2");
            await actorTrigger.ClickAsync();
            await page.Locator("#record-dialog #RoleCode").WaitForAsync();
            Assert.True(await page.Locator("#record-dialog #RoleCode").EvaluateAsync<bool>("field => field === document.activeElement"));
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            Assert.True(await page.Locator("#record-dialog").EvaluateAsync<bool>("dialog => dialog.scrollWidth <= dialog.clientWidth"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), $"attendta-role-modal-{width}.png"), FullPage = true });
            await page.Keyboard.PressAsync("Escape");
            Assert.True(await actorTrigger.EvaluateAsync<bool>("link => link === document.activeElement"));
        }
        await actorTrigger.ClickAsync();
        await page.Locator("#record-dialog #Reason").FillAsync("Retained during a failed save.");
        await page.Locator("#record-dialog #RoleCode").SelectOptionAsync("member");
        await page.Locator("#record-dialog button[type='submit']").ClickAsync();
        try
        {
            await posting.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.True(await page.Locator("#record-dialog #RoleCode").IsDisabledAsync());
            Assert.True(await page.Locator("#record-dialog #Reason").IsDisabledAsync());
        }
        finally { releasePost.TrySetResult(); }
        await page.Locator("#record-dialog .validation-summary-errors").WaitForAsync();
        Assert.False(await page.Locator("#record-dialog #Reason").IsDisabledAsync());
        Assert.Equal("Retained during a failed save.", await page.Locator("#record-dialog #Reason").InputValueAsync());
        await page.CloseAsync();

        using var invalid = await ModalAsync($"/admin/accounts/{target.Id}/role", RoleForm("officer", "   ", token));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var errors = await invalid.Content.ReadAsStringAsync(ct);
        Assert.Contains("data-record-form", errors);
        Assert.Contains("validation-summary-errors", errors);
        Assert.Matches("<option[^>]*selected=\"selected\"[^>]*value=\"officer\"|<option[^>]*value=\"officer\"[^>]*selected=\"selected\"", errors);

        using var protectedAdmin = await ModalAsync($"/admin/accounts/{actor.Id}/role", RoleForm("member", "Retain this reason.", token));
        Assert.Equal(HttpStatusCode.OK, protectedAdmin.StatusCode);
        var guard = await protectedAdmin.Content.ReadAsStringAsync(ct);
        Assert.Contains("validation-summary-errors", guard);
        Assert.Contains("Retain this reason.", guard);
        Assert.Contains("data-record-form", guard);

        using var saved = await ModalAsync($"/admin/accounts/{target.Id}/role", RoleForm("officer", " Approved officer role. ", token));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("{\"saved\":true}", await saved.Content.ReadAsStringAsync(ct));
        await using var observer = Context();
        Assert.Equal(RoleCode.Officer, (await observer.Users.SingleAsync(user => user.Id == target.Id, ct)).RoleId);
        Assert.Equal(RoleCode.Admin, (await observer.Users.SingleAsync(user => user.Id == actor.Id, ct)).RoleId);
        var audit = await observer.ActivityLogs.SingleAsync(log => log.Action == AuditActions.UserRoleAssigned && log.EntityId == target.Id, ct);
        Assert.Equal("Approved officer role.", audit.Reason);
        Assert.Equal(actor.Id, audit.ActorUserId);
    }

    private AsiBasecodeDBContext Context() =>
        new(new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    /// <summary>Keeps the active-Administrator count deterministic for every test in this class.</summary>
    private async Task ResetAdministratorsAsync(CancellationToken cancellationToken)
    {
        await using var context = Context();
        await context.Users
            .Where(user => user.RoleId == RoleCode.Admin)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.RoleId, RoleCode.Member), cancellationToken);
    }

    private async Task<UserSeed> SeedUserAsync(RoleCode role, string prefix, CancellationToken cancellationToken)
    {
        await using var context = Context();
        var email = $"{prefix}-{Guid.NewGuid():N}@attendta.test";
        var user = new User
        {
            Email = email,
            Name = $"Role {prefix}",
            Password = hasher.Hash(Password),
            RoleId = role,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return new UserSeed(user.Id, email);
    }

    private async Task SignInAsync(HttpClient targetClient, string email, CancellationToken cancellationToken)
    {
        var loginPage = await targetClient.GetStringAsync("/Account/Login", cancellationToken);
        var token = ExtractAntiforgeryToken(loginPage);
        using var response = await targetClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static FormUrlEncodedContent RoleForm(string roleCode, string reason, string token) =>
        new(new Dictionary<string, string>
        {
            ["RoleCode"] = roleCode,
            ["Reason"] = reason,
            ["__RequestVerificationToken"] = token
        });

    private static string ExtractAntiforgeryToken(string html)
    {
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private static string GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }

    private sealed record UserSeed(int Id, string Email);

    private sealed class TestActor(int id, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => id;
        public string Role => role;
    }
}
