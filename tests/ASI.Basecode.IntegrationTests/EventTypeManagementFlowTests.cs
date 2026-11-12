using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventTypeManagementFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task ManagementPagesTheListMapsFieldErrorsAndWritesTheToggleAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"event-type-officer-{suffix}@attendta.test";
        var memberEmail = $"event-type-member-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var officerId = await SeedUsersAsync(officerEmail, memberEmail, password, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var anonymous = await officerClient.GetAsync("/EventType", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", LocationPath(anonymous), StringComparison.Ordinal);

        await SignInAsync(officerClient, officerEmail, password, ct);

        var emptyResponse = await officerClient.GetAsync("/EventType", ct);
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        var empty = await emptyResponse.Content.ReadAsStringAsync(ct);
        Assert.Contains("No event types yet. Use \"New Event Type\" to create the first one.", empty, StringComparison.Ordinal);
        Assert.Contains("data-table", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", empty, StringComparison.Ordinal);

        await SeedEventTypesAsync(officerId, ct);

        var firstPageResponse = await officerClient.GetAsync("/EventType", ct);
        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        var firstPage = await firstPageResponse.Content.ReadAsStringAsync(ct);
        Assert.Contains("Page 1 of 2", firstPage, StringComparison.Ordinal);
        Assert.Equal(25, Regex.Matches(firstPage, "data-type-id=\"(\\d+)\"").Count);
        Assert.Contains("Flow Type 01", firstPage, StringComparison.Ordinal);
        Assert.Contains("Flow Type 25", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Flow Type 26", firstPage, StringComparison.Ordinal);
        Assert.Contains("data-table", firstPage, StringComparison.Ordinal);
        Assert.Contains("class=\"badge badge-active\">Active</span>", firstPage, StringComparison.Ordinal);
        Assert.Contains("page=2", firstPage, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Next page\"", firstPage, StringComparison.Ordinal);

        var lastPage = await officerClient.GetStringAsync("/EventType?page=2", ct);
        Assert.Contains("Page 2 of 2", lastPage, StringComparison.Ordinal);
        Assert.Contains("name=\"page\" value=\"2\"", lastPage, StringComparison.Ordinal);
        Assert.Contains("Flow Type 26", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Flow Type 01", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", lastPage, StringComparison.Ordinal);

        var pastEnd = await officerClient.GetStringAsync("/EventType?page=999", ct);
        Assert.Contains("Page 2 of 2", pastEnd, StringComparison.Ordinal);
        Assert.Contains("Flow Type 26", pastEnd, StringComparison.Ordinal);

        var zero = await officerClient.GetStringAsync("/EventType?page=0", ct);
        Assert.Contains("Page 1 of 2", zero, StringComparison.Ordinal);

        var duplicateToken = await AntiforgeryTokenAsync(officerClient, "/EventType/Create", ct);
        var duplicateResponse = await officerClient.PostAsync("/EventType/Create", Form(new Dictionary<string, string>
        {
            ["TypeName"] = "Flow Type 01",
            ["Description"] = "Duplicate attempt",
            ["__RequestVerificationToken"] = duplicateToken
        }), ct);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Contains("Event type name already exists.", await duplicateResponse.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var blankToken = await AntiforgeryTokenAsync(officerClient, "/EventType/Create", ct);
        var blankResponse = await officerClient.PostAsync("/EventType/Create", Form(new Dictionary<string, string>
        {
            ["TypeName"] = $"Blank Description {suffix}",
            ["Description"] = "   ",
            ["__RequestVerificationToken"] = blankToken
        }), ct);
        Assert.Equal(HttpStatusCode.OK, blankResponse.StatusCode);
        Assert.Contains("Description is required.", await blankResponse.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);

        var createdName = $"Flow Created {suffix}";
        var createToken = await AntiforgeryTokenAsync(officerClient, "/EventType/Create", ct);
        var createResponse = await officerClient.PostAsync("/EventType/Create", Form(new Dictionary<string, string>
        {
            ["TypeName"] = $"  {createdName}  ",
            ["Description"] = "  Created from the flow test  ",
            ["__RequestVerificationToken"] = createToken
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int createdId;
        await using (var context = Context())
        {
            var created = await context.EventTypes.AsNoTracking().SingleAsync(t => t.TypeName == createdName, ct);
            Assert.Equal("Created from the flow test", created.Description);
            Assert.Equal(officerId, created.CreatedBy);
            createdId = created.TypeId;
            var createLog = await context.ActivityLogs.AsNoTracking().SingleAsync(
                a => a.EntityType == "event_types" && a.EntityId == createdId && a.Action == AuditActions.EventTypeCreated, ct);
            Assert.Equal(officerId, createLog.ActorUserId);
            Assert.Contains($"\"TypeName\":\"{createdName}\"", createLog.NewValues, StringComparison.Ordinal);
        }

        var toggleToken = await AntiforgeryTokenAsync(officerClient, "/EventType", ct);
        var toggleResponse = await officerClient.PostAsync($"/EventType/ToggleActive?id={createdId}", Form(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = toggleToken
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, toggleResponse.StatusCode);

        await using (var context = Context())
        {
            var toggled = await context.EventTypes.AsNoTracking().SingleAsync(t => t.TypeId == createdId, ct);
            Assert.False(toggled.IsActive);
            var statusLog = await context.ActivityLogs.AsNoTracking().SingleAsync(
                a => a.EntityType == "event_types" && a.EntityId == createdId && a.Action == AuditActions.EventTypeStatusChanged, ct);
            Assert.Equal(officerId, statusLog.ActorUserId);
            Assert.Contains("\"IsActive\":true", statusLog.OldValues, StringComparison.Ordinal);
            Assert.Contains("\"IsActive\":false", statusLog.NewValues, StringComparison.Ordinal);
        }

        var afterToggle = await officerClient.GetStringAsync("/EventType", ct);
        Assert.Contains("class=\"badge badge-inactive\">Inactive</span>", afterToggle, StringComparison.Ordinal);

        var pagedToggleToken = await AntiforgeryTokenAsync(officerClient, "/EventType?page=2", ct);
        var pagedToggleResponse = await officerClient.PostAsync($"/EventType/ToggleActive?id={createdId}&page=2", Form(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = pagedToggleToken
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, pagedToggleResponse.StatusCode);
        Assert.Equal("/EventType?page=2", LocationPath(pagedToggleResponse));

        await SignInAsync(memberClient, memberEmail, password, ct);
        var denied = await memberClient.GetAsync("/EventType", ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", LocationPath(denied), StringComparison.Ordinal);
    }

    private AsiBasecodeDBContext Context() => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    private async Task<int> SeedUsersAsync(string officerEmail, string memberEmail, string password, CancellationToken cancellationToken)
    {
        var hasher = new Argon2PasswordHasher();
        await using var context = Context();
        var officer = new User
        {
            UserId = officerEmail,
            Name = "Event Type Officer",
            Password = hasher.Hash(password),
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        var member = new User
        {
            UserId = memberEmail,
            Name = "Event Type Member",
            Password = hasher.Hash(password),
            RoleId = RoleCode.Member,
            IsActive = true
        };
        context.Users.AddRange(officer, member);
        await context.SaveChangesAsync(cancellationToken);
        return officer.Id;
    }

    private async Task SeedEventTypesAsync(int actorId, CancellationToken cancellationToken)
    {
        await using var context = Context();
        for (var index = 1; index <= 26; index++)
        {
            context.EventTypes.Add(new EventType
            {
                TypeName = $"Flow Type {index:D2}",
                Description = $"Flow description {index:D2}",
                IsActive = true,
                CreatedBy = actorId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);

    private static async Task SignInAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        var loginPage = await client.GetStringAsync("/Account/Login", cancellationToken);
        var token = Regex.Match(loginPage, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        var response = await client.PostAsync("/Account/Login", Form(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> AntiforgeryTokenAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        var page = await client.GetStringAsync(url, cancellationToken);
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private static string LocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }
}
