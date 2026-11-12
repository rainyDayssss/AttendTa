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

public sealed class AdminAccountsListTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Password = "AccountsList#2026";
    private const string PasswordHashMarker = "$argon2id$accounts-secret-";
    private const int ListedAccountCount = 26;

    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory;
    private HttpClient client;
    private string adminEmail;
    private string officerEmail;
    private int[] orderedIds = Array.Empty<int>();

    public AdminAccountsListTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        adminEmail = $"accounts-admin-{suffix}@attendta.test";
        officerEmail = $"accounts-officer-{suffix}@attendta.test";
        await SeedAccountsAsync();

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
    public async Task AdministratorSeesPagedSearchableAccountsWithRoleAndStatusBadgesAndNoHashes()
    {
        var anonymous = await client.GetAsync("/admin/accounts", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", GetLocationPath(anonymous), StringComparison.Ordinal);

        using var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await SignInAsync(officerClient, officerEmail);
        var denied = await officerClient.GetAsync("/admin/accounts", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(denied), StringComparison.Ordinal);

        await SignInAsync(client, adminEmail);

        var firstPage = await client.GetStringAsync("/admin/accounts", TestContext.Current.CancellationToken);
        var firstPageIds = ExtractAccountIds(firstPage);
        Assert.Equal(25, firstPageIds.Count);
        Assert.Equal(orderedIds.Take(25), firstPageIds);
        Assert.Contains("Page 1 of 2", firstPage, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Next page\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Previous page\"", firstPage, StringComparison.Ordinal);
        Assert.Contains("Accounts Admin", firstPage, StringComparison.Ordinal);
        Assert.Contains(adminEmail, firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain(PasswordHashMarker, firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("password_hash", firstPage, StringComparison.OrdinalIgnoreCase);

        var adminRow = ExtractRow(firstPage, orderedIds[0]);
        Assert.Contains("class=\"badge badge-admin\">Administrator</span>", adminRow, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-active\">Active</span>", adminRow, StringComparison.Ordinal);

        var officerRow = ExtractRow(firstPage, orderedIds[1]);
        Assert.Contains("class=\"badge badge-officer\">Officer</span>", officerRow, StringComparison.Ordinal);

        var inactiveMemberRow = ExtractRow(firstPage, orderedIds[2]);
        Assert.Contains("class=\"badge badge-member\">Member</span>", inactiveMemberRow, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-inactive\">Inactive</span>", inactiveMemberRow, StringComparison.Ordinal);

        var activeMemberRow = ExtractRow(firstPage, orderedIds[3]);
        Assert.Contains("class=\"badge badge-active\">Active</span>", activeMemberRow, StringComparison.Ordinal);

        var lastPage = await client.GetStringAsync("/admin/accounts?page=999999999", TestContext.Current.CancellationToken);
        var lastPageIds = ExtractAccountIds(lastPage);
        Assert.Equal(orderedIds.Skip(25), lastPageIds);
        Assert.Contains("Page 2 of 2", lastPage, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Previous page\"", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain(PasswordHashMarker, lastPage, StringComparison.Ordinal);

        foreach (var invalidPage in new[] { "not-a-number", "0", "-1", "2147483648" })
        {
            var invalid = await client.GetStringAsync($"/admin/accounts?page={invalidPage}", TestContext.Current.CancellationToken);
            Assert.Equal(25, ExtractAccountIds(invalid).Count);
            Assert.Contains("Page 1 of 2", invalid, StringComparison.Ordinal);
        }

        var nameSearch = await client.GetStringAsync("/admin/accounts?search=Listed", TestContext.Current.CancellationToken);
        Assert.Equal(25, ExtractAccountIds(nameSearch).Count);
        Assert.Contains("Page 1 of 2", nameSearch, StringComparison.Ordinal);
        Assert.Contains("value=\"Listed\"", nameSearch, StringComparison.Ordinal);
        Assert.Contains("search=Listed&amp;page=2", nameSearch, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-account-id=\"{orderedIds[0]}\"", nameSearch, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-account-id=\"{orderedIds[1]}\"", nameSearch, StringComparison.Ordinal);
        Assert.DoesNotContain(PasswordHashMarker, nameSearch, StringComparison.Ordinal);

        var filteredLastPage = await client.GetStringAsync("/admin/accounts?search=Listed&page=2", TestContext.Current.CancellationToken);
        var filteredLastPageIds = ExtractAccountIds(filteredLastPage);
        Assert.Single(filteredLastPageIds);
        Assert.Equal(orderedIds[^1], filteredLastPageIds[0]);
        Assert.Contains("Page 2 of 2", filteredLastPage, StringComparison.Ordinal);
        Assert.Contains("search=Listed&amp;page=1", filteredLastPage, StringComparison.Ordinal);

        var emailSearch = await client.GetStringAsync($"/admin/accounts?search={Uri.EscapeDataString(adminEmail)}", TestContext.Current.CancellationToken);
        var emailSearchIds = ExtractAccountIds(emailSearch);
        Assert.Single(emailSearchIds);
        Assert.Equal(orderedIds[0], emailSearchIds[0]);
        Assert.DoesNotContain("aria-label=\"Pagination\"", emailSearch, StringComparison.Ordinal);

        var empty = await client.GetStringAsync("/admin/accounts?search=unmatched", TestContext.Current.CancellationToken);
        Assert.Contains("No accounts match your search.", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("data-account-id", empty, StringComparison.Ordinal);

        var shell = await client.GetStringAsync("/Home/Index", TestContext.Current.CancellationToken);
        Assert.Contains("/admin/accounts", shell, StringComparison.Ordinal);
    }

    private static List<int> ExtractAccountIds(string html) =>
        Regex.Matches(html, "data-account-id=\"(\\d+)\"")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToList();

    private static string ExtractRow(string html, int accountId)
    {
        var match = Regex.Match(html, $"<tr data-account-id=\"{accountId}\">(.*?)</tr>", RegexOptions.Singleline);
        Assert.True(match.Success, $"No row was rendered for account {accountId}.");
        return match.Value;
    }

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

    private async Task SeedAccountsAsync()
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);

        var users = new List<User>
        {
            new()
            {
                UserId = adminEmail,
                Name = "Accounts Admin",
                Password = hasher.Hash(Password),
                RoleId = RoleCode.Admin,
                IsActive = true
            },
            new()
            {
                UserId = officerEmail,
                Name = "Accounts Officer",
                Password = hasher.Hash(Password),
                RoleId = RoleCode.Officer,
                IsActive = true
            }
        };

        for (var index = 1; index <= ListedAccountCount; index++)
        {
            users.Add(new User
            {
                UserId = $"listed-{index:D2}-{Guid.NewGuid():N}@attendta.test",
                Name = $"Listed Account {index:D2}",
                Password = PasswordHashMarker + index,
                RoleId = RoleCode.Member,
                IsActive = index % 2 == 0
            });
        }

        context.Users.AddRange(users);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        orderedIds = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.Name)
            .ThenBy(user => user.Id)
            .Select(user => user.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private static string GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }
}
