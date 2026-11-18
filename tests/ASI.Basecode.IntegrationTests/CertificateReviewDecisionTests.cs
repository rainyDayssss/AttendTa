using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Focused SQL coverage for 6.6: the real review queue, both decisions, the single audit entry, every
/// refusal in the ticket matrix, and in-memory paging at five. The DI check also asserts that the real
/// ledger and the real member history resolve from the container.
/// </summary>
public sealed class CertificateReviewDecisionTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ApproveMarksEligibleWithReviewerAndTimeAndWritesOneAuditEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        await service.DecideAsync(registrationId, true, null, ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.Eligible, certificate.CertificateStatusId);
        Assert.Equal(setup.OfficerId, certificate.ReviewedBy);
        Assert.Equal(Now, certificate.ReviewedAt);
        Assert.Null(certificate.ReviewNotes);

        var log = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(row => row.EntityType == "certificates" && row.EntityId == certificateId, ct);
        Assert.Equal(AuditActions.CertificateApproved, log.Action);
        Assert.Equal(setup.OfficerId, log.ActorUserId);
        Assert.Null(log.Reason);
        Assert.DoesNotContain("token", log.NewValues!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeclineMarksNotEligibleWithTheTrimmedReasonAndWritesOneAuditEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        await service.DecideAsync(registrationId, false, "  Participation requirements not met.  ", ct);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(CertificateStatusCode.NotEligible, certificate.CertificateStatusId);
        Assert.Equal("Participation requirements not met.", certificate.ReviewNotes);
        Assert.Equal(setup.OfficerId, certificate.ReviewedBy);
        Assert.Equal(Now, certificate.ReviewedAt);

        var log = await observer.ActivityLogs.AsNoTracking()
            .SingleAsync(row => row.EntityType == "certificates" && row.EntityId == certificateId, ct);
        Assert.Equal(AuditActions.CertificateDeclined, log.Action);
        Assert.Equal("Participation requirements not met.", log.Reason);
    }

    [Fact]
    public async Task BlankReasonLeavesTheReviewUnchangedAndNamesTheReasonField()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, false, "   ", ct));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal("reason", exception.Field);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Theory]
    [InlineData(CertificateStatusCode.Eligible)]
    [InlineData(CertificateStatusCode.NotEligible)]
    [InlineData(CertificateStatusCode.Issued)]
    public async Task AlreadyDecidedReviewIsStaleAndUnchanged(CertificateStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(reviewStatus: status);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, true, null, ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        await AssertUnchangedAsync(certificateId, status);
    }

    [Fact]
    public async Task DecidedReviewWithBlankDeclineReasonIsStaleNotInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(reviewStatus: CertificateStatusCode.Eligible);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, false, "   ", ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.Eligible);
    }

    [Fact]
    public async Task InvalidatedAttendanceIsStaleAndUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(invalidateAttendance: true);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, false, "Not eligible.", ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Fact]
    public async Task InvalidatedAttendanceWithBlankDeclineReasonIsStaleNotInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(invalidateAttendance: true);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        // The certificate rule runs before the decline reason, so an undecidable review reports the
        // stale fact instead of asking for a reason the officer could never submit.
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, false, "   ", ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        Assert.Null(exception.Field);
        Assert.Contains("attendance is no longer valid", exception.Message, StringComparison.Ordinal);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Fact]
    public async Task LostSeatIsStaleWithTheChangedMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(cancelRegistration: true);
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        // Approving a review whose seat was cancelled refuses as stale, and the message names the lost
        // seat rather than the attendance, which is still valid.
        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, true, string.Empty, ct));

        Assert.Equal(OutcomeCode.Stale, exception.Outcome);
        Assert.Contains("details changed", exception.Message, StringComparison.Ordinal);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Fact]
    public async Task UnknownReviewIsNotFoundAndWritesNoAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(seedReview: false);
        var registrationId = setup.RegistrationIds[0];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);
        var before = await CertificateAuditCountAsync(ct);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, true, null, ct));

        Assert.Equal(OutcomeCode.NotFound, exception.Outcome);
        Assert.Equal(before, await CertificateAuditCountAsync(ct));
    }

    [Fact]
    public async Task MemberCallerIsForbiddenAndWritesNoAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync();
        var registrationId = setup.RegistrationIds[0];
        var certificateId = setup.CertificateIds[registrationId];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Member);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.DecideAsync(registrationId, true, null, ct));

        Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
        await AssertUnchangedAsync(certificateId, CertificateStatusCode.PendingApproval);
    }

    [Fact]
    public async Task QueueShowsFiveRowsAndClampsAPagePastTheEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(confirmedCount: 28);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var first = await service.GetPageAsync(setup.EventId, CertificateLedgerTab.Eligibility, 1, ct);
        Assert.Equal(28, first.EligibilityCount);
        Assert.Equal(0, first.IssuedCount);
        Assert.Equal(5, first.Rows.Count);
        Assert.Equal(1, first.Page.PageNumber);
        Assert.Equal(28, first.Page.TotalCount);
        Assert.True(first.Page.HasNextPage);
        Assert.Equal(setup.EventId, first.Selector.SelectedEventId);

        var last = await service.GetPageAsync(setup.EventId, CertificateLedgerTab.Eligibility, int.MaxValue, ct);
        Assert.Equal(3, last.Rows.Count);
        Assert.Equal(6, last.Page.PageNumber);
        Assert.False(last.Page.HasNextPage);

        var issued = await service.GetPageAsync(setup.EventId, CertificateLedgerTab.Issued, 1, ct);
        Assert.Empty(issued.Rows);
        Assert.Equal(0, issued.IssuedCount);
        Assert.Equal(28, issued.EligibilityCount);
    }

    [Fact]
    public void ApplicationRegistersTheRealLedgerAndMemberHistory()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using (var scope = factory.Services.CreateScope())
        {
            Assert.IsType<CertificateLedgerService>(
                scope.ServiceProvider.GetRequiredService<ICertificateLedgerService>());
            Assert.IsType<MyCertificatesService>(
                scope.ServiceProvider.GetRequiredService<IMyCertificatesService>());
        }
    }

    [Fact]
    public async Task LedgerBulkPanelKeepsTwentyFiveMixedAndStaleResultsInServerOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var statuses = Enumerable.Range(0, 25)
            .Select(index => index % 5 == 0 ? CertificateStatusCode.NotEligible : CertificateStatusCode.PendingApproval)
            .ToArray();
        var setup = await SeedAsync(reviewStatuses: statuses);
        var password = MemberEventTestSupport.NewPassword();
        var email = $"bulk-ledger-{Guid.NewGuid():N}@attendta.test";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, email, password, RoleCode.Officer, ct);

        // Reverse the submission order and record the identity associated with each id. The service
        // reports distinct IDs in first-seen order, not database or alphabetical order.
        var ids = setup.RegistrationIds.Reverse().ToArray();
        Dictionary<int, string> identities;
        await using (var context = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            identities = await (from registration in context.Registrations
                                join member in context.Members on registration.MemberId equals member.MemberId
                                where ids.Contains(registration.RegistrationId)
                                select new { registration.RegistrationId, member.StudentNumber })
                .ToDictionaryAsync(row => row.RegistrationId, row => row.StudentNumber, ct);
        }

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(client, email, password, ct);
        var token = await MemberEventTestSupport.AntiforgeryTokenAsync(client, $"/Certificates?eventId={setup.EventId}", ct);

        async Task<string> SubmitAsync()
        {
            var fields = ids.Select(id => new KeyValuePair<string, string>("ids", id.ToString())).ToList();
            fields.Add(new("eventId", setup.EventId.ToString()));
            fields.Add(new("tab", "eligibility"));
            fields.Add(new("__RequestVerificationToken", token));
            using var response = await client.PostAsync("/CertificateLedger/ApproveSelected", new FormUrlEncodedContent(fields), ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync(ct);
        }

        void AssertRows(string html, bool allFailed)
        {
            var section = Regex.Match(html, "<section class=\"bulk-result\"[^>]*>(.*?)</section>", RegexOptions.Singleline);
            Assert.True(section.Success);
            Assert.DoesNotContain("role=\"status\"", section.Value, StringComparison.Ordinal);
            Assert.Contains(allFailed ? "0 of 25 succeeded; 25 failed" : "20 of 25 succeeded; 5 failed", section.Value, StringComparison.Ordinal);
            var details = Regex.Match(section.Value, "<details class=\"bulk-result-details\">(.*?)</details>", RegexOptions.Singleline);
            Assert.True(details.Success); // No open attribute: collapsed, native, and script-independent.
            var visibleSummary = section.Value[..details.Index];
            Assert.Contains(allFailed ? "0 of 25 succeeded; 25 failed" : "20 of 25 succeeded; 5 failed", visibleSummary, StringComparison.Ordinal);
            Assert.Contains("<summary>View all 25 results</summary>", details.Value, StringComparison.Ordinal);
            var rows = Regex.Matches(details.Value, "<li class=\"bulk-result-item\">(.*?)</li>", RegexOptions.Singleline);
            Assert.Equal(25, rows.Count);
            for (var index = 0; index < ids.Length; index++)
            {
                var row = WebUtility.HtmlDecode(rows[index].Value);
                Assert.Contains("Member event Member", row, StringComparison.Ordinal);
                Assert.Contains(identities[ids[index]], row, StringComparison.Ordinal);
                var failed = allFailed || statuses[Array.IndexOf(setup.RegistrationIds.ToArray(), ids[index])] == CertificateStatusCode.NotEligible;
                Assert.Contains(failed ? "Not approved" : "Approved", row, StringComparison.Ordinal);
                Assert.Contains(failed ? "This review has already been decided." : "Approved.", row, StringComparison.Ordinal);
            }
        }

        var mixedHtml = await SubmitAsync();
        AssertRows(mixedHtml, allFailed: false);
        AssertRows(await SubmitAsync(), allFailed: true);

        // Exercise the actual server-rendered panel with JavaScript disabled and keyboard only.
        var panel = Regex.Match(mixedHtml, "<section class=\"bulk-result\"[^>]*>.*?</section>", RegexOptions.Singleline).Value;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chromium" });
        await using var browserContext = await browser.NewContextAsync(new() { JavaScriptEnabled = false });
        var page = await browserContext.NewPageAsync();
        await page.SetContentAsync(panel);
        var detailsControl = page.Locator(".bulk-result-details");
        var rows = page.Locator(".bulk-result-item");
        Assert.False(await detailsControl.EvaluateAsync<bool>("element => element.open"));
        Assert.False(await rows.First.IsVisibleAsync());
        await page.Keyboard.PressAsync("Tab");
        Assert.True(await page.Locator("summary").EvaluateAsync<bool>("element => element === document.activeElement"));
        await page.Keyboard.PressAsync("Enter");
        Assert.True(await detailsControl.EvaluateAsync<bool>("element => element.open"));
        Assert.Equal(25, await rows.CountAsync());
        Assert.True(await rows.Last.IsVisibleAsync());
    }

    [Fact]
    public async Task BulkApprovalApprovesPendingReviewsAndRefusesTheDeclinedOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(reviewStatuses:
        [
            CertificateStatusCode.PendingApproval,
            CertificateStatusCode.PendingApproval,
            CertificateStatusCode.PendingApproval,
            CertificateStatusCode.NotEligible
        ]);
        var declinedId = setup.RegistrationIds[3];
        var declinedCertificateId = setup.CertificateIds[declinedId];
        var declinedReviewedAt = Now.AddDays(-1);

        // The declined row already carries a reviewer, a time and a reason; the batch must not touch them.
        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Certificates.SingleAsync(item => item.CertificateId == declinedCertificateId, ct);
            row.ReviewedBy = setup.OfficerId;
            row.ReviewedAt = declinedReviewedAt;
            row.ReviewNotes = "Not eligible.";
            await writer.SaveChangesAsync(ct);
        }

        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var outcomes = await service.ApproveManyAsync(setup.RegistrationIds, ct);

        Assert.Equal(4, outcomes.Count);
        Assert.Equal(3, outcomes.Count(outcome => outcome.Succeeded));
        var refused = outcomes.Single(outcome => !outcome.Succeeded);
        Assert.Equal(declinedId, refused.RegistrationId);
        Assert.Equal("This review has already been decided.", refused.Message);
        Assert.False(string.IsNullOrWhiteSpace(refused.MemberName));
        Assert.False(string.IsNullOrWhiteSpace(refused.StudentNumber));

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        foreach (var registrationId in setup.RegistrationIds.Take(3))
        {
            var certificate = await observer.Certificates.AsNoTracking()
                .SingleAsync(row => row.CertificateId == setup.CertificateIds[registrationId], ct);
            Assert.Equal(CertificateStatusCode.Eligible, certificate.CertificateStatusId);
            Assert.Equal(setup.OfficerId, certificate.ReviewedBy);
            Assert.Equal(Now, certificate.ReviewedAt);
            Assert.Null(certificate.ReviewNotes);
            Assert.Equal(1, await observer.ActivityLogs.AsNoTracking()
                .CountAsync(row => row.EntityType == "certificates"
                                   && row.EntityId == certificate.CertificateId
                                   && row.Action == AuditActions.CertificateApproved, ct));
        }

        var declined = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == declinedCertificateId, ct);
        Assert.Equal(CertificateStatusCode.NotEligible, declined.CertificateStatusId);
        Assert.Equal(setup.OfficerId, declined.ReviewedBy);
        Assert.Equal(declinedReviewedAt, declined.ReviewedAt);
        Assert.Equal("Not eligible.", declined.ReviewNotes);
        Assert.Equal(0, await AuditCountForAsync(declinedCertificateId, ct));
    }

    [Theory]
    [InlineData(CertificateStatusCode.Issued)]
    [InlineData(CertificateStatusCode.NotEligible)]
    public async Task BulkApprovalRefusesAnAlreadyDecidedReviewAndLeavesItUnchanged(CertificateStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(reviewStatuses: [CertificateStatusCode.PendingApproval, status]);
        var pendingId = setup.RegistrationIds[0];
        var decidedId = setup.RegistrationIds[1];
        var decidedCertificateId = setup.CertificateIds[decidedId];
        var decidedReviewedAt = Now.AddDays(-1);

        await using (var writer = EventDraftTestSupport.Context(fixture.ConnectionString))
        {
            var row = await writer.Certificates.SingleAsync(item => item.CertificateId == decidedCertificateId, ct);
            row.ReviewedBy = setup.OfficerId;
            row.ReviewedAt = decidedReviewedAt;
            row.ReviewNotes = "Decided earlier.";
            await writer.SaveChangesAsync(ct);
        }

        var auditBefore = await AuditCountForAsync(decidedCertificateId, ct);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var outcomes = await service.ApproveManyAsync(setup.RegistrationIds, ct);

        Assert.Equal(2, outcomes.Count);
        Assert.True(outcomes.Single(outcome => outcome.RegistrationId == pendingId).Succeeded);
        var refused = outcomes.Single(outcome => outcome.RegistrationId == decidedId);
        Assert.False(refused.Succeeded);
        Assert.Equal("This review has already been decided.", refused.Message);

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var decided = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == decidedCertificateId, ct);
        Assert.Equal(status, decided.CertificateStatusId);
        Assert.Equal(setup.OfficerId, decided.ReviewedBy);
        Assert.Equal(decidedReviewedAt, decided.ReviewedAt);
        Assert.Equal("Decided earlier.", decided.ReviewNotes);
        Assert.Equal(auditBefore, await AuditCountForAsync(decidedCertificateId, ct));
    }

    [Fact]
    public async Task BulkApprovalDecidesADuplicateIdOnceAndWritesOneAuditEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(reviewStatuses: [CertificateStatusCode.PendingApproval, CertificateStatusCode.PendingApproval]);
        var duplicatedId = setup.RegistrationIds[0];
        var otherId = setup.RegistrationIds[1];
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var outcomes = await service.ApproveManyAsync(new[] { duplicatedId, duplicatedId, otherId }, ct);

        Assert.Equal(2, outcomes.Count);
        Assert.Equal(new[] { duplicatedId, otherId }, outcomes.Select(outcome => outcome.RegistrationId).ToArray());
        Assert.All(outcomes, outcome => Assert.True(outcome.Succeeded));
        Assert.Equal(1, await AuditCountForAsync(setup.CertificateIds[duplicatedId], ct));

        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == setup.CertificateIds[duplicatedId], ct);
        Assert.Equal(CertificateStatusCode.Eligible, certificate.CertificateStatusId);
    }

    [Fact]
    public async Task BulkApprovalIssuesNoCertificateAndWritesNoFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(confirmedCount: 2);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var outcomes = await service.ApproveManyAsync(setup.RegistrationIds, ct);

        Assert.All(outcomes, outcome => Assert.True(outcome.Succeeded));
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificateIds = setup.CertificateIds.Values.ToList();
        var rows = await observer.Certificates.AsNoTracking()
            .Where(row => certificateIds.Contains(row.CertificateId))
            .ToListAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.NotEqual(CertificateStatusCode.Issued, row.CertificateStatusId);
            Assert.Null(row.FilePath);
            Assert.Null(row.CertificateNumber);
            Assert.Null(row.IssuedAt);
        });
        // The harness's renderer and file store throw on any use, so a successful batch proves the
        // approval path never rendered or wrote a PDF.
    }

    [Fact]
    public async Task BulkApprovalRefusesInvalidatedAttendancePerRecordAndLeavesTheReviewsPending()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await SeedAsync(confirmedCount: 2, invalidateAttendance: true);
        await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
        var service = Service(context, setup.OfficerId, RoleCodes.Officer);

        var outcomes = await service.ApproveManyAsync(setup.RegistrationIds, ct);

        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, outcome =>
        {
            Assert.False(outcome.Succeeded);
            Assert.Contains("attendance is no longer valid", outcome.Message, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(outcome.MemberName));
        });
        foreach (var registrationId in setup.RegistrationIds)
        {
            await AssertUnchangedAsync(setup.CertificateIds[registrationId], CertificateStatusCode.PendingApproval);
            Assert.Equal(0, await AuditCountForAsync(setup.CertificateIds[registrationId], ct));
        }
    }

    private static CertificateLedgerService Service(
        AsiBasecodeDBContext context,
        int actorId,
        string role)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorId, role);
        var clock = new FixedClock(Now);
        return new CertificateLedgerService(
            new EventRepository(unitOfWork),
            new CertificateRepository(unitOfWork),
            new RegistrationRepository(unitOfWork),
            new AttendanceRepository(unitOfWork),
            new UserRepository(unitOfWork),
            unitOfWork,
            actor,
            new ActivityLogWriter(context, actor, clock),
            clock,
            new UnusedPdfRenderer(),
            new UnusedPrivateFileStore());
    }

    private async Task<Setup> SeedAsync(
        int confirmedCount = 1,
        CertificateStatusCode? reviewStatus = CertificateStatusCode.PendingApproval,
        bool invalidateAttendance = false,
        bool seedReview = true,
        bool cancelRegistration = false,
        IReadOnlyList<CertificateStatusCode>? reviewStatuses = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var officerId = await EventDraftTestSupport.SeedActorAsync(fixture.ConnectionString, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officerId, ct);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString,
            officerId,
            typeId,
            venueId,
            EventStatusCode.Completed,
            Now.AddDays(-2),
            Now.AddDays(-3),
            50,
            null,
            $"Certificate review {Guid.NewGuid():N}",
            ct);

        var registrationIds = new List<int>();
        var certificateIds = new Dictionary<int, int>();
        // A per-record status list carries its own count, so a mixed batch seeds exactly the rows it
        // describes; otherwise every registration shares the single review status.
        var total = reviewStatuses?.Count ?? confirmedCount;
        for (var index = 0; index < total; index++)
        {
            var user = await MemberEventTestSupport.SeedUserAsync(
                fixture.ConnectionString,
                $"review-{Guid.NewGuid():N}@attendta.test",
                MemberEventTestSupport.NewPassword(),
                RoleCode.Member,
                ct);
            var member = await MemberEventTestSupport.SeedMemberAsync(
                fixture.ConnectionString, user.Id, MembershipStatusCode.Active, ct);
            var registration = await MemberEventTestSupport.SeedRegistrationAsync(
                fixture.ConnectionString,
                eventItem.EventId,
                member.MemberId,
                RegistrationStatusCode.Confirmed,
                officerId,
                ct);
            registrationIds.Add(registration.RegistrationId);

            if (cancelRegistration)
            {
                // A seat lost after the review exists: the reviewer cancelled the registration, so the
                // review can no longer be decided.
                await using var canceller = EventDraftTestSupport.Context(fixture.ConnectionString);
                var row = await canceller.Registrations
                    .SingleAsync(item => item.RegistrationId == registration.RegistrationId, ct);
                row.RegistrationStatusId = RegistrationStatusCode.Cancelled;
                await canceller.SaveChangesAsync(ct);
            }

            await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
            writer.AttendanceRecords.Add(new Attendance
            {
                RegistrationId = registration.RegistrationId,
                CheckInMethodId = CheckInMethodCode.QrCode,
                RecordedBy = officerId,
                RecordedAt = Now.AddDays(-2),
                InvalidatedBy = invalidateAttendance ? officerId : null,
                InvalidatedAt = invalidateAttendance ? Now.AddHours(-1) : null,
                InvalidationReason = invalidateAttendance ? "Correction." : null
            });
            if (seedReview)
            {
                var certificate = new Certificate
                {
                    RegistrationId = registration.RegistrationId,
                    CertificateStatusId = reviewStatuses is not null && index < reviewStatuses.Count
                        ? reviewStatuses[index]
                        : reviewStatus!.Value,
                    CreatedBy = officerId,
                    CreatedAt = Now.AddDays(-2)
                };
                writer.Certificates.Add(certificate);
                await writer.SaveChangesAsync(ct);
                certificateIds[registration.RegistrationId] = certificate.CertificateId;
            }
            else
            {
                await writer.SaveChangesAsync(ct);
            }
        }

        return new Setup(officerId, eventItem.EventId, registrationIds, certificateIds);
    }

    private async Task<int> CertificateAuditCountAsync(CancellationToken cancellationToken)
    {
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        return await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.Action == AuditActions.CertificateApproved
                               || row.Action == AuditActions.CertificateDeclined, cancellationToken);
    }

    /// <summary>Counts every audit entry written against one certificate row.</summary>
    private async Task<int> AuditCountForAsync(int certificateId, CancellationToken cancellationToken)
    {
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        return await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "certificates" && row.EntityId == certificateId, cancellationToken);
    }

    private async Task AssertUnchangedAsync(int certificateId, CertificateStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(expected, certificate.CertificateStatusId);
        Assert.Null(certificate.ReviewedBy);
        Assert.Null(certificate.ReviewedAt);
        Assert.Equal(0, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "certificates" && row.EntityId == certificateId, ct));
    }

    private sealed record Setup(
        int OfficerId,
        int EventId,
        IReadOnlyList<int> RegistrationIds,
        IReadOnlyDictionary<int, int> CertificateIds);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }

    private sealed class Actor(int userId, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => userId;
        public string Role => role;
    }

    private sealed class UnusedPdfRenderer : ICertificatePdfRenderer
    {
        public Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedPrivateFileStore : IPrivateFileStore
    {
        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();
        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Delete(string relativePath) => throw new NotSupportedException();
    }
}
