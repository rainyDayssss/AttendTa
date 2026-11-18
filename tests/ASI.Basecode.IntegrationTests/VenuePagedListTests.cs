using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
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
    public async Task VenueCapacityAndStatusRemainSeparatedAndAlignedAcrossViewports()
    {
        var ct = TestContext.Current.CancellationToken;
        var prefix = $"Spacing-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        var venues = new[]
        {
            new Venue { RoomName = prefix + "-A", Capacity = 7, ImagePath = "uploads/venues/a.jpg", IsActive = true, CreatedBy = officerId, CreatedAt = DateTime.UtcNow },
            new Venue { RoomName = prefix + "-B", Capacity = 2147483647, ImagePath = "uploads/venues/b.jpg", IsActive = false, CreatedBy = officerId, CreatedAt = DateTime.UtcNow }
        };
        try
        {
        await using (var context = new AsiBasecodeDBContext(options))
        {
            context.Venues.AddRange(venues);
            await context.SaveChangesAsync(ct);
        }
        await SignInAsync(officerClient, officerEmail);
        var html = await officerClient.GetStringAsync($"/Venue?search={prefix}", ct);
        var table = Regex.Match(html, "<div class=\"table-scroll\"[^>]*>.*?</table>\\s*</div>", RegexOptions.Singleline);
        Assert.True(table.Success);
        var css = await officerClient.GetStringAsync("/css/app.css", ct);
        Assert.Contains("venue-table", css, StringComparison.Ordinal);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<!doctype html><html><head><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><style>{css}</style></head><body>{table.Value}</body></html>");
        Assert.Equal(2, await page.Locator("[data-venue-id]").CountAsync());

        // 720 CSS pixels is the effective layout width of a 1440px viewport at 200% zoom.
        foreach (var width in new[] { 1440, 720 })
        {
            await page.SetViewportSizeAsync(width, 900);
            Assert.True(await page.EvaluateAsync<bool>("""
                () => {
                    const header = document.querySelector('.venue-table th.numeric');
                    const textBounds = element => { const range = document.createRange(); range.selectNodeContents(element); return range.getBoundingClientRect(); };
                    return getComputedStyle(header).textAlign === 'right' &&
                        [...document.querySelectorAll('[data-venue-id]')].every(row => {
                            const capacity = row.querySelector('td.numeric');
                            const status = row.querySelector('[data-label="Status"] .badge');
                            return getComputedStyle(capacity).textAlign === 'right' &&
                                Math.abs(textBounds(header).right - textBounds(capacity).right) < 1 &&
                                textBounds(capacity).left >= capacity.getBoundingClientRect().left &&
                                status.getBoundingClientRect().left - textBounds(capacity).right >= 50 &&
                                status.getBoundingClientRect().right <= status.parentElement.getBoundingClientRect().right + 1;
                        });
                }
                """), $"Capacity alignment or status separation failed at {width} CSS pixels.");
        }

        await page.SetViewportSizeAsync(390, 900);
        Assert.True(await page.EvaluateAsync<bool>("""
            () => [...document.querySelectorAll('[data-venue-id]')].every(row => {
                const capacity = row.querySelector('td.numeric');
                const status = row.querySelector('[data-label="Status"]');
                const badge = status.querySelector('.badge');
                return getComputedStyle(capacity).display === 'block' &&
                    getComputedStyle(capacity, '::before').content.includes('Capacity') &&
                    getComputedStyle(status, '::before').content.includes('Status') &&
                    capacity.getBoundingClientRect().bottom <= status.getBoundingClientRect().top + 1 &&
                    badge.getBoundingClientRect().right <= status.getBoundingClientRect().right + 1;
            })
            """), "Mobile venue rows must keep labeled, visible stacked cells.");
        }
        finally
        {
            // Delete only the rows created by this test in its disposable fixture database.
            var ids = venues.Where(venue => venue.VenueId > 0).Select(venue => venue.VenueId).ToArray();
            await using var cleanup = new AsiBasecodeDBContext(options);
            await cleanup.Venues.Where(venue => ids.Contains(venue.VenueId)).ExecuteDeleteAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task FailedListRetainsVenueHeadersAndRetryInsteadOfEmptyState()
    {
        var ct = TestContext.Current.CancellationToken;
        using var failingFactory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        failingFactory.ConfigureServicesForTesting = services =>
        {
            services.RemoveAll<IVenueService>();
            services.AddScoped<IVenueService, FailingVenueService>();
        };
        using var failingClient = failingFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(failingClient, officerEmail);

        var html = await failingClient.GetStringAsync("/Venue?status=inactive", ct);
        Assert.Contains("The venue list could not be loaded. Please try again.", html, StringComparison.Ordinal);
        Assert.Contains("Try again", html, StringComparison.Ordinal);
        Assert.Contains("data-table table-sticky venue-table", html, StringComparison.Ordinal);
        Assert.Contains("class=\"numeric\">Capacity", html, StringComparison.Ordinal);
        Assert.Contains("class=\"column-status\">Status", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No inactive venues.", html, StringComparison.Ordinal);
    }

    private sealed class FailingVenueService : IVenueService
    {
        public FailingVenueService() { }
        public Task<PagedResult<VenueViewModel>> GetPageAsync(string status, int? pageNumber, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Test failure");
        public VenueViewModel GetVenue(int id) => throw new NotSupportedException();
        public bool IsServablePhoto(string path) => throw new NotSupportedException();
        public Task<VenuePhotoStream> GetPhotoAsync(int venueId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ManagementSeesRoomNameOrderedPagesAndClampedPastEnd()
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
        Assert.Contains("data-table table-sticky venue-table", empty, StringComparison.Ordinal);
        Assert.Contains("class=\"numeric\">Capacity", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("data-venue-id=", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", empty, StringComparison.Ordinal);

        var (pageOne, pageTwo) = await SeedVenuesAsync(ct);

        var firstPage = await officerClient.GetStringAsync("/Venue", ct);
        Assert.Contains("data-table table-sticky venue-table", firstPage, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 6", firstPage, StringComparison.Ordinal);
        Assert.Equal(pageOne, VenueIds(firstPage));
        Assert.Contains("aria-label=\"Next page\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Previous page\"", firstPage, StringComparison.Ordinal);
        foreach (var venueId in pageOne)
        {
            var row = Row(firstPage, venueId);
            Assert.Contains("data-label=\"Capacity\" class=\"numeric\"", row, StringComparison.Ordinal);
            Assert.Contains("data-label=\"Status\"", row, StringComparison.Ordinal);
            // The owner removed the list thumbnail deliberately; the photo stays reachable
            // through the Venue Photo endpoint, the edit form and the row details dialog.
            // Assert the intended contract so the omission is a decision, not a silent regression.
            Assert.DoesNotContain("venue-thumb", row, StringComparison.Ordinal);
            Assert.Contains("Show details", row, StringComparison.Ordinal);
            Assert.Contains($"action=\"/Venue/ToggleActive?id={venueId}\"", row, StringComparison.Ordinal);
        }

        Assert.DoesNotContain($"data-venue-id=\"{pageTwo[0]}\"", firstPage, StringComparison.Ordinal);

        var lastPage = await officerClient.GetStringAsync("/Venue?page=6", ct);
        Assert.Contains("Page 6 of 6", lastPage, StringComparison.Ordinal);
        Assert.Equal(pageTwo, VenueIds(lastPage));
        Assert.Contains("aria-label=\"Previous page\"", lastPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", lastPage, StringComparison.Ordinal);

        var pastEnd = await officerClient.GetStringAsync("/Venue?page=999999999", ct);
        Assert.Contains("Page 6 of 6", pastEnd, StringComparison.Ordinal);
        Assert.Equal(pageTwo, VenueIds(pastEnd));
        Assert.DoesNotContain("aria-label=\"Next page\"", pastEnd, StringComparison.Ordinal);

        foreach (var invalidPage in new[] { "0", "-1", "not-a-number", "2147483648" })
        {
            var invalid = await officerClient.GetStringAsync($"/Venue?page={invalidPage}", ct);
            Assert.Contains("Page 1 of 6", invalid, StringComparison.Ordinal);
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
        return (ordered[..5], ordered[25..]);
    }

    private async Task<int> SeedUserAsync(string email, RoleCode role)
    {
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        await using var context = new AsiBasecodeDBContext(options);
        var user = new User
        {
            Email = email,
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
