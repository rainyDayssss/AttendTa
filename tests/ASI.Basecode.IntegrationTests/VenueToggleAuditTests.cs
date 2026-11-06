using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class VenueToggleAuditTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Password = "VenueToggle#2026";

    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient anonymousClient = null!;
    private HttpClient memberClient = null!;
    private HttpClient officerClient = null!;
    private string officerEmail = null!;
    private string memberEmail = null!;
    private int officerId;

    public VenueToggleAuditTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        officerEmail = $"venue-toggle-officer-{suffix}@attendta.test";
        memberEmail = $"venue-toggle-member-{suffix}@attendta.test";
        officerId = await SeedUserAsync(officerEmail, RoleCode.Officer);
        await SeedUserAsync(memberEmail, RoleCode.Member);

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
    }

    public async ValueTask DisposeAsync()
    {
        anonymousClient?.Dispose();
        memberClient?.Dispose();
        officerClient?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
    }

    [Fact]
    public async Task OfficerToggleKeepsThePageLocksTheVenueAndAppendsAPathFreeStatusAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (venueId, linkedEventId) = await SeedAsync(ct);

        var anonymous = await anonymousClient.PostAsync($"/Venue/ToggleActive?id={venueId}", null, ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", GetLocationPath(anonymous), StringComparison.Ordinal);

        await SignInAsync(memberClient, memberEmail);
        var denied = await memberClient.PostAsync($"/Venue/ToggleActive?id={venueId}", null, ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(denied), StringComparison.Ordinal);

        await SignInAsync(officerClient, officerEmail);
        var pageTwo = await officerClient.GetStringAsync("/Venue?page=2", ct);
        Assert.Contains("Page 2 of 2", pageTwo, StringComparison.Ordinal);
        var targetRow = Row(pageTwo, venueId);
        Assert.Contains("class=\"badge badge-active\">Active</span>", targetRow, StringComparison.Ordinal);
        Assert.Contains("Deactivate", targetRow, StringComparison.Ordinal);
        Assert.Contains("name=\"page\" value=\"2\"", targetRow, StringComparison.Ordinal);

        var toggled = await officerClient.PostAsync($"/Venue/ToggleActive?id={venueId}", ToggleForm(AntiforgeryToken(pageTwo), 2), ct);
        Assert.Equal(HttpStatusCode.Redirect, toggled.StatusCode);
        Assert.Equal("/Venue?page=2", GetLocationPath(toggled));

        var afterToggle = await officerClient.GetStringAsync("/Venue?page=2", ct);
        var inactiveRow = Row(afterToggle, venueId);
        Assert.Contains("class=\"badge badge-inactive\">Inactive</span>", inactiveRow, StringComparison.Ordinal);
        Assert.Contains("Activate", inactiveRow, StringComparison.Ordinal);
        Assert.DoesNotContain("Deactivate", inactiveRow, StringComparison.Ordinal);

        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var observer = new AsiBasecodeDBContext(options);
        var venue = await observer.Venues.AsNoTracking().SingleAsync(item => item.VenueId == venueId, ct);
        Assert.False(venue.IsActive);
        Assert.Equal(officerId, venue.UpdatedBy);
        Assert.True(await observer.Set<Event>().AsNoTracking().AnyAsync(item => item.EventId == linkedEventId, ct));

        var firstLog = Assert.Single(await LogsAsync(observer, venueId, ct));
        Assert.Equal("venue.status_changed", firstLog.Action);
        Assert.Equal("venues", firstLog.EntityType);
        Assert.Equal(officerId, firstLog.ActorUserId);
        Assert.Equal("Venue status changed.", firstLog.Description);
        AssertStatus(firstLog.OldValues, expectedActive: true, expectedLinkedEventCount: 1);
        AssertStatus(firstLog.NewValues, expectedActive: false, expectedLinkedEventCount: 1);
        Assert.DoesNotContain("uploads/", firstLog.OldValues, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("uploads/", firstLog.NewValues, StringComparison.OrdinalIgnoreCase);

        var toggledBack = await officerClient.PostAsync($"/Venue/ToggleActive?id={venueId}", ToggleForm(AntiforgeryToken(afterToggle), 2), ct);
        Assert.Equal(HttpStatusCode.Redirect, toggledBack.StatusCode);
        Assert.Equal("/Venue?page=2", GetLocationPath(toggledBack));

        var logs = (await LogsAsync(observer, venueId, ct)).OrderBy(log => log.LogId).ToList();
        Assert.Equal(2, logs.Count);
        AssertStatus(logs[1].OldValues, expectedActive: false, expectedLinkedEventCount: 1);
        AssertStatus(logs[1].NewValues, expectedActive: true, expectedLinkedEventCount: 1);
        Assert.True((await observer.Venues.AsNoTracking().SingleAsync(item => item.VenueId == venueId, ct)).IsActive);

        var missingPage = await officerClient.GetStringAsync("/Venue?page=2", ct);
        var missing = await officerClient.PostAsync($"/Venue/ToggleActive?id={int.MaxValue}", ToggleForm(AntiforgeryToken(missingPage), 2), ct);
        Assert.Equal(HttpStatusCode.Redirect, missing.StatusCode);
        Assert.Equal("/Venue?page=2", GetLocationPath(missing));
        Assert.Equal(2, (await LogsAsync(observer, venueId, ct)).Count);
        Assert.False(await observer.ActivityLogs.AsNoTracking().AnyAsync(log => log.EntityId == int.MaxValue && log.EntityType == "venues", ct));
    }

    private static Task<List<ActivityLog>> LogsAsync(AsiBasecodeDBContext context, int venueId, CancellationToken cancellationToken) =>
        context.ActivityLogs.AsNoTracking()
            .Where(log => log.EntityType == "venues" && log.EntityId == venueId)
            .ToListAsync(cancellationToken);

    private static void AssertStatus(string json, bool expectedActive, int expectedLinkedEventCount)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expectedActive, document.RootElement.GetProperty("IsActive").GetBoolean());
        Assert.Equal(expectedLinkedEventCount, document.RootElement.GetProperty("LinkedEventCount").GetInt32());
    }

    private static string Row(string html, int venueId)
    {
        var match = Regex.Match(html, $"<tr data-venue-id=\"{venueId}\">(.*?)</tr>", RegexOptions.Singleline);
        Assert.True(match.Success, $"No venue row was rendered for venue {venueId}.");
        return match.Value;
    }

    private static string AntiforgeryToken(string html)
    {
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    private static FormUrlEncodedContent ToggleForm(string token, int page) => new(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token,
        ["page"] = page.ToString()
    });

    private async Task<(int VenueId, int EventId)> SeedAsync(CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        var type = new EventType
        {
            TypeName = $"Toggle Type {Guid.NewGuid():N}",
            Description = "test",
            IsActive = true,
            CreatedBy = officerId,
            CreatedAt = DateTime.UtcNow
        };
        context.EventTypes.Add(type);
        await context.SaveChangesAsync(cancellationToken);

        var venues = Enumerable.Range(1, 25).Select(index => new Venue
        {
            RoomName = $"Filler Venue {index:D2}",
            Capacity = 20,
            ImagePath = "uploads/venues/filler.jpg",
            IsActive = true,
            CreatedBy = officerId,
            CreatedAt = DateTime.UtcNow
        }).ToList();
        var target = new Venue
        {
            RoomName = "Zzz Toggle Target",
            Capacity = 40,
            ImagePath = "uploads/venues/target.jpg",
            IsActive = true,
            CreatedBy = officerId,
            CreatedAt = DateTime.UtcNow
        };
        context.Venues.AddRange(venues);
        context.Venues.Add(target);
        await context.SaveChangesAsync(cancellationToken);

        var linked = new Event
        {
            Title = "Linked draft event",
            TypeId = type.TypeId,
            VenueId = target.VenueId,
            StartsAt = DateTime.UtcNow.AddDays(2),
            RegistrationDeadline = DateTime.UtcNow.AddDays(1),
            ParticipantLimit = 30,
            EventStatusId = EventStatusCode.Draft,
            CreatedBy = officerId,
            CreatedAt = DateTime.UtcNow
        };
        context.Set<Event>().Add(linked);
        await context.SaveChangesAsync(cancellationToken);
        return (target.VenueId, linked.EventId);
    }

    private async Task<int> SeedUserAsync(string email, RoleCode role)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        var user = new User
        {
            UserId = email,
            Name = "Venue toggle audit test",
            Password = hasher.Hash(Password),
            RoleId = role,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }

    private static async Task SignInAsync(HttpClient targetClient, string email)
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

    private static string GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }
}
