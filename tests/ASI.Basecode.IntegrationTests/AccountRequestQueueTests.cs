using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class AccountRequestQueueTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Password = "QueueTest#2026";
    private const string PasswordHashMarker = "$argon2id$queue-secret-";

    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory;
    private HttpClient client;
    private string adminEmail;
    private string memberEmail;

    public AccountRequestQueueTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        adminEmail = $"queue-admin-{suffix}@attendta.test";
        memberEmail = $"queue-member-{suffix}@attendta.test";
        await SeedUserAsync(adminEmail, RoleCode.Admin);
        await SeedUserAsync(memberEmail, RoleCode.Member);

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
        if (factory is not null) await factory.DisposeAsync();
    }

    [Fact]
    public async Task ManagementQueueFiltersPagesNormalizesAndNeverRendersHashes()
    {
        var anonymous = await client.GetAsync("/AccountRequest", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", GetLocationPath(anonymous), StringComparison.Ordinal);

        await SignInAsync(memberEmail);
        var denied = await client.GetAsync("/AccountRequest", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(denied), StringComparison.Ordinal);

        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await SignInAsync(adminClient, adminEmail);
        var ids = await SeedRequestsAsync();

        var defaultPending = await adminClient.GetStringAsync("/AccountRequest", TestContext.Current.CancellationToken);
        Assert.Contains("Page 1 of 2", defaultPending, StringComparison.Ordinal);
        Assert.Contains("Pending Queue Request", defaultPending, StringComparison.Ordinal);
        Assert.Contains("Reason for declining", defaultPending, StringComparison.Ordinal);
        Assert.Contains("/AccountRequest/Approve", defaultPending, StringComparison.Ordinal);
        Assert.Contains("/AccountRequest/Reject", defaultPending, StringComparison.Ordinal);
        Assert.Contains("__RequestVerificationToken", defaultPending, StringComparison.Ordinal);
        Assert.DoesNotContain("Rejected Queue Request", defaultPending, StringComparison.Ordinal);

        var firstPageResponse = await adminClient.GetAsync("/AccountRequest?status=pending&page=1&keep=alpha&keep=beta", TestContext.Current.CancellationToken);
        firstPageResponse.EnsureSuccessStatusCode();
        var firstPage = await firstPageResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var pageIds = Regex.Matches(firstPage, "data-request-id=\"(\\d+)\"")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();
        Assert.Equal(25, pageIds.Length);
        Assert.Equal(ids.Skip(1).Reverse(), pageIds);
        Assert.Contains($"data-request-id=\"{ids[^1]}\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-request-id=\"{ids[0]}\"", firstPage, StringComparison.Ordinal);
        Assert.Contains("2026-11-05 20:30 Asia/Manila", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain(PasswordHashMarker, firstPage, StringComparison.Ordinal);
        Assert.Contains("status=pending&amp;keep=alpha&amp;keep=beta&amp;page=2", firstPage, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Next page\"", firstPage, StringComparison.Ordinal);
        Assert.Contains("Showing 1–25 of 26 requests.", firstPage, StringComparison.Ordinal);

        var searched = await adminClient.GetStringAsync("/AccountRequest?status=pending&search=Queue%20Request%2001", TestContext.Current.CancellationToken);
        Assert.Contains("Pending Queue Request 01", searched, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending Queue Request 02", searched, StringComparison.Ordinal);
        Assert.Contains("Showing 1–1 of 1 request.", searched, StringComparison.Ordinal);

        var oldestFirst = await adminClient.GetStringAsync("/AccountRequest?status=pending&sort=oldest", TestContext.Current.CancellationToken);
        var oldestIds = Regex.Matches(oldestFirst, "data-request-id=\"(\\d+)\"")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();
        Assert.Equal(ids.Take(25), oldestIds);

        var lastPage = await adminClient.GetStringAsync("/AccountRequest?status=pending&page=999999999&keep=alpha&keep=beta", TestContext.Current.CancellationToken);
        Assert.Contains("Page 2 of 2", lastPage, StringComparison.Ordinal);
        Assert.Contains($"data-request-id=\"{ids[0]}\"", lastPage, StringComparison.Ordinal);
        Assert.Contains("status=pending&amp;keep=alpha&amp;keep=beta&amp;page=1", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", lastPage, StringComparison.Ordinal);

        foreach (var invalidPage in new[] { "not-a-number", "0", "-1", "2147483648" })
        {
            var invalid = await adminClient.GetStringAsync($"/AccountRequest?status=pending&page={invalidPage}", TestContext.Current.CancellationToken);
            Assert.Contains("Page 1 of 2", invalid, StringComparison.Ordinal);
        }

        var rejected = await adminClient.GetStringAsync("/AccountRequest?status=rejected&keep=retained", TestContext.Current.CancellationToken);
        Assert.Contains("Rejected Queue Request", rejected, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending Queue Request", rejected, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", rejected, StringComparison.Ordinal);
        Assert.DoesNotContain(PasswordHashMarker, rejected, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog-open", rejected, StringComparison.Ordinal);

        var empty = await adminClient.GetStringAsync("/AccountRequest?status=approved", TestContext.Current.CancellationToken);
        Assert.Contains("No approved account requests found.", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", empty, StringComparison.Ordinal);

        var all = await adminClient.GetStringAsync("/AccountRequest?status=all", TestContext.Current.CancellationToken);
        Assert.Contains("Membership Account Requests", all, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Request status\"", all, StringComparison.Ordinal);
        Assert.Contains("filter-tab-count", all, StringComparison.Ordinal);
        Assert.Contains(">27<", all, StringComparison.Ordinal);
        Assert.Contains("Pending Queue Request", all, StringComparison.Ordinal);
        Assert.Contains("Approve", all, StringComparison.Ordinal);
        Assert.Contains("Decline", all, StringComparison.Ordinal);
        Assert.Contains("reject-dialog", all, StringComparison.Ordinal);

        var allLastPage = await adminClient.GetStringAsync("/AccountRequest?status=all&page=2", TestContext.Current.CancellationToken);
        Assert.Contains("Rejected Queue Request", allLastPage, StringComparison.Ordinal);

        var token = Regex.Match(all, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        var approvedId = ids[^1];
        using var approve = await adminClient.PostAsync(
            "/AccountRequest/Approve?status=all",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = approvedId.ToString(),
                ["__RequestVerificationToken"] = token
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, approve.StatusCode);
        Assert.Equal("/AccountRequest/Index?status=all", approve.Headers.Location?.ToString());

        var afterApproval = await adminClient.GetStringAsync("/AccountRequest?status=all", TestContext.Current.CancellationToken);
        Assert.Contains("Account request approved.", afterApproval, StringComparison.Ordinal);
    }

    private async Task SignInAsync(string email) => await SignInAsync(client, email);

    private async Task SignInAsync(HttpClient targetClient, string email)
    {
        var loginPage = await targetClient.GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);
        var token = Regex.Match(loginPage, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        var response = await targetClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private async Task SeedUserAsync(string email, RoleCode role)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        context.Users.Add(new User
        {
            UserId = email,
            Name = "Account Request Queue Test",
            Password = hasher.Hash(Password),
            RoleId = role,
            IsActive = true
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<int>> SeedRequestsAsync()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        var requestedAt = new DateTime(2026, 11, 5, 12, 30, 0, DateTimeKind.Utc);
        var requests = Enumerable.Range(1, 26).Select(index => new AccountRequest
        {
            StudentNumber = $"QUEUE-{Guid.NewGuid():N}",
            FullName = $"Pending Queue Request {index:D2}",
            Email = $"pending-{Guid.NewGuid():N}@attendta.test",
            PasswordHash = PasswordHashMarker + index,
            Course = "Computing",
            YearLevel = 2,
            Section = "A",
            MobileNumber = "09170000000",
            AccountRequestStatusId = AccountRequestStatusCode.Pending,
            RequestedAt = requestedAt
        }).ToList();
        requests.Add(new AccountRequest
        {
            StudentNumber = $"QUEUE-{Guid.NewGuid():N}",
            FullName = "Rejected Queue Request",
            Email = $"rejected-{Guid.NewGuid():N}@attendta.test",
            PasswordHash = null!,
            Course = "Computing",
            YearLevel = 2,
            Section = "A",
            MobileNumber = "09170000000",
            AccountRequestStatusId = AccountRequestStatusCode.Rejected,
            RequestedAt = requestedAt.AddDays(-1)
        });

        context.AccountRequests.AddRange(requests);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return requests.Take(26).Select(request => request.RequestId).ToList();
    }

    private static string GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }
}
