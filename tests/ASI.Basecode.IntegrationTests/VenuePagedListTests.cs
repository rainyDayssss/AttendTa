using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class VenuePagedListTests : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Password = "VenuePaged#2026";

    private readonly SqlServerDatabaseFixture fixture;
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient client = null!;
    private HttpClient officerClient = null!;
    private string officerEmail = null!;
    private string memberEmail = null!;
    private int officerId;

    public VenuePagedListTests(SqlServerDatabaseFixture fixture) => this.fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        officerEmail = $"venue-page-officer-{suffix}@attendta.test";
        memberEmail = $"venue-page-member-{suffix}@attendta.test";
        officerId = await SeedUserAsync(officerEmail, RoleCode.Officer);
        await SeedUserAsync(memberEmail, RoleCode.Member);

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        officerClient?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
    }

    [Fact]
    public async Task ManagementSeesAuthorizedThumbnailsOnRoomNameOrderedPagesAndClampedPastEnd()
    {
        var ct = TestContext.Current.CancellationToken;

        var anonymous = await client.GetAsync("/Venue", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", GetLocationPath(anonymous), StringComparison.Ordinal);

        await SignInAsync(client, memberEmail);
        var denied = await client.GetAsync("/Venue", ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(denied), StringComparison.Ordinal);

        var deniedToggle = await client.PostAsync("/Venue/ToggleActive?id=1", null, ct);
        Assert.Equal(HttpStatusCode.Redirect, deniedToggle.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(deniedToggle), StringComparison.Ordinal);

        await SignInAsync(officerClient, officerEmail);
        var empty = await officerClient.GetStringAsync("/Venue", ct);
        Assert.Contains("No venues yet. Use \"New Venue\" to create the first one.", empty, StringComparison.Ordinal);
        Assert.Contains("data-table", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("data-venue-id=", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", empty, StringComparison.Ordinal);

        var (pageOne, pageTwo) = await SeedVenuesAsync(ct);

        var firstPage = await officerClient.GetStringAsync("/Venue", ct);
        Assert.Contains("Page 1 of 2", firstPage, StringComparison.Ordinal);
        Assert.Equal(pageOne, VenueIds(firstPage));
        Assert.Contains("aria-label=\"Next page\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Previous page\"", firstPage, StringComparison.Ordinal);
        foreach (var venueId in pageOne)
        {
            var row = Row(firstPage, venueId);
            Assert.Contains($"src=\"/Venue/Photo?id={venueId}\"", row, StringComparison.Ordinal);
            Assert.Contains("class=\"venue-thumb\"", row, StringComparison.Ordinal);
            Assert.Contains($"action=\"/Venue/ToggleActive?id={venueId}\"", row, StringComparison.Ordinal);
        }

        Assert.DoesNotContain($"data-venue-id=\"{pageTwo[0]}\"", firstPage, StringComparison.Ordinal);

        var lastPage = await officerClient.GetStringAsync("/Venue?page=2", ct);
        Assert.Contains("Page 2 of 2", lastPage, StringComparison.Ordinal);
        Assert.Equal(pageTwo, VenueIds(lastPage));
        Assert.Contains("aria-label=\"Previous page\"", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", lastPage, StringComparison.Ordinal);

        var pastEnd = await officerClient.GetStringAsync("/Venue?page=999999999", ct);
        Assert.Contains("Page 2 of 2", pastEnd, StringComparison.Ordinal);
        Assert.Equal(pageTwo, VenueIds(pastEnd));
        Assert.DoesNotContain("aria-label=\"Next page\"", pastEnd, StringComparison.Ordinal);

        foreach (var invalidPage in new[] { "0", "-1", "not-a-number", "2147483648" })
        {
            var invalid = await officerClient.GetStringAsync($"/Venue?page={invalidPage}", ct);
            Assert.Contains("Page 1 of 2", invalid, StringComparison.Ordinal);
            Assert.Equal(pageOne, VenueIds(invalid));
        }

        var photo = await officerClient.GetAsync($"/Venue/Photo?id={pageOne[0]}", ct);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
    }

    private static int[] VenueIds(string html) =>
        Regex.Matches(html, "data-venue-id=\"(\\d+)\"")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();

    private static string Row(string html, int venueId)
    {
        var match = Regex.Match(html, $"<tr data-venue-id=\"{venueId}\">(.*?)</tr>", RegexOptions.Singleline);
        Assert.True(match.Success, $"No venue row was rendered for venue {venueId}.");
        return match.Value;
    }

    private async Task<(int[] PageOne, int[] PageTwo)> SeedVenuesAsync(CancellationToken cancellationToken)
    {
        string photo;
        using (var scope = factory.Services.CreateScope())
        {
            var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
            await using var image = new MemoryStream(
                await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "TestAssets", "venue-upload.jpg"), cancellationToken));
            photo = await files.SaveAsync(PrivateFileArea.VenueImages, image, ".jpg", cancellationToken);
        }

        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        var venues = Enumerable.Range(1, 24).Select(index => new Venue
        {
            RoomName = $"Paged Venue {index:D2}",
            Capacity = 20,
            ImagePath = photo,
            IsActive = true,
            CreatedBy = officerId,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // Two rows share a room name so the venue_id tie-break decides their order.
        venues.Add(new Venue { RoomName = "Paged Venue 25", Capacity = 20, ImagePath = photo, IsActive = true, CreatedBy = officerId, CreatedAt = DateTime.UtcNow });
        venues.Add(new Venue { RoomName = "Paged Venue 25", Capacity = 20, ImagePath = photo, IsActive = true, CreatedBy = officerId, CreatedAt = DateTime.UtcNow });

        context.Venues.AddRange(venues);
        await context.SaveChangesAsync(cancellationToken);

        var ordered = venues.OrderBy(venue => venue.RoomName, StringComparer.Ordinal).ThenBy(venue => venue.VenueId).Select(venue => venue.VenueId).ToArray();
        return (ordered[..25], ordered[25..]);
    }

    private async Task<int> SeedUserAsync(string email, RoleCode role)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        var user = new User
        {
            UserId = email,
            Name = "Venue paged list test",
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
