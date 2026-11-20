using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class CompactListFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task RealListsFilterBeforePagingAndRenderCompactDetailsWithoutExposingHashes()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var password = $"Test#{Guid.NewGuid():N}a1";
        var hash = new Argon2PasswordHasher().Hash(password);
        await using var db = new AsiBasecodeDBContext(new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);
        var officer = new User { Email = "ui-officer@attendta.test", Name = "UI Review Officer", Password = hash, RoleId = RoleCode.Admin, IsActive = true };
        db.Users.Add(officer);
        await db.SaveChangesAsync(ct);
        var users = Enumerable.Range(1, 8).Select(i => new User { Email = $"ui-member-{i}@attendta.test", Name = $"Review Member {i:D2}", Password = hash, RoleId = RoleCode.Member, IsActive = true }).ToArray();
        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);
        var members = users.Select((user, index) => new Member { UserId = user.Id, StudentNumber = $"UI-{index + 1:D4}", Course = index == 0 ? "BS Information Technology" : index < 6 ? "BSIT" : "BSCS", YearLevel = 2, Section = "A", MobileNumber = "09000000000", MembershipStatusId = MembershipStatusCode.Active, CreatedBy = officer.Id, CreatedAt = now }).ToArray();
        db.Members.AddRange(members);
        var type = new EventType { TypeName = "Review Workshop", Description = string.Join(" ", Enumerable.Repeat("A full classification description that remains readable without opening an editor.", 20)), IsActive = true, CreatedBy = officer.Id, CreatedAt = now };
        var shortType = new EventType { TypeName = "Brief classification", Description = "Short description.", IsActive = true, CreatedBy = officer.Id, CreatedAt = now };
        var venue = new Venue { RoomName = "Review Hall", Location = "Campus building A", Capacity = 50, AvailabilityNotes = "Contact the officer before setup.", ContactPerson = "Sample contact", ImagePath = "uploads/venues/review.png", IsActive = true, CreatedBy = officer.Id, CreatedAt = now };
        db.AddRange(type, shortType, venue);
        await db.SaveChangesAsync(ct);
        var upcoming = new Event { Title = "Review Upcoming Event", Description = "Full event information.", TypeId = type.TypeId, VenueId = venue.VenueId, StartsAt = now.AddDays(3), RegistrationDeadline = now.AddDays(2), ParticipantLimit = 20, EventStatusId = EventStatusCode.Published, PublishedBy = officer.Id, PublishedAt = now, CreatedBy = officer.Id, CreatedAt = now };
        var completed = new Event { Title = "Review Completed Event", TypeId = type.TypeId, VenueId = venue.VenueId, StartsAt = now.AddDays(-2), RegistrationDeadline = now.AddDays(-3), ParticipantLimit = 20, EventStatusId = EventStatusCode.Completed, PublishedBy = officer.Id, PublishedAt = now.AddDays(-4), CompletedBy = officer.Id, CompletedAt = now.AddDays(-1), CreatedBy = officer.Id, CreatedAt = now.AddDays(-4) };
        db.AddRange(upcoming, completed);
        await db.SaveChangesAsync(ct);
        var confirmed = new List<Registration>();
        foreach (var member in members)
        {
            db.Registrations.Add(new Registration { MemberId = member.MemberId, EventId = upcoming.EventId, RegistrationStatusId = RegistrationStatusCode.Pending, CourseSnapshot = member.Course, YearLevelSnapshot = 2, SectionSnapshot = "A", SubmittedAt = now, CreatedBy = officer.Id, CreatedAt = now });
            var registration = new Registration { MemberId = member.MemberId, EventId = completed.EventId, RegistrationStatusId = RegistrationStatusCode.Confirmed, CourseSnapshot = member.Course, YearLevelSnapshot = 2, SectionSnapshot = "A", SubmittedAt = now.AddDays(-4), ReviewedBy = officer.Id, ReviewedAt = now.AddDays(-3), ConfirmedBy = officer.Id, ConfirmedAt = now.AddDays(-3), QrToken = Guid.NewGuid().ToString("N"), CreatedBy = officer.Id, CreatedAt = now.AddDays(-4) };
            confirmed.Add(registration); db.Registrations.Add(registration);
        }
        await db.SaveChangesAsync(ct);
        foreach (var registration in confirmed)
        {
            db.Set<Attendance>().Add(new Attendance { RegistrationId = registration.RegistrationId, CheckInMethodId = CheckInMethodCode.ManualSearch, RecordedBy = officer.Id, RecordedAt = now.AddDays(-2) });
            db.Certificates.Add(new Certificate { RegistrationId = registration.RegistrationId, CertificateStatusId = CertificateStatusCode.PendingApproval, CreatedBy = officer.Id, CreatedAt = now.AddDays(-1) });
        }
        db.AccountRequests.Add(new AccountRequest { FullName = "Sample Applicant", StudentNumber = "UI-APPLICANT", Email = "applicant@attendta.test", PasswordHash = hash, Course = "BSIT", YearLevel = 2, Section = "A", MobileNumber = "09000000001", AccountRequestStatusId = AccountRequestStatusCode.Pending, RequestedAt = now });
        await db.SaveChangesAsync(ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignIn(client, officer.Email, password, ct);
        var first = await client.GetStringAsync("/Member", ct);
        Assert.Equal(5, Regex.Matches(first, "data-member-id=").Count);
        Assert.Contains("Showing 1–5 of 8 members", first);
        Assert.DoesNotContain("class=\"paging-mode", first);
        Assert.Contains("class=\"sr-only\">Auto · 5 per page</span>", WebUtility.HtmlDecode(first));
        Assert.Contains("Page 1 of 2", first);
        Assert.Contains("aria-label=\"Next page\"", first);
        Assert.Contains("rel=\"next\"", first);
        using var legacy = await client.GetAsync("/Member?search=Review&course=BSIT&year=2&pageSize=2&page=4&fit=2", ct);
        Assert.Equal(HttpStatusCode.Redirect, legacy.StatusCode);
        var canonical = legacy.Headers.Location!.ToString();
        Assert.Contains("pageSize=5", canonical);
        Assert.Contains("page=2", canonical);
        var filtered = await client.GetStringAsync(canonical, ct);
        Assert.Equal(members.Skip(5).Take(1).Select(member => member.MemberId), Regex.Matches(filtered, "data-member-id=\"(\\d+)\"").Select(match => int.Parse(match.Groups[1].Value)));
        Assert.Contains("Showing 6–6 of 6 matching members", filtered);
        Assert.Contains("Page 2 of 2", filtered);
        Assert.Contains("pageSize=5", WebUtility.HtmlDecode(filtered));
        Assert.Contains("course=BSIT", filtered);
        Assert.Contains("Show details", filtered);
        Assert.Contains("<dialog", filtered);
        Assert.DoesNotContain(hash, filtered);
        Assert.Contains("popover class=\"row-menu\"", filtered);
        Assert.Contains("data-dialog-open=\"member-details-", filtered);
        Assert.Contains("name=\"course\"", filtered);
        Assert.Contains("value=\"BSCS\"", filtered);
        Assert.Contains("value=\"BSIT\"", filtered);
        var empty = await client.GetStringAsync("/Member?search=nobody-matches", ct);
        Assert.Contains("0 matching members", empty);
        Assert.DoesNotContain("aria-label=\"Pagination\"", empty);

        var pages = new Dictionary<string, string> {
            ["members"] = "/Member", ["venues"] = "/Venue", ["event-types"] = "/EventType", ["events"] = "/Event",
            ["requests"] = "/AccountRequest", ["accounts"] = "/admin/accounts", ["activity"] = "/admin/auditlogs",
            ["registrations"] = $"/RegistrationReview/Index?eventId={upcoming.EventId}", ["dossier"] = $"/Event/{upcoming.EventId}",
            ["eligibility"] = $"/Certificates?eventId={completed.EventId}", ["report"] = $"/Reports/Participation?eventId={completed.EventId}", ["browse-events"] = "/MemberEvents"
        };
        var capture = Environment.GetEnvironmentVariable("ATTENDTA_UI_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(capture)) Directory.CreateDirectory(capture);
        foreach (var page in pages)
        {
            using var response = await client.GetAsync(page.Value, ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{page.Value} returned {response.StatusCode}");
            var html = await response.Content.ReadAsStringAsync(ct);
            Assert.DoesNotContain(hash, html);
            Assert.Contains("features/list-controls.js", html);
            Assert.Contains("<label for=\"list-search\" class=\"field-label\">Search</label>", html);
            Assert.DoesNotContain("data-search-clear", html);
            if (page.Key == "requests")
            {
                var row = Regex.Match(html, "<tr data-request-id=.*?</tr>", RegexOptions.Singleline).Value;
                Assert.True(row.IndexOf("data-dialog-open=\"reject-dialog\"", StringComparison.Ordinal) < row.IndexOf("row-menu-trigger", StringComparison.Ordinal));
            }
            if (page.Key == "eligibility")
            {
                var row = Regex.Match(html, "<tr data-registration-id=.*?</tr>", RegexOptions.Singleline).Value;
                Assert.True(row.IndexOf("data-certificate-open=\"decline-certificate\"", StringComparison.Ordinal) < row.IndexOf("row-menu-trigger", StringComparison.Ordinal));
            }
            if (!string.IsNullOrWhiteSpace(capture)) await File.WriteAllTextAsync(Path.Combine(capture, page.Key + ".html"), html, ct);
        }

        var filteredRegistrations = await client.GetStringAsync($"/RegistrationReview/Index?eventId={upcoming.EventId}&search=Review%20Member%2001", ct);
        Assert.Single(Regex.Matches(filteredRegistrations, "data-registration-id="));
        Assert.Contains("Showing 1–1 of 1 matching registration", filteredRegistrations);
        using var legacyDossier = await client.GetAsync($"/Event/{upcoming.EventId}?pageSize=1&status=pending", ct);
        Assert.Equal(HttpStatusCode.Redirect, legacyDossier.StatusCode);
        var dossier = await client.GetStringAsync(legacyDossier.Headers.Location!.ToString(), ct);
        Assert.Equal(5, Regex.Matches(dossier, "data-registration-id=").Count);
        Assert.Contains("data-paging-mode=\"roster\"", dossier);
        Assert.Contains("pending registrations", dossier);
        var token = Regex.Match(dossier, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        using var post = await client.PostAsync($"/Event/ApproveSelected?id={upcoming.EventId}&pageSize=2&page=4&status=pending", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["page"] = "4"
        }), ct);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var postHtml = WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync(ct));
        Assert.Contains("Showing 6–8 of 8 matching pending registrations", postHtml);
        Assert.Contains("pageSize=5", postHtml);
        Assert.Contains("data-paging-fit=\"off\"", postHtml);
        Assert.DoesNotContain("class=\"paging-mode", postHtml);
        Assert.Contains("class=\"sr-only\">5 per page</span>", postHtml);
        var tallerDossier = await client.GetStringAsync($"/Event/{upcoming.EventId}?pageSize=8&status=pending", ct);
        Assert.Equal(8, Regex.Matches(tallerDossier, "data-registration-id=").Count);
        var resettable = await client.GetStringAsync($"/RegistrationReview/Index?eventId={upcoming.EventId}&search=Review&status=confirmed", ct);
        var resetHref = WebUtility.HtmlDecode(Regex.Match(resettable, "href=\"([^\"]+)\"[^>]*data-filter-clear").Groups[1].Value);
        Assert.Contains($"eventId={upcoming.EventId}", resetHref);
        Assert.DoesNotContain("search=", resetHref);
        Assert.DoesNotContain("status=", resetHref);
        Assert.Contains(">Reset</a>", resettable);
        foreach (var route in new[] {
            "/AccountRequest?status=pending", "/AccountRequest?status=all",
            "/AccountRequest?status=approved", "/AccountRequest?status=rejected",
            "/Member?status=active", "/Member?status=inactive",
            "/Venue?status=active", "/Venue?status=inactive",
            "/EventType?status=active", "/EventType?status=inactive",
            $"/Event/{upcoming.EventId}?status=confirmed", $"/Event/{upcoming.EventId}?status=cancelled"
        })
        {
            var tabOnly = await client.GetStringAsync(route, ct);
            Assert.DoesNotContain("data-filter-clear", tabOnly);
            var withSearch = await client.GetStringAsync(route + "&search=Review", ct);
            var tabReset = WebUtility.HtmlDecode(Regex.Match(withSearch, "href=\"([^\"]+)\"[^>]*data-filter-clear").Groups[1].Value);
            Assert.NotEmpty(tabReset);
            Assert.Contains("status=", tabReset);
            Assert.DoesNotContain("search=", tabReset);
            if (route.StartsWith("/Event/", StringComparison.Ordinal)) Assert.Contains($"/Event/{upcoming.EventId}", tabReset);
        }
        foreach (var route in new[] { "/AccountRequest?status=all&course=BSIT&year=2", "/Member?status=inactive&course=BSIT&year=2" })
        {
            var withFilters = await client.GetStringAsync(route, ct);
            var tabReset = WebUtility.HtmlDecode(Regex.Match(withFilters, "href=\"([^\"]+)\"[^>]*data-filter-clear").Groups[1].Value);
            Assert.Contains("status=", tabReset);
            Assert.DoesNotContain("course=", tabReset);
            Assert.DoesNotContain("year=", tabReset);
        }
        var accountsStatus = await client.GetStringAsync("/admin/accounts?filter=active", ct);
        Assert.Contains("data-filter-clear", accountsStatus);
        var eventsStatus = await client.GetStringAsync("/Event?filter=Published", ct);
        Assert.Contains("data-filter-clear", eventsStatus);
        var filteredEvents = await client.GetStringAsync($"/Event?search=Upcoming&typeId={type.TypeId}", ct);
        Assert.Contains("Showing 1–1 of 1 matching event", filteredEvents);
        Assert.DoesNotContain("Review Completed Event", filteredEvents);
        Assert.Contains($"value=\"{type.TypeId}\"", filteredEvents);
        var noTypeMatches = await client.GetStringAsync("/Event?typeId=2147483647", ct);
        Assert.Contains("0 matching events", noTypeMatches);
        var activeEvents = await client.GetStringAsync("/Events", ct);
        Assert.Contains("Review Upcoming Event", activeEvents);
        Assert.DoesNotContain("Review Completed Event", activeEvents);
        var completedEvents = await client.GetStringAsync("/Events?filter=Completed", ct);
        Assert.Contains("Review Completed Event", completedEvents);
        Assert.DoesNotContain("Review Upcoming Event", completedEvents);
        var filteredCertificates = await client.GetStringAsync($"/Certificates?eventId={completed.EventId}&search=UI-0001", ct);
        Assert.Contains("Showing 1–1 of 1 matching review", filteredCertificates);
        Assert.DoesNotContain("<dt>Issued</dt>", filteredCertificates);
        Assert.DoesNotContain("<dt>Certificate number</dt>", filteredCertificates);
        await VerifyTableDisclosureAsync(client, type.TypeId, shortType.TypeId, ct);

        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignIn(memberClient, users[0].Email, password, ct);
        foreach (var action in new[] { "Registrations", "Attendance", "Certificates" })
        {
            var html = await memberClient.GetStringAsync($"/MyEvents/{action}", ct);
            Assert.DoesNotContain("Review Member 02", html);
            if (!string.IsNullOrWhiteSpace(capture)) await File.WriteAllTextAsync(Path.Combine(capture, "my-" + action.ToLowerInvariant() + ".html"), html, ct);
        }
    }

    private static async Task VerifyTableDisclosureAsync(HttpClient client, int longTypeId, int shortTypeId, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.RouteAsync("**/*", async route =>
        {
            using var response = await client.GetAsync(route.Request.Url, ct);
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(header => header.Key is not ("Transfer-Encoding" or "Content-Length" or "Content-Encoding"))
                .ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
            await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = await response.Content.ReadAsByteArrayAsync(ct) });
        });
        foreach (var width in new[] { 1440, 1024, 390 })
        {
            await page.SetViewportSizeAsync(width, 900);
            foreach (var (name, route) in new[] { ("members", "/Member"), ("venues", "/Venue"), ("types", "/EventType") })
            {
                await page.GotoAsync($"http://localhost{route}?fit={(width < 768 ? "3m" : "3d")}");
                await page.EvaluateAsync("document.fonts.ready");
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), $"{name} overflow at {width}px");
                if (name == "members")
                {
                    Assert.Equal(5, await page.Locator("thead th").CountAsync());
                    Assert.True(await page.Locator("td[data-label='Email']").First.IsVisibleAsync());
                    Assert.Contains("BSIT · 2 · A", await page.Locator("tbody tr").First.InnerTextAsync());
                    await page.Locator(".row-menu-trigger").First.ClickAsync();
                    await page.GetByRole(AriaRole.Button, new() { Name = "Show details", Exact = false }).First.ClickAsync();
                    var fields = await page.Locator("dialog[open] dt").AllTextContentsAsync();
                    Assert.DoesNotContain("Email", fields);
                    Assert.DoesNotContain("Course", fields);
                    Assert.Contains("Mobile", fields);
                    await page.Keyboard.PressAsync("Escape");
                }
                if (name == "venues")
                {
                    Assert.Equal(new[] { "Room Name", "Location", "Capacity", "Status", "Actions" }, await page.Locator("thead th").AllTextContentsAsync());
                    if (width > 640)
                    {
                        Assert.Equal("right", await page.Locator("td[data-label='Capacity']").First.EvaluateAsync<string>("cell => getComputedStyle(cell).textAlign"));
                        Assert.Equal("right", await page.Locator("thead th.numeric").EvaluateAsync<string>("cell => getComputedStyle(cell).textAlign"));
                    }
                }
                if (name == "types")
                {
                    Assert.Equal(0, await page.Locator("[id^='type-details-']").CountAsync());
                    var longRow = page.Locator($"[data-type-id='{longTypeId}']");
                    var toggle = longRow.Locator("[data-expandable-toggle]");
                    Assert.True(await toggle.IsVisibleAsync());
                    Assert.False(await page.Locator($"[data-type-id='{shortTypeId}'] [data-expandable-toggle]").IsVisibleAsync());
                    await toggle.FocusAsync();
                    await page.Keyboard.PressAsync("Enter");
                    Assert.Equal("true", await toggle.GetAttributeAsync("aria-expanded"));
                    Assert.True(await longRow.Locator("[data-expandable-content]").EvaluateAsync<bool>("text => text.scrollHeight <= text.clientHeight + 1"));
                    await page.SetViewportSizeAsync(width - 40, 900);
                    await page.WaitForTimeoutAsync(550);
                    Assert.Equal("true", await toggle.GetAttributeAsync("aria-expanded"));
                    await page.SetViewportSizeAsync(width, 900);
                    await toggle.ClickAsync();
                    Assert.Equal("false", await toggle.GetAttributeAsync("aria-expanded"));
                }
                await page.EvaluateAsync("window.scrollTo(0, 0)");
                await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), $"attendta-table-{name}-{width}.png"), FullPage = true });
            }
        }
        await page.SetViewportSizeAsync(390, 600);
        await page.GotoAsync("http://localhost/Member?pageSize=12");
        await page.WaitForURLAsync("**/Member?pageSize=5&page=1&fit=3m");
        Assert.Equal(5, await page.Locator("[data-member-id]").CountAsync());
        var lastRow = page.Locator("[data-member-id]").Last;
        await lastRow.ScrollIntoViewIfNeededAsync();
        Assert.True(await lastRow.IsVisibleAsync());
        var pager = page.Locator("[data-paging-size]");
        await pager.ScrollIntoViewIfNeededAsync();
        Assert.True(await pager.IsVisibleAsync());
        Assert.True(await pager.EvaluateAsync<bool>("el => { const r = el.getBoundingClientRect(); return r.bottom <= innerHeight && r.top >= 0; }"));
        Assert.Empty(errors);
    }

    private static async Task SignIn(HttpClient client, string email, string password, CancellationToken ct)
    {
        var html = await client.GetStringAsync("/Account/Login", ct);
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = email, ["Password"] = password, ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
