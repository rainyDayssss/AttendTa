using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class ActivityLogViewerTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory;
    private HttpClient officerClient;
    private string adminEmail;
    private string officerEmail;
    private string password;

    public ActivityLogViewerTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        password = $"ActivityLog#{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";
        adminEmail = $"audit-admin-{suffix}@attendta.test";
        officerEmail = $"audit-officer-{suffix}@attendta.test";
        await SeedUserAsync(adminEmail, "Activity Log Admin", RoleCode.Admin);
        await SeedUserAsync(officerEmail, "Activity Log Officer", RoleCode.Officer);

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        officerClient?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
    }

    [Fact]
    public async Task ActivityLogIsAdminOnlyReadOnlyAndPagesTiedEntriesNewestFirst()
    {
        var anonymous = await officerClient.GetAsync("/admin/auditlogs", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", GetLocationPath(anonymous), StringComparison.Ordinal);

        await SignInAsync(officerClient, officerEmail);
        var denied = await officerClient.GetAsync("/admin/auditlogs", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(denied), StringComparison.Ordinal);

        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await SignInAsync(adminClient, adminEmail);

        var empty = await adminClient.GetStringAsync("/admin/auditlogs", TestContext.Current.CancellationToken);
        Assert.Contains("No activity has been recorded yet.", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", empty, StringComparison.Ordinal);

        var logIds = await SeedActivityLogsAsync(adminEmail);
        var firstPageResponse = await adminClient.GetAsync("/admin/auditlogs?page=1", TestContext.Current.CancellationToken);
        firstPageResponse.EnsureSuccessStatusCode();
        var firstPage = await firstPageResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var firstPageIds = ExtractLogIds(firstPage);

        Assert.Equal(25, firstPageIds.Length);
        Assert.Equal(logIds.Take(25), firstPageIds);
        Assert.Contains("2026-11-05 20:35 Asia/Manila", firstPage, StringComparison.Ordinal);
        Assert.Contains("2026-11-05 20:30 Asia/Manila", firstPage, StringComparison.Ordinal);
        Assert.Contains("Activity Log Admin", firstPage, StringComparison.Ordinal);
        Assert.Contains("User", firstPage, StringComparison.Ordinal);
        Assert.Contains("System process", firstPage, StringComparison.Ordinal);
        Assert.Contains("System", firstPage, StringComparison.Ordinal);
        Assert.Contains("audit.test.action.26", firstPage, StringComparison.Ordinal);
        Assert.Contains("test_entities", firstPage, StringComparison.Ordinal);
        Assert.Contains("Reason for audit entry 26", firstPage, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 2", firstPage, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Next page\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Edit", firstPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Delete", firstPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("asp-action=\"", firstPage, StringComparison.Ordinal);
        var lastPage = await adminClient.GetStringAsync("/admin/auditlogs?page=2", TestContext.Current.CancellationToken);
        Assert.Equal(logIds.Skip(25), ExtractLogIds(lastPage));
        Assert.Contains("Page 2 of 2", lastPage, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Previous page\"", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", lastPage, StringComparison.Ordinal);

        var outOfRange = await adminClient.GetStringAsync("/admin/auditlogs?page=999999999", TestContext.Current.CancellationToken);
        Assert.Contains("Page 2 of 2", outOfRange, StringComparison.Ordinal);
        Assert.Equal(logIds.Skip(25), ExtractLogIds(outOfRange));

        foreach (var invalidPage in new[] { "not-a-number", "0", "-1", "2147483648" })
        {
            var invalid = await adminClient.GetStringAsync($"/admin/auditlogs?page={invalidPage}", TestContext.Current.CancellationToken);
            Assert.Contains("Page 1 of 2", invalid, StringComparison.Ordinal);
        }
    }

    private async Task SignInAsync(HttpClient targetClient, string email)
    {
        var loginPage = await targetClient.GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);
        var token = Regex.Match(loginPage, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        var response = await targetClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private async Task SeedUserAsync(string email, string name, RoleCode role)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        context.Users.Add(new User
        {
            UserId = email,
            Name = name,
            Password = hasher.Hash(password),
            RoleId = role,
            IsActive = true
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<long>> SeedActivityLogsAsync(string adminEmailForLookup)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        var adminUserId = await context.Users
            .AsNoTracking()
            .Where(user => user.UserId == adminEmailForLookup)
            .Select(user => user.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        var olderOccurredAt = new DateTime(2026, 11, 5, 12, 30, 0, DateTimeKind.Utc);
        var newerOccurredAt = olderOccurredAt.AddMinutes(5);
        var logs = Enumerable.Range(1, 26).Select(index => new ActivityLog
        {
            ActorTypeId = index == 26 ? ActorTypeCode.System : ActorTypeCode.User,
            ActorUserId = index == 26 ? null : adminUserId,
            Action = $"audit.test.action.{index:D2}",
            EntityType = "test_entities",
            EntityId = index,
            Description = $"Activity log integration entry {index}",
            Reason = $"Reason for audit entry {index}",
            OccurredAt = index >= 14 ? newerOccurredAt : olderOccurredAt
        }).ToList();

        context.ActivityLogs.AddRange(logs);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return logs
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.LogId)
            .Select(log => log.LogId)
            .ToList();
    }

    private static long[] ExtractLogIds(string html) => Regex.Matches(html, "data-log-id=\"(\\d+)\"")
        .Cast<Match>()
        .Select(match => long.Parse(match.Groups[1].Value))
        .ToArray();

    private static string GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }
}
