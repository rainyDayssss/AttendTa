using System.Net;
using System.Security.Cryptography;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.DemoData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class CertificateLedgerFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task OfficerCanReviewIssueAndViewWithRealRendererWithoutPersistingCertificateRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) + "aA1!";
        var email = $"ledger-{Guid.NewGuid():N}@attendta.test";
        var officer = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Officer, ct);
        var (type, venue) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var startsAt = DateTime.UtcNow.AddDays(-2);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(fixture.ConnectionString, officer.Id, type, venue,
            EventStatusCode.Published, startsAt, startsAt.AddDays(-1), 20, null, "Campus Leadership Summit", ct);
        var registrationIds = new List<int>();
        var memberEmail = "";
        for (var index = 1; index <= 5; index++)
        {
            memberEmail = $"ledger-member-{Guid.NewGuid():N}@attendta.test";
            var user = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
            await using (var context = MemberEventTestSupport.Context(fixture.ConnectionString))
            {
                (await context.Users.SingleAsync(row => row.Id == user.Id, ct)).Name = $"Participant {index}";
                await context.SaveChangesAsync(ct);
            }
            var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
            var registration = await MemberEventTestSupport.SeedRegistrationAsync(fixture.ConnectionString,
                eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, officer.Id, ct);
            registrationIds.Add(registration.RegistrationId);
        }
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var enabled = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DemoData:Enabled", "true");
            builder.UseSetting("DemoData:AllowDatabase", new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(fixture.ConnectionString).InitialCatalog);
            builder.UseSetting("DemoData:TokenKey", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        });
        using var client = enabled.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymous = enabled.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/Certificates", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync($"/Certificates/Pdf/{registrationIds[0]}", ct)).StatusCode);
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);
        using var memberClient = enabled.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await MemberEventTestSupport.SignInAsync(memberClient, memberEmail, password, ct);
        Assert.Contains("AccessDenied", MemberEventTestSupport.LocationPath(await memberClient.GetAsync("/Certificates", ct)), StringComparison.Ordinal);
        Assert.Contains("AccessDenied", MemberEventTestSupport.LocationPath(await memberClient.GetAsync($"/Certificates/Pdf/{registrationIds[0]}", ct)), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/CertificateLedger/Approve",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = registrationIds[1].ToString() }), ct)).StatusCode);

        // The test seeds this event as Published; the demo seed populates its attendance and
        // certificate data, and the demo completes it live through the real service, which is what
        // makes it appear in the certificate ledger.
        var completeToken = await MemberEventTestSupport.AntiforgeryTokenAsync(client, "/Event", ct);
        using (var completed = await client.PostAsync($"/Event/Complete?id={eventItem.EventId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = eventItem.EventId.ToString(),
                ["__RequestVerificationToken"] = completeToken
            }), ct))
        {
            Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        }

        var html = await client.GetStringAsync("/Certificates", ct);
        Assert.Contains("Campus Leadership Summit", html, StringComparison.Ordinal);
        Assert.Contains("Participant 2", html, StringComparison.Ordinal);
        Assert.Contains("Decline reason", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("(?i)demo|mock|prototype|simulated|fixture", html);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, "/Certificates", ct);
        var store = enabled.Services.GetRequiredService<DemoStore>();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.RouteAsync("**/*", async route =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(route.Request.Method), route.Request.Url);
            if (route.Request.PostDataBuffer is byte[] body)
            {
                request.Content = new ByteArrayContent(body);
                if (route.Request.Headers.TryGetValue("content-type", out var contentType))
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
            var response = await client.SendAsync(request, ct);
            // TestServer has no listening socket; resolve redirects inside the browser relay.
            for (var redirects = 0; (int)response.StatusCode is 301 or 302 or 303 && redirects < 5; redirects++)
            {
                var location = new Uri(new Uri(route.Request.Url), response.Headers.Location!);
                response.Dispose();
                response = await client.GetAsync(location, ct);
            }
            using var finalResponse = response;
            var headers = finalResponse.Headers.Concat(finalResponse.Content.Headers)
                .Where(header => header.Key is not ("Transfer-Encoding" or "Content-Length" or "Content-Encoding"))
                .ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
            await route.FulfillAsync(new() { Status = (int)finalResponse.StatusCode, Headers = headers, BodyBytes = await finalResponse.Content.ReadAsByteArrayAsync(ct) });
        });
        foreach (var (width, label) in new[] { (1440, "desktop"), (390, "phone") })
        {
            await page.SetViewportSizeAsync(width, 900);
            await page.GotoAsync("http://localhost/Certificates");
            await page.Locator("[data-certificate-open='decline-certificate']").First.ClickAsync();
            Assert.True(await page.Locator("#decline-certificate").EvaluateAsync<bool>("dialog => dialog.open"));
            Assert.True(await page.Locator("#certificate-decline-reason").EvaluateAsync<bool>("field => field === document.activeElement"));
            Assert.False(await page.Locator("#decline-certificate form").EvaluateAsync<bool>("form => form.checkValidity()"));
            await page.Keyboard.PressAsync("Shift+Tab");
            Assert.True(await page.Locator("#decline-certificate").EvaluateAsync<bool>("dialog => dialog.contains(document.activeElement)"));
            await page.Keyboard.PressAsync("Escape");
            Assert.True(await page.Locator("[data-certificate-open='decline-certificate']").First.EvaluateAsync<bool>("button => button === document.activeElement"));
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), $"attendta-6-12-ledger-{label}.png"), FullPage = true });
        }

        await PostAsync("Decline", registrationIds[3], " ");
        await using (var check = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var pending = await check.Certificates.AsNoTracking()
                .SingleAsync(row => row.RegistrationId == registrationIds[3], ct);
            Assert.Equal(CertificateStatusCode.PendingApproval, pending.CertificateStatusId);
        }
        Assert.Contains("Enter a decline reason", await client.GetStringAsync("/Certificates", ct), StringComparison.Ordinal);
        await page.Locator($"[data-certificate-open='decline-certificate'][data-id='{registrationIds[3]}']").ClickAsync();
        await page.Locator("#certificate-decline-reason").FillAsync("Participation requirements not met.");
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "attendta-6-12-decline-phone.png") });
        await page.Locator("#decline-certificate button[type='submit']").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.True(await page.GetByText("Certificate eligibility declined.", new() { Exact = true }).IsVisibleAsync(),
            page.Url + "\n" + await page.Locator("body").InnerTextAsync());
        await page.SetViewportSizeAsync(1440, 900);
        await page.Locator($"[data-certificate-open='approve-certificate'][data-id='{registrationIds[1]}']").ClickAsync();
        await page.Locator("#approve-certificate button[type='submit']").ClickAsync();
        await page.GetByText("Certificate eligibility approved.", new() { Exact = true }).WaitForAsync();
        await PostAsync("Approve", registrationIds[1]);
        Assert.Contains("already been decided", await client.GetStringAsync("/Certificates", ct), StringComparison.Ordinal);
        await PostAsync("Issue", registrationIds[3]);
        Assert.False(store.IsCertificateIssued(registrationIds[3]));
        await page.Locator($"[data-certificate-open='issue-certificate'][data-id='{registrationIds[1]}']").ClickAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "attendta-6-12-issue-desktop.png") });
        await page.Locator("#issue-certificate button[type='submit']").ClickAsync();
        await page.GetByText("Certificate issued. View it in the Issued Registry.", new() { Exact = true }).WaitForAsync();
        Assert.True(store.IsCertificateIssued(registrationIds[1]));
        var registry = await client.GetStringAsync("/Certificates?tab=issued", ct);
        Assert.Contains("Participant 2", registry, StringComparison.Ordinal);
        var viewer = await client.GetStringAsync($"/Certificates/Viewer/{registrationIds[1]}", ct);
        Assert.Contains("Download PDF", viewer, StringComparison.Ordinal);
        var pdf = await client.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.True(pdf.Headers.CacheControl!.NoStore);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync(ct)), StringComparison.Ordinal);
        var download = await client.GetAsync($"/Certificates/Pdf/{registrationIds[1]}?download=true", ct);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(await pdf.Content.ReadAsByteArrayAsync(ct), await download.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Certificates/Pdf/{registrationIds[3]}", ct)).StatusCode);
        await page.SetViewportSizeAsync(390, 900);
        await page.GotoAsync($"http://localhost/Certificates/Viewer/{registrationIds[1]}");
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "attendta-6-12-certificate-viewer-phone.png"), FullPage = true });
        await page.CloseAsync();
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        // The demo seed writes real attendance and reviews, so the completion hook finds nothing to
        // add and the decisions update the seeded rows in SQL.
        var certificates = await observer.Certificates.AsNoTracking().ToListAsync(ct);
        Assert.Equal(registrationIds.Count, certificates.Count);
        Assert.Equal(CertificateStatusCode.Issued,
            certificates.Single(row => row.RegistrationId == registrationIds[1]).CertificateStatusId);
        Assert.Equal(CertificateStatusCode.NotEligible,
            certificates.Single(row => row.RegistrationId == registrationIds[3]).CertificateStatusId);
        var logs = await observer.ActivityLogs.AsNoTracking().ToListAsync(ct);
        var completion = Assert.Single(logs, row => row.Action == AuditActions.EventCompleted);
        Assert.Equal(eventItem.EventId, completion.EntityId);
        Assert.Single(logs, row => row.Action == AuditActions.CertificateApproved);
        Assert.Single(logs, row => row.Action == AuditActions.CertificateDeclined);
        Assert.False(Directory.Exists(Path.Combine(factory.SetupRoot, "certificates"))
            && Directory.EnumerateFiles(Path.Combine(factory.SetupRoot, "certificates"), "*", SearchOption.AllDirectories).Any());
        Assert.Equal(EventStatusCode.Completed, (await observer.Events.SingleAsync(row => row.EventId == eventItem.EventId, ct)).EventStatusId);

        async Task PostAsync(string action, int id, string reason = "")
        {
            using var response = await client.PostAsync($"/CertificateLedger/{action}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = id.ToString(),
                ["eventId"] = eventItem.EventId.ToString(),
                ["reason"] = reason,
                ["__RequestVerificationToken"] = token
            }), ct);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
    }
}
