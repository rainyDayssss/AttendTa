using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class VenueEditorBehaviorTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public async Task FailedSubmissionRendersReselectionAndOnlyAvailableCurrentPhoto(bool dialog, bool edit, bool servable)
    {
        var ct = TestContext.Current.CancellationToken;
        var password = $"Test#{Guid.NewGuid():N}a1";
        var email = $"venue-editor-{Guid.NewGuid():N}@attendta.test";
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var venueId = await SeedAsync(factory, email, password, servable, ct);
        await SignInAsync(client, email, password, ct);
        var url = edit ? $"/Venue/Edit?id={venueId}" : "/Venue/Create";
        using var get = new HttpRequestMessage(HttpMethod.Get, url);
        if (dialog) get.Headers.Add("X-Record-Dialog", "true");
        using var page = await client.SendAsync(get, ct);
        var html = await page.Content.ReadAsStringAsync(ct);
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        form.Add(new StringContent("Validation venue"), "RoomName");
        form.Add(new StringContent("0"), "Capacity");
        if (edit) form.Add(new StringContent(venueId.ToString()), "VenueId");
        var photo = new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "TestAssets", "venue-upload.jpg"), ct));
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(photo, "photo", "selected.jpg");
        using var request = new HttpRequestMessage(HttpMethod.Post, edit ? "/Venue/Edit" : "/Venue/Create") { Content = form };
        if (dialog) request.Headers.Add("X-Record-Dialog", "true");
        using var response = await client.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("Choose the photo again", result, StringComparison.Ordinal);
        Assert.Contains("Capacity-validation", result, StringComparison.Ordinal);
        Assert.Contains("input-validation-error", result, StringComparison.Ordinal);
        Assert.Matches("data-record-preview[^>]*hidden", result);
        Assert.Equal(edit && servable, result.Contains("alt=\"Current venue photo\"", StringComparison.Ordinal));
        var input = Regex.Match(result, "<input id=\"photo\"[^>]*>").Value;
        Assert.NotEmpty(input);
        Assert.Equal(!edit, input.Contains("required=\"required\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShortViewportDialogControlsAreReachableByKeyboard()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = $"Test#{Guid.NewGuid():N}a1";
        var email = $"venue-keyboard-{Guid.NewGuid():N}@attendta.test";
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SeedAsync(factory, email, password, false, ct);
        await SignInAsync(client, email, password, ct);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 320 } });
        await page.RouteAsync("**/*", async route =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, route.Request.Url);
            if (route.Request.Headers.TryGetValue("x-record-dialog", out var header)) request.Headers.Add("X-Record-Dialog", header);
            using var response = await client.SendAsync(request, ct);
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(h => h.Key is not ("Transfer-Encoding" or "Content-Length" or "Content-Encoding"))
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
            await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = await response.Content.ReadAsByteArrayAsync(ct) });
        });
        await page.GotoAsync("http://localhost/Venue?fit=3m");
        await page.GetByRole(AriaRole.Link, new() { Name = "New Venue" }).FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await page.Locator("#record-dialog[open] #RoomName").WaitForAsync();
        var targets = new[] { "RoomName", "Capacity", "Location", "AvailabilityNotes", "ContactPerson", "photo" };
        var reached = new HashSet<string>();
        var submitReached = false;
        for (var i = 0; i < 20; i++)
        {
            var state = await page.EvaluateAsync<string>("""
                () => {
                    const e = document.activeElement, r = e.getBoundingClientRect();
                    const d = document.querySelector('#record-dialog').getBoundingClientRect();
                    const visible = r.width > 0 && r.height > 0 && r.top >= Math.max(0, d.top) - 1 && r.bottom <= Math.min(innerHeight, d.bottom) + 1;
                    return (visible ? 'visible:' : 'clipped:') + (e.id || (e.type === 'submit' ? 'submit' : 'other'));
                }
                """);
            var name = state[(state.IndexOf(':') + 1)..];
            if (targets.Contains(name) || name == "submit")
            {
                Assert.StartsWith("visible:", state, StringComparison.Ordinal);
                reached.Add(name);
                if (name == "submit") { submitReached = true; break; }
            }
            await page.Keyboard.PressAsync("Tab");
        }
        Assert.True(submitReached, "The submit button was not reachable by Tab.");
        Assert.All(targets, target => Assert.Contains(target, reached));
    }

    private async Task<int> SeedAsync(AttendTaWebApplicationFactory factory, string email, string password, bool servable, CancellationToken ct)
    {
        string path = "venues/legacy.jpg";
        if (servable)
        {
            using var scope = factory.Services.CreateScope();
            await using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestAssets", "venue-upload.jpg"));
            path = await scope.ServiceProvider.GetRequiredService<IPrivateFileStore>().SaveAsync(PrivateFileArea.VenueImages, file, ".jpg", ct);
        }
        await using var db = new AsiBasecodeDBContext(new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);
        var user = new User { Name = "Venue test officer", Email = email, Password = new Argon2PasswordHasher().Hash(password), RoleId = RoleCode.Officer, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        var venue = new Venue { RoomName = $"Venue {Guid.NewGuid():N}", Capacity = 20, ImagePath = path, IsActive = true, CreatedBy = user.Id, CreatedAt = DateTime.UtcNow };
        db.Venues.Add(venue);
        await db.SaveChangesAsync(ct);
        return venue.VenueId;
    }

    private static async Task SignInAsync(HttpClient client, string email, string password, CancellationToken ct)
    {
        var html = await client.GetStringAsync("/Account/Login", ct);
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = email, ["Password"] = password, ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
