using System.Net;
using System.Security.Cryptography;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class CertificateLedgerFlowTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task OfficerCanReviewIssueAndDownloadTheRetainedCertificateWithAnAuditRow()
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
        var memberEmails = new List<string>();
        var memberEmail = "";
        for (var index = 1; index <= 5; index++)
        {
            memberEmail = $"ledger-member-{Guid.NewGuid():N}@attendta.test";
            memberEmails.Add(memberEmail);
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
        await SeedAttendanceAndReviewsAsync(eventItem, officer.Id, registrationIds, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var anonymousLedger = await anonymous.GetAsync("/Certificates", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymousLedger.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymousLedger), StringComparison.Ordinal);
        var anonymousPdf = await anonymous.GetAsync($"/Certificates/Pdf/{registrationIds[0]}", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymousPdf.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymousPdf), StringComparison.Ordinal);
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await MemberEventTestSupport.SignInAsync(memberClient, memberEmail, password, ct);
        Assert.Contains("AccessDenied", MemberEventTestSupport.LocationPath(await memberClient.GetAsync("/Certificates", ct)), StringComparison.Ordinal);
        // The PDF route admits members; this seeded row has no retained file yet, so the not-found
        // comes from the missing document, not an ownership refusal.
        Assert.Equal(HttpStatusCode.NotFound, (await memberClient.GetAsync($"/Certificates/Pdf/{registrationIds[0]}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/CertificateLedger/Approve",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = registrationIds[1].ToString() }), ct)).StatusCode);

        // The test seeds this event as Published with its attendance and reviews; completing it
        // through the real service is what makes it appear in the certificate ledger.
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
        await using (var refused = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var declined = await refused.Certificates.AsNoTracking()
                .SingleAsync(row => row.RegistrationId == registrationIds[3], ct);
            Assert.Equal(CertificateStatusCode.NotEligible, declined.CertificateStatusId);
            Assert.Null(declined.FilePath);
        }
        await page.Locator($"[data-certificate-open='issue-certificate'][data-id='{registrationIds[1]}']").ClickAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "attendta-6-12-issue-desktop.png") });
        await page.Locator("#issue-certificate button[type='submit']").ClickAsync();
        await page.GetByText("Certificate issued. View it in the Issued Registry.", new() { Exact = true }).WaitForAsync();
        await using (var issuedCheck = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var issued = await issuedCheck.Certificates.AsNoTracking()
                .SingleAsync(row => row.RegistrationId == registrationIds[1], ct);
            Assert.Equal(CertificateStatusCode.Issued, issued.CertificateStatusId);
            Assert.StartsWith("certificates/", issued.FilePath, StringComparison.Ordinal);
        }
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
        Assert.Equal($"AT-2026-{registrationIds[1]:D6}.pdf", download.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        Assert.Equal(await pdf.Content.ReadAsByteArrayAsync(ct), await download.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Certificates/Pdf/{registrationIds[3]}", ct)).StatusCode);

        // The seeded issued row has no retained file, so the officer viewer and PDF are a
        // controlled not-found.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Certificates/Viewer/{registrationIds[0]}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Certificates/Pdf/{registrationIds[0]}", ct)).StatusCode);

        // The member history was handed over to the real issued row and its retained file.
        using var ownerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await MemberEventTestSupport.SignInAsync(ownerClient, memberEmails[1], password, ct);
        var memberHistory = await ownerClient.GetStringAsync("/MyEvents/Certificates", ct);
        Assert.Contains($"AT-2026-{registrationIds[1]:D6}", memberHistory, StringComparison.Ordinal);
        var memberPdf = await ownerClient.GetAsync($"/MyEvents/Certificates/{registrationIds[1]}/Pdf", ct);
        Assert.Equal(HttpStatusCode.OK, memberPdf.StatusCode);
        Assert.StartsWith("%PDF-",
            System.Text.Encoding.ASCII.GetString(await memberPdf.Content.ReadAsByteArrayAsync(ct)), StringComparison.Ordinal);

        // The shared Pdf route admits the owner: same bytes as the officer read, and download=true
        // is an attachment named with the certificate number.
        var ownerPdf = await ownerClient.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct);
        Assert.Equal(HttpStatusCode.OK, ownerPdf.StatusCode);
        Assert.Equal("application/pdf", ownerPdf.Content.Headers.ContentType!.MediaType);
        Assert.True(ownerPdf.Headers.CacheControl!.NoStore);
        Assert.Equal(await pdf.Content.ReadAsByteArrayAsync(ct), await ownerPdf.Content.ReadAsByteArrayAsync(ct));
        var ownerDownload = await ownerClient.GetAsync($"/Certificates/Pdf/{registrationIds[1]}?download=true", ct);
        Assert.Equal("attachment", ownerDownload.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal($"AT-2026-{registrationIds[1]:D6}.pdf", ownerDownload.Content.Headers.ContentDisposition.FileName!.Trim('"'));

        // An inactive membership does not block the owner's download (R6).
        await using (var membership = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            var owner = await membership.Users.SingleAsync(row => row.UserId == memberEmails[1], ct);
            (await membership.Members.SingleAsync(row => row.UserId == owner.Id, ct)).MembershipStatusId = MembershipStatusCode.Inactive;
            await membership.SaveChangesAsync(ct);
        }
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct)).StatusCode);

        // A non-owner member and an unknown id receive the identical not-found: same status, same
        // message, and the same page once the id in the retry link is normalised.
        var nonOwner = await memberClient.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct);
        var unknown = await memberClient.GetAsync("/Certificates/Pdf/2147483647", ct);
        Assert.Equal(HttpStatusCode.NotFound, nonOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var nonOwnerBody = await nonOwner.Content.ReadAsStringAsync(ct);
        var unknownBody = await unknown.Content.ReadAsStringAsync(ct);

        // Compare the alert block itself: same message, same structure, and the only difference is
        // the id in the retry link.
        static string AlertBlock(string html) =>
            System.Text.RegularExpressions.Regex.Match(
                html,
                "<div class=\"notice notice-error\" role=\"alert\">.*?</div>",
                System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        var nonOwnerAlert = AlertBlock(nonOwnerBody).Replace(
            $"/Certificates/Pdf/{registrationIds[1]}\"", "/Certificates/Pdf/{id}\"", StringComparison.Ordinal);
        var unknownAlert = AlertBlock(unknownBody).Replace(
            "/Certificates/Pdf/2147483647\"", "/Certificates/Pdf/{id}\"", StringComparison.Ordinal);
        Assert.Contains("Certificate not found.", nonOwnerAlert, StringComparison.Ordinal);
        Assert.Equal(nonOwnerAlert, unknownAlert);

        // The viewer stays Management-only: a member never reaches the metadata page.
        var memberViewer = await memberClient.GetAsync($"/Certificates/Viewer/{registrationIds[1]}", ct);
        Assert.Equal(HttpStatusCode.Redirect, memberViewer.StatusCode);
        Assert.Contains("AccessDenied", MemberEventTestSupport.LocationPath(memberViewer), StringComparison.Ordinal);

        await page.SetViewportSizeAsync(390, 900);
        await page.GotoAsync($"http://localhost/Certificates/Viewer/{registrationIds[1]}");
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), "attendta-6-12-certificate-viewer-phone.png"), FullPage = true });
        await page.CloseAsync();
        await using var observer = MemberEventTestSupport.Context(fixture.ConnectionString);
        // The seeded reviews already exist, so the completion hook finds nothing to add and the
        // decisions update the seeded rows in SQL.
        var certificates = await observer.Certificates.AsNoTracking().ToListAsync(ct);
        Assert.Equal(registrationIds.Count, certificates.Count);
        var issuedCertificate = certificates.Single(row => row.RegistrationId == registrationIds[1]);
        Assert.Equal(CertificateStatusCode.Issued, issuedCertificate.CertificateStatusId);
        Assert.StartsWith("certificates/", issuedCertificate.FilePath, StringComparison.Ordinal);
        Assert.Equal(CertificateStatusCode.NotEligible,
            certificates.Single(row => row.RegistrationId == registrationIds[3]).CertificateStatusId);
        var logs = await observer.ActivityLogs.AsNoTracking().ToListAsync(ct);
        var completion = Assert.Single(logs, row => row.Action == AuditActions.EventCompleted);
        Assert.Equal(eventItem.EventId, completion.EntityId);
        Assert.Single(logs, row => row.Action == AuditActions.CertificateApproved);
        Assert.Single(logs, row => row.Action == AuditActions.CertificateDeclined);
        var issuance = Assert.Single(logs, row => row.Action == AuditActions.CertificateIssued);
        Assert.Equal(issuedCertificate.CertificateId, issuance.EntityId);
        Assert.DoesNotContain("path", issuance.NewValues!, StringComparison.OrdinalIgnoreCase);
        var certificateDirectory = Path.Combine(factory.SetupRoot, "certificates");
        var certificateFiles = Directory.Exists(certificateDirectory)
            ? Directory.GetFiles(certificateDirectory, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();
        var retained = Assert.Single(certificateFiles);
        var retainedBytes = await File.ReadAllBytesAsync(retained, ct);
        Assert.StartsWith("%PDF-",
            System.Text.Encoding.ASCII.GetString(retainedBytes), StringComparison.Ordinal);

        // The served bytes are the retained file's bytes, not a re-render: both reads match the
        // file on disk exactly.
        Assert.Equal(retainedBytes, await pdf.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal(retainedBytes, await ownerPdf.Content.ReadAsByteArrayAsync(ct));

        // A stored key outside certificates/ is refused by the service before the store is asked.
        // The venue file exists on purpose: OpenReadAsync infers the area from the key, so without
        // the prefix refusal these PNG bytes would be streamed as application/pdf.
        var venueImage = Path.Combine(factory.SetupRoot, "uploads", "venues", "not-a-certificate.png");
        Directory.CreateDirectory(Path.GetDirectoryName(venueImage)!);
        await File.WriteAllBytesAsync(venueImage, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], ct);
        await using (var tampered = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            (await tampered.Certificates.SingleAsync(row => row.RegistrationId == registrationIds[1], ct)).FilePath =
                "uploads/venues/not-a-certificate.png";
            await tampered.SaveChangesAsync(ct);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Certificates/Viewer/{registrationIds[1]}", ct)).StatusCode);
        await using (var restored = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            (await restored.Certificates.SingleAsync(row => row.RegistrationId == registrationIds[1], ct)).FilePath =
                issuedCertificate.FilePath;
            await restored.SaveChangesAsync(ct);
        }
        File.Delete(venueImage);

        // A retained file missing on disk is a controlled not-found, not an unhandled 500.
        File.Delete(retained);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.GetAsync($"/MyEvents/Certificates/{registrationIds[1]}/Pdf", ct)).StatusCode);
        Assert.Equal(EventStatusCode.Completed, (await observer.Events.SingleAsync(row => row.EventId == eventItem.EventId, ct)).EventStatusId);

        // An inactive account is refused by the AD-8 cookie check: the next request is a sign-in redirect.
        await using (var account = MemberEventTestSupport.Context(fixture.ConnectionString))
        {
            (await account.Users.SingleAsync(row => row.UserId == memberEmails[1], ct)).IsActive = false;
            await account.SaveChangesAsync(ct);
        }
        var inactiveAccountPdf = await ownerClient.GetAsync($"/Certificates/Pdf/{registrationIds[1]}", ct);
        Assert.Equal(HttpStatusCode.Redirect, inactiveAccountPdf.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(inactiveAccountPdf), StringComparison.Ordinal);

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

    /// <summary>
    /// Seeds the attendance and certificate-review rows the ledger reads, through the real
    /// repositories. The last attendee's check-in is invalidated, the third valid attendee's review
    /// is declined and the first valid attendee's review is issued without a retained file, so the
    /// officer ledger opens with every state the flow exercises.
    /// </summary>
    private async Task SeedAttendanceAndReviewsAsync(
        Event eventItem,
        int officerId,
        IReadOnlyList<int> registrationIds,
        CancellationToken cancellationToken)
    {
        const string invalidatedReason = "Pass could not be verified at the door.";
        const string declinedReason = "Attendance did not meet the participation requirement.";

        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var unitOfWork = new UnitOfWork(context);
        var attendance = new AttendanceRepository(unitOfWork);
        var certificates = new CertificateRepository(unitOfWork);

        var invalidatedRegistrationId = registrationIds[^1];
        var attendanceByRegistration = new Dictionary<int, Attendance>();
        for (var index = 0; index < registrationIds.Count; index++)
        {
            var registrationId = registrationIds[index];
            var row = new Attendance
            {
                RegistrationId = registrationId,
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = officerId,
                RecordedAt = eventItem.StartsAt.AddMinutes(5 + index)
            };
            if (registrationId == invalidatedRegistrationId)
            {
                row.InvalidatedBy = officerId;
                row.InvalidatedAt = eventItem.StartsAt.AddHours(1);
                row.InvalidationReason = invalidatedReason;
            }

            await attendance.AddAsync(row, cancellationToken);
            attendanceByRegistration[registrationId] = row;
        }

        var staged = new List<Certificate>(registrationIds.Count);
        var known = new Dictionary<int, Certificate>(registrationIds.Count);
        foreach (var registrationId in registrationIds)
        {
            var certificate = new Certificate
            {
                RegistrationId = registrationId,
                CertificateStatusId = CertificateStatusCode.PendingApproval,
                CreatedBy = officerId,
                CreatedAt = eventItem.StartsAt.AddHours(2)
            };
            staged.Add(certificate);
            known[registrationId] = certificate;
        }

        var invalidated = known[invalidatedRegistrationId];
        invalidated.CertificateStatusId = CertificateStatusCode.NotEligible;
        invalidated.ReviewNotes = invalidatedReason;
        invalidated.UpdatedBy = officerId;
        invalidated.UpdatedAt = eventItem.StartsAt.AddHours(1);

        var valid = registrationIds.Where(id => attendanceByRegistration[id].InvalidatedAt is null).ToList();
        var declined = known[valid[2]];
        declined.CertificateStatusId = CertificateStatusCode.NotEligible;
        declined.ReviewNotes = declinedReason;
        declined.ReviewedBy = officerId;
        declined.ReviewedAt = eventItem.StartsAt.AddHours(3);
        declined.UpdatedBy = officerId;
        declined.UpdatedAt = eventItem.StartsAt.AddHours(3);

        var issued = known[valid[0]];
        var issuedAt = eventItem.StartsAt.AddDays(1);
        issued.CertificateStatusId = CertificateStatusCode.Issued;
        issued.CertificateNumber = $"AT-{issuedAt:yyyy}-{valid[0]:D6}";
        issued.RecipientNameSnapshot = "Participant 1";
        issued.EventTitleSnapshot = eventItem.Title;
        issued.EventStartsAtSnapshot = eventItem.StartsAt;
        issued.ReviewedBy = officerId;
        issued.ReviewedAt = eventItem.StartsAt.AddHours(2);
        issued.IssuedBy = officerId;
        issued.IssuedAt = issuedAt;
        issued.UpdatedBy = officerId;
        issued.UpdatedAt = issuedAt;

        await certificates.AddRangeAsync(staged, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
