using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class EventCombinedEditFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task CombinedEditsEnforceStateValidationAuthorizationAndAtomicAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var officerEmail = $"combined-officer-{suffix}@attendta.test";
        var memberEmail = $"combined-member-{suffix}@attendta.test";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var actor = await EventDraftTestSupport.SeedUsersAsync(fixture.ConnectionString, officerEmail, memberEmail, password, ct);
        var (type, venue) = await EventDraftTestSupport.SeedActiveReferencesAsync(fixture.ConnectionString, actor, 40, ct);
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var scope = factory.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
        var original = await files.SaveAsync(PrivateFileArea.EventImages, new MemoryStream(Jpeg(1)), ".jpg", ct);
        var publishedOriginal = await files.SaveAsync(PrivateFileArea.EventImages, new MemoryStream(Jpeg(2)), ".jpg", ct);
        int draftId, publishedId;
        await using (var db = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            Event Item(EventStatusCode status, string path) => new()
            {
                Title = "Original", Description = "Before", TypeId = type, VenueId = venue,
                StartsAt = new DateTime(2026, 12, 10, 1, 0, 0), RegistrationDeadline = new DateTime(2026, 12, 9, 9, 0, 0),
                ParticipantLimit = 20, EventStatusId = status, ImagePath = path, CreatedBy = actor, CreatedAt = DateTime.UtcNow
            };
            var draft = Item(EventStatusCode.Draft, original);
            var published = Item(EventStatusCode.Published, publishedOriginal);
            db.Events.AddRange(draft, published);
            await db.SaveChangesAsync(ct);
            draftId = draft.EventId;
            publishedId = published.EventId;
        }
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var member = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await EventDraftTestSupport.SignInAsync(officer, officerEmail, password, ct);
        await EventDraftTestSupport.SignInAsync(member, memberEmail, password, ct);
        await VerifyCoverCheckboxAsync(officer, draftId, ct);
        var token = await EventDraftTestSupport.AntiforgeryTokenAsync(officer, "/Event", ct);
        var memberToken = await EventDraftTestSupport.AntiforgeryTokenAsync(member, "/Home/Index", ct);

        async Task<string> Version(int id)
        {
            var html = await officer.GetStringAsync($"/Event/Edit?id={id}", ct);
            var match = Regex.Match(html, "name=\"CoverVersion\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success);
            return match.Groups[1].Value;
        }
        MultipartFormDataContent Form(int id, string title, string version, string? csrf, byte[]? image = null, bool remove = false)
        {
            var form = new MultipartFormDataContent();
            var values = new Dictionary<string, string>
            {
                ["EventId"] = id.ToString(CultureInfo.InvariantCulture), ["Title"] = title, ["Description"] = "After",
                ["TypeId"] = type.ToString(CultureInfo.InvariantCulture), ["VenueId"] = venue.ToString(CultureInfo.InvariantCulture),
                ["StartsAt"] = "2026-12-10T09:00", ["RegistrationDeadline"] = "2026-12-09T17:00",
                ["ParticipantLimit"] = "20", ["CoverVersion"] = version
            };
            if (csrf != null) values["__RequestVerificationToken"] = csrf;
            if (remove) values["removeCover"] = "true";
            foreach (var pair in values) form.Add(new StringContent(pair.Value), pair.Key);
            if (image != null) form.Add(new ByteArrayContent(image), "cover", "cover.jpg");
            return form;
        }
        async Task<HttpResponseMessage> Post(HttpClient client, int id, string title, string version, string? csrf, byte[]? image = null, bool remove = false, bool dialog = false)
        {
            using var form = Form(id, title, version, csrf, image, remove);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Event/Edit") { Content = form };
            if (dialog) request.Headers.Add("X-Record-Dialog", "true");
            return await client.SendAsync(request, ct);
        }
        async Task<(Event Row, List<ASI.Basecode.Data.Models.ActivityLog> Audit)> State(int id)
        {
            await using var db = EventDraftTestSupport.Context(fixture.ConnectionString);
            return (await db.Events.AsNoTracking().SingleAsync(x => x.EventId == id, ct),
                await db.ActivityLogs.AsNoTracking().Where(x => x.EntityType == "events" && x.EntityId == id).OrderBy(x => x.LogId).ToListAsync(ct));
        }
        string Physical(string path) => Path.Combine(factory.SetupRoot, path.Replace('/', Path.DirectorySeparatorChar));
        var draftVersion = await Version(draftId);
        var forbidden = await Post(member, draftId, "Forbidden", draftVersion, memberToken, Jpeg(3));
        Assert.Contains("/Account/AccessDenied", EventDraftTestSupport.LocationPath(forbidden), StringComparison.Ordinal);
        var unauthenticated = await Post(anonymous, draftId, "Anonymous", draftVersion, null, Jpeg(3));
        Assert.Contains("/Account/Login", EventDraftTestSupport.LocationPath(unauthenticated), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(officer, draftId, "No token", draftVersion, null, Jpeg(3))).StatusCode);
        var invalid = await Post(officer, draftId, "Still entered", draftVersion, token, [1, 2, 3], dialog: true);
        var invalidHtml = await invalid.Content.ReadAsStringAsync(ct);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("does not match a supported image format", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("Still entered", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("Choose the image again", invalidHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await Post(officer, draftId, "", draftVersion, token, Jpeg(3))).StatusCode);
        var conflict = await Post(officer, draftId, "Conflict", draftVersion, token, Jpeg(3), remove: true);
        Assert.Contains("not both", await conflict.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        var before = await State(draftId);
        Assert.Equal("Original", before.Row.Title);
        Assert.Equal(original, before.Row.ImagePath);
        Assert.Empty(before.Audit);
        Assert.True(File.Exists(Physical(original)));

        var replacement = await Post(officer, draftId, "Draft replaced", draftVersion, token, Jpeg(4), dialog: true);
        Assert.Contains("\"saved\":true", await replacement.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        var replaced = await State(draftId);
        Assert.Equal("Draft replaced", replaced.Row.Title);
        Assert.NotEqual(original, replaced.Row.ImagePath);
        Assert.False(File.Exists(Physical(original)));
        Assert.True(File.Exists(Physical(replaced.Row.ImagePath)));
        AssertAudit(Assert.Single(replaced.Audit), "Original", "Draft replaced", true, true);
        var stale = await Post(officer, draftId, "Stale", draftVersion, token, Jpeg(5));
        Assert.Contains("cover changed", await stale.Content.ReadAsStringAsync(ct), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Physical(replaced.Row.ImagePath)));

        var remove = await Post(officer, draftId, "Draft removed", await Version(draftId), token, remove: true);
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);
        var removed = await State(draftId);
        Assert.Equal("Draft removed", removed.Row.Title);
        Assert.Null(removed.Row.ImagePath);
        Assert.False(File.Exists(Physical(replaced.Row.ImagePath)));
        Assert.Equal(2, removed.Audit.Count);
        AssertAudit(removed.Audit[1], "Draft replaced", "Draft removed", true, false);

        var publishedVersion = await Version(publishedId);
        var refused = await Post(officer, publishedId, "Refused", publishedVersion, token, remove: true);
        Assert.Contains("Only draft event covers can be removed", await refused.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        Assert.Equal(publishedOriginal, (await State(publishedId)).Row.ImagePath);
        Assert.Empty((await State(publishedId)).Audit);
        var publishedReplace = await Post(officer, publishedId, "Published replaced", publishedVersion, token, Jpeg(6));
        Assert.Equal(HttpStatusCode.Redirect, publishedReplace.StatusCode);
        var publishedAfter = await State(publishedId);
        Assert.NotEqual(publishedOriginal, publishedAfter.Row.ImagePath);
        Assert.False(File.Exists(Physical(publishedOriginal)));
        AssertAudit(Assert.Single(publishedAfter.Audit), "Original", "Published replaced", true, true);

        await using (var connection = new SqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "ALTER TABLE dbo.events ADD CONSTRAINT CK_CombinedEditFailure CHECK (title <> 'Reject combined');";
            await command.ExecuteNonQueryAsync(ct);
            try
            {
                var failed = await Post(officer, publishedId, "Reject combined", await Version(publishedId), token, Jpeg(7));
                Assert.Contains("save could not be confirmed", await failed.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
                var afterFailure = await State(publishedId);
                Assert.Equal("Published replaced", afterFailure.Row.Title);
                Assert.Equal(publishedAfter.Row.ImagePath, afterFailure.Row.ImagePath);
                Assert.Single(afterFailure.Audit);
                Assert.True(File.Exists(Physical(publishedAfter.Row.ImagePath)));
            }
            finally
            {
                command.CommandText = "ALTER TABLE dbo.events DROP CONSTRAINT CK_CombinedEditFailure;";
                await command.ExecuteNonQueryAsync(ct);
            }
        }

        // An inner service call must not delete either file before an outer transaction decides.
        using var nestedScope = factory.Services.CreateScope();
        var unit = nestedScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var accessor = nestedScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.ToString(CultureInfo.InvariantCulture)), new Claim(ClaimTypes.Role, RoleCodes.Officer)], "test"))
        };
        var service = nestedScope.ServiceProvider.GetRequiredService<IEventLifecycleService>();
        var nestedModel = await service.GetEventAsync(publishedId, ct);
        nestedModel.Title = "Nested replacement";
        string nestedPath;
        await using (var transaction = await unit.Database.Database.BeginTransactionAsync(ct))
        {
            await service.UpdateDraftWithCoverAsync(nestedModel, new MemoryStream(Jpeg(8)), ".jpg", false, ct);
            nestedPath = (await unit.Database.Set<Event>().SingleAsync(e => e.EventId == publishedId, ct)).ImagePath;
            Assert.NotEqual(publishedAfter.Row.ImagePath, nestedPath);
            Assert.True(File.Exists(Physical(publishedAfter.Row.ImagePath)));
            Assert.True(File.Exists(Physical(nestedPath)));
            await transaction.RollbackAsync(ct);
        }
        var afterRollback = await State(publishedId);
        Assert.Equal("Published replaced", afterRollback.Row.Title);
        Assert.Equal(publishedAfter.Row.ImagePath, afterRollback.Row.ImagePath);
        Assert.Single(afterRollback.Audit);
        Assert.True(File.Exists(Physical(publishedAfter.Row.ImagePath)));
        Assert.True(File.Exists(Physical(nestedPath)));
    }

    private static async Task VerifyCoverCheckboxAsync(HttpClient client, int eventId, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.RouteAsync("**/*", async route =>
        {
            using var response = await client.GetAsync(route.Request.Url, ct);
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(header => header.Key is not ("Transfer-Encoding" or "Content-Length" or "Content-Encoding"))
                .ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
            await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = await response.Content.ReadAsByteArrayAsync(ct) });
        });
        foreach (var width in new[] { 1440, 390 })
        {
            await page.SetViewportSizeAsync(width, 900);
            await page.GotoAsync($"http://localhost/Event/Edit?id={eventId}");
            var checkbox = page.GetByRole(AriaRole.Checkbox, new() { Name = "Remove current cover" });
            var label = page.Locator("label.cover-remove");
            var box = await checkbox.BoundingBoxAsync();
            var target = await label.BoundingBoxAsync();
            var help = await page.Locator("#cover-help").BoundingBoxAsync();
            Assert.NotNull(box);
            Assert.NotNull(target);
            Assert.NotNull(help);
            Assert.InRange(box.Width, 15, 17);
            Assert.InRange(box.Height, 15, 17);
            Assert.True(target.Height >= 44);
            Assert.True(target.Y >= help.Y + help.Height);
            Assert.InRange(Math.Abs(target.X - help.X), 0, 1);
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            await checkbox.FocusAsync();
            await page.Keyboard.PressAsync("Space");
            Assert.True(await checkbox.IsCheckedAsync());
            await label.ClickAsync();
            Assert.False(await checkbox.IsCheckedAsync());
        }
    }

    private static byte[] Jpeg(byte marker) => [0xff, 0xd8, 0xff, marker, 0, 0, 0, 0];

    private static void AssertAudit(ActivityLog audit, string oldTitle, string newTitle, bool oldCover, bool newCover)
    {
        Assert.Equal(AuditActions.EventUpdated, audit.Action);
        Assert.Contains($"\"Title\":\"{oldTitle}\"", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains($"\"Title\":\"{newTitle}\"", audit.NewValues, StringComparison.Ordinal);
        Assert.Contains($"\"HasCover\":{oldCover.ToString().ToLowerInvariant()}", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains($"\"HasCover\":{newCover.ToString().ToLowerInvariant()}", audit.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"CoverChanged\":true", audit.NewValues, StringComparison.Ordinal);
        Assert.DoesNotContain("uploads/", audit.OldValues, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("uploads/", audit.NewValues, StringComparison.OrdinalIgnoreCase);
    }
}
