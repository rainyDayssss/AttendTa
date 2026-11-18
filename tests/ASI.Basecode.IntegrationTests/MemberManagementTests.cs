using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.IntegrationTests.Support;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using ASI.Basecode.WebApp.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class MemberManagementTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private const string ManagementPassword = "MemberManagement#2026";
    private const string MemberPassword = "MemberProfile#2026";

    private AsiBasecodeDBContext Context(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString)
            .AddInterceptors(interceptors).Options);

    [Theory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(1440, false)]
    public async Task MemberEditorSavesAndDisplaysValidationErrorsInBrowser(int width, bool changeFocusBeforeLoad)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct, withLoginPasswords: true);
        await using (var writer = Context())
        {
            var member = await writer.Members.SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            member.Course = "BSIT";
            member.MobileNumber = "09171234567";
            await writer.SaveChangesAsync(ct);
        }
        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = width, Height = 700 } });
        var delayedImageStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseImage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayReloadImage = false;
        await page.RouteAsync("**/*", async route =>
        {
            if (delayReloadImage && route.Request.Url.Contains("/img/attendta-logo.jpg", StringComparison.OrdinalIgnoreCase))
            {
                delayedImageStarted.TrySetResult();
                await releaseImage.Task;
            }
            using var request = new HttpRequestMessage(new HttpMethod(route.Request.Method), route.Request.Url);
            if (route.Request.Headers.TryGetValue("x-record-dialog", out var dialogHeader))
                request.Headers.Add("X-Record-Dialog", dialogHeader);
            if (route.Request.PostDataBuffer is byte[] body)
            {
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", route.Request.Headers["content-type"]);
            }
            using var response = await client.SendAsync(request, ct);
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(header => header.Key is not ("Transfer-Encoding" or "Content-Length" or "Content-Encoding"))
                .ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
            await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = await response.Content.ReadAsByteArrayAsync(ct) });
        });
        await page.GotoAsync($"http://localhost/Member?search={Uri.EscapeDataString(seeded.MemberEmail)}");
        async Task OpenEditor()
        {
            await page.Locator($"[data-record-open='edit-member-{seeded.MemberId}']").ClickAsync();
            await page.Locator("#record-dialog #FullName").WaitForAsync();
        }
        await OpenEditor();
        Assert.Equal(0, await page.Locator("#record-dialog [data-record-conflict]").CountAsync());
        delayReloadImage = width == 390;
        var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFrameNavigated(object? sender, IFrame frame)
        {
            if (frame == page.MainFrame) reloaded.TrySetResult();
        }
        page.FrameNavigated += OnFrameNavigated;
        try
        {
            await page.Locator("#record-dialog button[type='submit']").ClickAsync();
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            if (delayReloadImage)
            {
                await delayedImageStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
                await page.WaitForFunctionAsync("() => document.querySelector('[data-member-edit-unchanged]') && document.activeElement?.matches('[data-record-open].record-result-focus')");
                Assert.NotEqual("complete", await page.EvaluateAsync<string>("document.readyState"));
                await page.EvaluateAsync("() => { const edit = document.querySelector('[data-record-open].record-result-focus'); window.scrollTo(0, 0); const original = edit.scrollIntoView; edit.scrollIntoView = function(...args) { window.__restoredScrollCalled = true; return original.apply(this, args); }; }");
                if (changeFocusBeforeLoad)
                    await page.EvaluateAsync("() => { const notice = document.querySelector('[data-member-edit-unchanged]'); notice.tabIndex = -1; notice.focus({ preventScroll: true }); }");
            }
        }
        finally
        {
            page.FrameNavigated -= OnFrameNavigated;
            releaseImage.TrySetResult();
        }
        await page.WaitForLoadStateAsync(LoadState.Load);
        if (changeFocusBeforeLoad)
        {
            Assert.True(await page.Locator("[data-member-edit-unchanged]").EvaluateAsync<bool>("element => element === document.activeElement"));
            Assert.False(await page.EvaluateAsync<bool>("Boolean(window.__restoredScrollCalled)"));
            return;
        }
        if (delayReloadImage) Assert.True(await page.EvaluateAsync<bool>("Boolean(window.__restoredScrollCalled)"));
        var notice = page.Locator("[data-member-edit-unchanged]");
        Assert.Contains("No changes were made to the member.", await notice.InnerTextAsync());
        var edit = page.Locator($"[data-record-open='edit-member-{seeded.MemberId}']");
        Assert.Contains("member-edit-unchanged", await edit.GetAttributeAsync("aria-describedby"), StringComparison.Ordinal);
        Assert.Equal("status", await notice.GetAttributeAsync("role"));
        await page.WaitForFunctionAsync("selector => { const edit = document.querySelector(selector), notice = document.querySelector('[data-member-edit-unchanged]'), header = document.querySelector('.topbar'); if (!edit || !notice || !header || document.activeElement !== edit) return false; const r = edit.getBoundingClientRect(), n = notice.getBoundingClientRect(), h = header.getBoundingClientRect(); return n.top >= h.bottom && n.bottom <= innerHeight && r.top >= n.bottom && r.bottom <= innerHeight; }", $"[data-record-open='edit-member-{seeded.MemberId}']", new() { Timeout = 10000 });
        await OpenEditor();
        await page.Locator("#record-dialog #FullName").FillAsync("Edited Member Name");
        await page.Locator("#record-dialog button[type='submit']").ClickAsync();
        await page.Locator("#record-dialog").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await using (var observer = Context())
            Assert.Equal("Edited Member Name", (await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct)).Name);
        await OpenEditor();
        Assert.Equal("Edited Member Name", await page.Locator("#record-dialog #FullName").InputValueAsync());
        await page.Locator("#record-dialog #FullName").FillAsync("Unsaved Browser Input");
        await page.Locator("#record-dialog #StudentNumberCorrectionReason").FillAsync("Keep browser reason");
        await using (var competing = Context())
        {
            var service = CreateService(competing, seeded.ActorId);
            var model = await service.GetMemberEditorAsync(seeded.MemberId, ct);
            var version = MemberEditSnapshot.FromModel(model);
            model.Section = "B";
            Assert.True(await service.UpdateMemberAsync(model, ct, version));
        }
        await page.Locator("#record-dialog button[type='submit']").ClickAsync();
        var errors = page.Locator("#record-dialog .validation-summary-errors");
        await errors.WaitForAsync();
        Assert.Contains("out of date", await errors.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Your input is retained", await page.Locator("#record-dialog [data-record-message]").InnerTextAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("highlighted errors", await page.Locator("#record-dialog [data-record-message]").InnerTextAsync(), StringComparison.Ordinal);
        Assert.True(await errors.EvaluateAsync<bool>("element => element === document.activeElement"));
        Assert.Equal("Unsaved Browser Input", await page.Locator("#record-dialog #FullName").InputValueAsync());
        Assert.Equal("Keep browser reason", await page.Locator("#record-dialog #StudentNumberCorrectionReason").InputValueAsync());
        Assert.Equal("_blank", await page.Locator("#record-dialog a:has-text('Open latest member')").GetAttributeAsync("target"));
        await page.Keyboard.PressAsync("Escape");
        Assert.True(await page.Locator("[data-discard-prompt]").IsVisibleAsync());
        await page.Locator("[data-discard-prompt] button:has-text('Discard changes')").ClickAsync();
        await page.Locator("#record-dialog").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await OpenEditor();
        Assert.Equal("B", await page.Locator("#record-dialog #Section").InputValueAsync());
        await page.Locator("#record-dialog #StudentNumber").FillAsync("123");
        await page.Locator("#record-dialog #MobileNumber").FillAsync("123");
        await page.Locator("#record-dialog button[type='submit']").ClickAsync();
        await page.Locator("#record-dialog #StudentNumber-validation.field-validation-error").WaitForAsync();
        Assert.Equal(0, await page.Locator("#record-dialog [data-record-conflict]").CountAsync());
        Assert.Equal(0, await page.Locator("#record-dialog .validation-summary-errors").CountAsync());
        Assert.Equal("Please correct the highlighted errors.", await page.Locator("#record-dialog [data-record-message]").InnerTextAsync());
        Assert.Contains("8-digit", await page.Locator("#StudentNumber-validation").InnerTextAsync());
        Assert.Contains("11-digit", await page.Locator("#MobileNumber-validation").InnerTextAsync());
        Assert.True(await page.Locator("#record-dialog #StudentNumber").EvaluateAsync<bool>("element => element === document.activeElement"));
        Assert.Equal(1, await page.Locator("#record-dialog").GetByText("Enter an 8-digit student number.", new() { Exact = true }).CountAsync());
        if (Environment.GetEnvironmentVariable("ATTENDTA_VISUAL_DIR") is string visualDir)
            await page.ScreenshotAsync(new() { Path = System.IO.Path.Combine(visualDir, $"attendta-member-dialog-{width}.png"), FullPage = true });
        await page.Locator("#record-dialog #MobileNumber").FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");
        Assert.True(await page.Locator("#record-dialog button[type='submit']").EvaluateAsync<bool>("element => element === document.activeElement && element.getBoundingClientRect().bottom <= document.querySelector('#record-dialog').getBoundingClientRect().bottom"));
        await page.Keyboard.PressAsync("Shift+Tab");
        Assert.True(await page.Locator("#record-dialog [data-record-close]").Last.EvaluateAsync<bool>("element => element === document.activeElement"));
        await page.Locator("#record-dialog [data-record-close]").First.ClickAsync();
        Assert.True(await page.Locator("[data-discard-prompt]").IsVisibleAsync());
        using var standalone = await client.GetAsync($"/Member/Edit/{seeded.MemberId}", ct);
        var html = await standalone.Content.ReadAsStringAsync(ct);
        Assert.Contains("action=\"/Member/Edit\"", html, StringComparison.Ordinal);
        await page.GotoAsync($"http://localhost/Member/Edit/{seeded.MemberId}");
        await page.Locator("#StudentNumber").FillAsync("123");
        await page.Locator("form.form-panel button[type='submit']").ClickAsync();
        await page.Locator("#StudentNumber-validation.field-validation-error").WaitForAsync();
        Assert.Equal(1, await page.GetByText("Enter an 8-digit student number.", new() { Exact = true }).CountAsync());
        Assert.Equal(0, await page.Locator(".validation-summary-errors").CountAsync());
        if (Environment.GetEnvironmentVariable("ATTENDTA_VISUAL_DIR") is string standaloneVisualDir)
            await page.ScreenshotAsync(new() { Path = System.IO.Path.Combine(standaloneVisualDir, $"attendta-member-standalone-{width}.png"), FullPage = true });
    }

    [Theory]
    [InlineData(false, 390)]
    [InlineData(false, 1440)]
    [InlineData(true, 390)]
    [InlineData(true, 1440)]
    public async Task ConflictReloadLinkOpensCurrentEditorWithoutDiscardingStaleInput(bool dialog, int width)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedValidMemberAsync(ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var browserContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = 700 } });
        await browserContext.RouteAsync("**/*", async route =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(route.Request.Method), route.Request.Url);
            if (route.Request.Headers.TryGetValue("x-record-dialog", out var dialogHeader))
                request.Headers.Add("X-Record-Dialog", dialogHeader);
            if (route.Request.PostDataBuffer is byte[] body)
            {
                request.Content = new ByteArrayContent(body);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", route.Request.Headers["content-type"]);
            }
            using var response = await client.SendAsync(request, ct);
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(header => header.Key is not ("Transfer-Encoding" or "Content-Length" or "Content-Encoding"))
                .ToDictionary(header => header.Key, header => string.Join(", ", header.Value));
            await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = await response.Content.ReadAsByteArrayAsync(ct) });
        });
        var page = await browserContext.NewPageAsync();
        await page.GotoAsync(dialog
            ? $"http://localhost/Member?search={Uri.EscapeDataString(seeded.MemberEmail)}"
            : $"http://localhost/Member/Edit/{seeded.MemberId}");
        if (dialog) await page.Locator($"[data-record-open='edit-member-{seeded.MemberId}']").ClickAsync();
        var form = page.Locator(dialog ? "#record-dialog form[data-record-form]" : "form.form-panel");
        await form.Locator("#FullName").WaitForAsync();
        Assert.Equal(0, await form.Locator("[data-record-conflict]").CountAsync());
        Assert.Equal(0, await form.Locator("[aria-describedby^='member-edit-recovery-']").CountAsync());
        var originalVersion = await form.Locator("input[name='EditVersion']").InputValueAsync();
        await form.Locator("#FullName").FillAsync("Unsaved Browser Input");
        await form.Locator("#StudentNumberCorrectionReason").FillAsync("Keep browser reason");
        await using (var competing = Context())
        {
            var service = CreateService(competing, seeded.ActorId);
            var model = await service.GetMemberEditorAsync(seeded.MemberId, ct);
            var snapshot = MemberEditSnapshot.FromModel(model);
            model.FullName = "Winning Editor";
            model.Section = "B";
            Assert.True(await service.UpdateMemberAsync(model, ct, snapshot));
        }
        await form.Locator("button[type='submit']").ClickAsync();
        await form.Locator(".validation-summary-errors").WaitForAsync();
        Assert.Contains("out of date", await form.Locator(".validation-summary-errors").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("copy the changes you want to keep into the fresh form, then save that form", await form.Locator("[data-record-conflict]").InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal("Unsaved Browser Input", await form.Locator("#FullName").InputValueAsync());
        Assert.Equal("Keep browser reason", await form.Locator("#StudentNumberCorrectionReason").InputValueAsync());
        Assert.Equal(originalVersion, await form.Locator("input[name='EditVersion']").InputValueAsync());

        var summary = form.Locator(".validation-summary-errors");
        Assert.True(await summary.EvaluateAsync<bool>("element => element === document.activeElement"));
        var recoveryId = await summary.GetAttributeAsync("aria-describedby");
        Assert.False(string.IsNullOrWhiteSpace(recoveryId));
        Assert.Equal(1, await page.Locator($"[id='{recoveryId}']").CountAsync());
        Assert.Equal(recoveryId, await form.Locator("[data-record-conflict]").GetAttributeAsync("id"));
        if (dialog)
        {
            Assert.True(await summary.EvaluateAsync<bool>("element => { const style = getComputedStyle(element), recovery = element.parentElement.querySelector('[data-record-conflict]'); return style.outlineStyle === 'solid' && style.outlineWidth === '2px' && style.outlineColor === 'rgb(29, 78, 216)' && style.outlineOffset === '2px' && recovery.getBoundingClientRect().top - element.getBoundingClientRect().bottom >= 16; }"));
        }
        Assert.True(await form.Locator("[data-record-conflict]").EvaluateAsync<bool>("element => { const next = element.parentElement.querySelector('label[for=Email]'); return next.getBoundingClientRect().top - element.getBoundingClientRect().bottom >= 16; }"));
        var recovery = form.GetByRole(AriaRole.Link, new() { Name = "Open latest member in a new tab" });
        if (dialog)
        {
            await page.Keyboard.PressAsync("Tab");
            Assert.True(await page.Locator("#record-dialog [aria-label='Close editor']").EvaluateAsync<bool>("element => element === document.activeElement"));
        }
        await page.Keyboard.PressAsync("Tab");
        Assert.True(await recovery.EvaluateAsync<bool>("element => element === document.activeElement"));
        var fresh = await browserContext.RunAndWaitForPageAsync(async () =>
        {
            await page.Keyboard.PressAsync("Enter");
        });
        await fresh.WaitForLoadStateAsync();
        Assert.Equal($"http://localhost/Member/Edit/{seeded.MemberId}", fresh.Url);
        var freshForm = fresh.Locator("form.form-panel");
        await freshForm.Locator("#FullName").WaitForAsync();
        Assert.Equal("Winning Editor", await freshForm.Locator("#FullName").InputValueAsync());
        Assert.Equal("B", await freshForm.Locator("#Section").InputValueAsync());
        Assert.NotEqual(originalVersion, await freshForm.Locator("input[name='EditVersion']").InputValueAsync());
        Assert.Equal("Unsaved Browser Input", await form.Locator("#FullName").InputValueAsync());
        Assert.Equal("Keep browser reason", await form.Locator("#StudentNumberCorrectionReason").InputValueAsync());
        Assert.Equal(originalVersion, await form.Locator("input[name='EditVersion']").InputValueAsync());

        await freshForm.Locator("#FullName").FillAsync("Fresh Editor Save");
        var saveResponse = await fresh.RunAndWaitForResponseAsync(
            async () => await freshForm.Locator("button[type='submit']").ClickAsync(),
            response => response.Request.Method == "POST" && response.Url.EndsWith("/Member/Edit", StringComparison.Ordinal));
        Assert.Equal(302, saveResponse.Status);
        await using var observer = Context();
        Assert.Equal("Fresh Editor Save", (await observer.Users.AsNoTracking().SingleAsync(x => x.Id == seeded.UserId, ct)).Name);
        Assert.Equal(2, await observer.ActivityLogs.CountAsync(x => x.EntityType == "members" && x.EntityId == seeded.MemberId, ct));
        Assert.Equal("Unsaved Browser Input", await form.Locator("#FullName").InputValueAsync());
        Assert.Equal("Keep browser reason", await form.Locator("#StudentNumberCorrectionReason").InputValueAsync());
    }

    [Fact]
    public async Task EquivalentMemberEditPreservesStoredValuesMetadataAndAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using (var setup = Context())
        {
            var member = await setup.Members.SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            member.Course = "BS Information Technology";
            member.Section = "a";
            member.MobileNumber = "09171234567";
            await setup.SaveChangesAsync(ct);
        }
        await using (var writer = Context())
        {
            var changed = await UpdateCurrentMemberAsync(CreateService(writer, seeded.ActorId), new MemberViewModel
            {
                MemberId = seeded.MemberId, FullName = " original  MEMBER name ",
                StudentNumber = $" {seeded.StudentNumber} ", Course = "BSIT", YearLevel = 2,
                Section = "A", MobileNumber = " 09171234567 ", MembershipType = " Regular "
            }, ct);
            Assert.False(changed);
        }
        await using var observer = Context();
        var after = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        Assert.Equal("BS Information Technology", after.Course);
        Assert.Equal("a", after.Section);
        Assert.Null(after.UpdatedAt);
        Assert.Null(after.UpdatedBy);
        Assert.Null(user.UpdatedTime);
        Assert.Null(user.UpdatedById);
        Assert.Equal("Original Member Name", user.Name);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task StandaloneUnchangedEditShowsNeutralNotice()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct, withLoginPasswords: true);
        await using (var setup = Context())
        {
            var member = await setup.Members.SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            member.Course = "BSIT";
            member.MobileNumber = "09171234567";
            await setup.SaveChangesAsync(ct);
        }
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        using var page = await client.GetAsync($"/Member/Edit/{seeded.MemberId}", ct);
        var html = await page.Content.ReadAsStringAsync(ct);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        using var response = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["EditVersion"] = HiddenValue(html, "EditVersion"), ["MemberId"] = seeded.MemberId.ToString(),
            ["FullName"] = "Original Member Name", ["StudentNumber"] = seeded.StudentNumber,
            ["Course"] = "BSIT", ["YearLevel"] = "2", ["Section"] = "A",
            ["MobileNumber"] = "09171234567", ["MembershipType"] = "Regular"
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var result = await client.GetAsync(response.Headers.Location!, ct);
        Assert.Contains("No changes were made to the member.", await result.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        await using var observer = Context();
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpEditNormalizesBeforeValidationWithoutIgnoringBindingErrors(bool dialog)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct, withLoginPasswords: true);
        await using (var setup = Context())
        {
            var member = await setup.Members.SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            member.Course = "BSIT";
            member.MobileNumber = "09171234567";
            await setup.SaveChangesAsync(ct);
        }
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        using var page = await client.GetAsync($"/Member/Edit/{seeded.MemberId}", ct);
        var html = await page.Content.ReadAsStringAsync(ct);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["EditVersion"] = HiddenValue(html, "EditVersion"), ["MemberId"] = seeded.MemberId.ToString(),
            ["FullName"] = " original MEMBER name ", ["StudentNumber"] = $" {seeded.StudentNumber} ",
            ["Course"] = " BS Information Technology ", ["YearLevel"] = "2", ["Section"] = " a ",
            ["MobileNumber"] = " 09171234567 ", ["MembershipType"] = " Regular "
        };
        if (dialog) client.DefaultRequestHeaders.Add("X-Record-Dialog", "true");
        using var response = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(fields), ct);
        if (dialog)
            Assert.Contains("\"saved\":true", await response.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        else
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        fields["YearLevel"] = "not-a-number";
        using var invalid = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(fields), ct);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidHtml = await invalid.Content.ReadAsStringAsync(ct);
        AssertFieldError(invalidHtml, "YearLevel");
        Assert.Contains("value=\"BSIT\" selected", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"A\" selected", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"Regular\" selected", invalidHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-record-conflict", invalidHtml, StringComparison.Ordinal);
        fields["YearLevel"] = "2";
        fields["StudentNumber"] = UniqueStudentNumber();
        using var correction = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(fields), ct);
        var correctionHtml = await correction.Content.ReadAsStringAsync(ct);
        Assert.Contains("A reason is required", correctionHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"BSIT\" selected", correctionHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"A\" selected", correctionHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"Regular\" selected", correctionHtml, StringComparison.Ordinal);
        await using var observer = Context();
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateStudentNumberAppearsOnlyBesideItsField(bool dialog)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedValidMemberAsync(ct);
        var other = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        var fields = await ReadEditFieldsAsync(client, seeded, dialog, ct);
        fields["StudentNumber"] = other.StudentNumber;
        fields["StudentNumberCorrectionReason"] = "Correct the number";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Member/Edit") { Content = new FormUrlEncodedContent(fields) };
        if (dialog) request.Headers.Add("X-Record-Dialog", "true");
        using var response = await client.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        AssertFieldError(html, "StudentNumber");
        Assert.Single(Regex.Matches(html, "Student number already exists\\.", RegexOptions.IgnoreCase));
        Assert.Matches("(?is)<span[^>]*data-valmsg-for=\"StudentNumber\"[^>]*>.*?Student number already exists\\..*?</span>", html);
        Assert.DoesNotContain("validation-summary-errors", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnrelatedEditPreservesEquivalentStoredStudentNumber()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        var storedNumber = $" {seeded.StudentNumber} ";
        await using (var setup = Context())
        {
            var member = await setup.Members.SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            member.StudentNumber = storedNumber;
            await setup.SaveChangesAsync(ct);
        }
        await using (var writer = Context())
        {
            Assert.True(await UpdateCurrentMemberAsync(CreateService(writer, seeded.ActorId), new MemberViewModel
            {
                MemberId = seeded.MemberId, FullName = "Changed Name", StudentNumber = seeded.StudentNumber,
                Course = "BSIT", YearLevel = 2, Section = "A", MembershipType = "Regular"
            }, ct));
        }
        await using var observer = Context();
        Assert.Equal(storedNumber, (await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct)).StudentNumber);
        var audit = await observer.ActivityLogs.AsNoTracking().SingleAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct);
        Assert.Equal(AuditActions.MemberEdited, audit.Action);
        Assert.Null(audit.Reason);
        Assert.Contains(storedNumber, audit.NewValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditComparesFreshMemberEvenWhenPreviouslyTracked()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using var writer = Context();
        var tracked = await writer.Members.SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        await using (var other = Context())
        {
            var member = await other.Members.SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            member.Course = "BSIT";
            member.MobileNumber = null;
            await other.SaveChangesAsync(ct);
        }
        Assert.Equal("Original course", tracked.Course);
        Assert.False(await UpdateCurrentMemberAsync(CreateService(writer, seeded.ActorId), new MemberViewModel
        {
            MemberId = seeded.MemberId, FullName = "Original Member Name", StudentNumber = seeded.StudentNumber,
            Course = "BSIT", YearLevel = 2, Section = "A", MembershipType = "Regular"
        }, ct));
        await using var observer = Context();
        var persisted = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        Assert.Equal("BSIT", persisted.Course);
        Assert.Null(persisted.UpdatedAt);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task GeneralMemberReadPreservesDirtyTrackedValuesAndEditRefusesToDiscardThem()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using var writer = Context();
        var work = new UnitOfWork(writer);
        var repository = new MemberRepository(work);
        var tracked = await repository.GetMemberAsync(seeded.MemberId, ct);
        tracked.Course = "Unsaved change";
        Assert.Same(tracked, await repository.GetMemberAsync(seeded.MemberId, ct));
        Assert.Equal("Unsaved change", tracked.Course);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UpdateCurrentMemberAsync(CreateService(writer, seeded.ActorId, work, repository), new MemberViewModel
            {
                MemberId = seeded.MemberId, FullName = "Original Member Name", StudentNumber = seeded.StudentNumber,
                Course = "BSIT", YearLevel = 2, Section = "A", MembershipType = "Regular"
            }, ct));
        Assert.Contains("unsaved tracked changes", exception.Message, StringComparison.Ordinal);
        Assert.Equal(EntityState.Modified, writer.Entry(tracked).State);
        Assert.Equal("Unsaved change", tracked.Course);
        await using var observer = Context();
        Assert.Equal("Original course", (await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct)).Course);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task CorrectionUpdatesLinkedProfileAuditsSafeValuesAndPreservesHistoryAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Inactive, ct);
        int registrationId;
        int certificateId;
        await using (var context = Context())
        {
            var type = new EventType
            {
                TypeName = $"Member test type {Guid.NewGuid():N}",
                Description = "Test event type",
                IsActive = true,
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow
            };
            var venue = new Venue
            {
                RoomName = $"Member test room {Guid.NewGuid():N}",
                Capacity = 30,
                ImagePath = "uploads/venues/member-test.jpg",
                IsActive = true,
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow
            };
            context.EventTypes.Add(type);
            context.Venues.Add(venue);
            await context.SaveChangesAsync(ct);

            var eventItem = new Event
            {
                Title = "Member profile historical event",
                TypeId = type.TypeId,
                VenueId = venue.VenueId,
                StartsAt = DateTime.UtcNow.AddDays(2),
                RegistrationDeadline = DateTime.UtcNow.AddDays(1),
                ParticipantLimit = 20,
                EventStatusId = EventStatusCode.Completed,
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow
            };
            context.Events.Add(eventItem);
            await context.SaveChangesAsync(ct);

            var registration = new Registration
            {
                MemberId = seeded.MemberId,
                EventId = eventItem.EventId,
                RegistrationStatusId = RegistrationStatusCode.Confirmed,
                CourseSnapshot = "Original course snapshot",
                YearLevelSnapshot = 2,
                SectionSnapshot = "OLD-A",
                SubmittedAt = DateTime.UtcNow.AddDays(-2),
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };
            context.Registrations.Add(registration);
            await context.SaveChangesAsync(ct);
            registrationId = registration.RegistrationId;

            var certificate = new Certificate
            {
                RegistrationId = registration.RegistrationId,
                CertificateStatusId = CertificateStatusCode.Issued,
                CertificateNumber = $"CERT-{Guid.NewGuid():N}",
                RecipientNameSnapshot = "Original member name",
                EventTitleSnapshot = eventItem.Title,
                EventStartsAtSnapshot = eventItem.StartsAt,
                IssuerNameSnapshot = "AttendTa",
                FilePath = "uploads/certificates/member-test.pdf",
                IssuedBy = seeded.ActorId,
                IssuedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = seeded.ActorId,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            context.Certificates.Add(certificate);
            await context.SaveChangesAsync(ct);
            certificateId = certificate.CertificateId;
        }

        var correctedNumber = UniqueStudentNumber();
        await using (var writer = Context())
        {
            await UpdateCurrentMemberAsync(CreateService(writer, seeded.ActorId), new MemberViewModel
            {
                MemberId = seeded.MemberId,
                FullName = "Corrected Member Name",
                Email = "tampered@example.invalid",
                StudentNumber = correctedNumber,
                StudentNumberCorrectionReason = "Verified against the registrar's record.",
                Course = "BSCS",
                YearLevel = 3,
                Section = "B",
                MobileNumber = "09171234567",
                MembershipType = "Honors"
            }, ct);
        }

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var audit = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct);
        var registrationAfter = await observer.Registrations.AsNoTracking().SingleAsync(item => item.RegistrationId == registrationId, ct);
        var certificateAfter = await observer.Certificates.AsNoTracking().SingleAsync(item => item.CertificateId == certificateId, ct);

        Assert.Equal("Corrected Member Name", user.Name);
        Assert.Equal(seeded.MemberEmail, user.Email);
        Assert.Equal(seeded.PasswordHash, user.Password);
        Assert.Equal(correctedNumber, member.StudentNumber);
        Assert.Equal("BSCS", member.Course);
        Assert.Equal(3, member.YearLevel);
        Assert.Equal("B", member.Section);
        Assert.Equal("09171234567", member.MobileNumber);
        Assert.Equal("Honors", member.MembershipType);
        Assert.Equal(MembershipStatusCode.Inactive, member.MembershipStatusId);
        Assert.Equal(seeded.ActorId, member.UpdatedBy);
        Assert.Equal(AuditActions.MemberStudentNumberCorrected, audit.Action);
        Assert.Equal("Verified against the registrar's record.", audit.Reason);
        Assert.Contains("\"FullName\":\"Original Member Name\"", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains($"\"StudentNumber\":\"{seeded.StudentNumber}\"", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains($"\"StudentNumber\":\"{correctedNumber}\"", audit.NewValues, StringComparison.Ordinal);
        Assert.Contains("\"Status\":\"inactive\"", audit.NewValues, StringComparison.Ordinal);
        Assert.DoesNotContain("password", audit.OldValues + audit.NewValues, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(seeded.PasswordHash, audit.OldValues + audit.NewValues, StringComparison.Ordinal);

        Assert.Equal("Original course snapshot", registrationAfter.CourseSnapshot);
        Assert.Equal(2, registrationAfter.YearLevelSnapshot);
        Assert.Equal("OLD-A", registrationAfter.SectionSnapshot);
        Assert.Equal("Original member name", certificateAfter.RecipientNameSnapshot);
        Assert.Equal("Member profile historical event", certificateAfter.EventTitleSnapshot);
        Assert.Equal(CertificateStatusCode.Issued, certificateAfter.CertificateStatusId);
    }

    [Fact]
    public async Task UnchangedStudentNumberNeedsNoReasonAndStillAuditsTheProfileEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using (var writer = Context())
        {
            await UpdateCurrentMemberAsync(CreateService(writer, seeded.ActorId), new MemberViewModel
            {
                MemberId = seeded.MemberId,
                FullName = "Renamed Member",
                StudentNumber = seeded.StudentNumber,
                Course = "BSIT",
                YearLevel = 2,
                Section = "A",
                MembershipType = "Regular"
            }, ct);
        }

        await using var observer = Context();
        var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var audit = await observer.ActivityLogs.AsNoTracking().SingleAsync(
            item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct);
        Assert.Equal(MembershipStatusCode.Active, member.MembershipStatusId);
        Assert.Equal(AuditActions.MemberEdited, audit.Action);
        Assert.Null(audit.Reason);
    }

    [Fact]
    public async Task StudentNumberRaceReturnsAFieldSpecificDuplicateAndRollsBackWithoutAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using var writer = Context();
        var work = new UnitOfWork(writer);
        var repository = new InsertCompetingMemberRepository(
            new MemberRepository(work),
            (userId, studentNumber, token) => InsertCompetingMemberAsync(userId, studentNumber, token));
        var service = CreateService(writer, seeded.ActorId, work, repository);
        var candidateNumber = UniqueStudentNumber();
        repository.StudentNumberForRace = candidateNumber;

        var exception = await Assert.ThrowsAsync<DomainException>(() => UpdateCurrentMemberAsync(service, new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "Attempted change",
            StudentNumber = candidateNumber,
            StudentNumberCorrectionReason = "Correction evidence",
            Course = "BSIT",
            YearLevel = 2,
            Section = "A"
        }, ct));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(MemberViewModel.StudentNumber), exception.Field);
        await using var observer = Context();
        var unchanged = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var unchangedUser = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        Assert.Equal(seeded.StudentNumber, unchanged.StudentNumber);
        Assert.Equal("Original Member Name", unchangedUser.Name);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task ExistingDuplicateStudentNumberReturnsAFieldErrorWithoutWriting()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        var other = await SeedMemberAsync(MembershipStatusCode.Inactive, ct);
        await using var writer = Context();
        var service = CreateService(writer, seeded.ActorId);

        var exception = await Assert.ThrowsAsync<DomainException>(() => UpdateCurrentMemberAsync(service, new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "Must not change",
            StudentNumber = other.StudentNumber,
            StudentNumberCorrectionReason = "Incorrect duplicate correction",
            Course = "BSCS",
            YearLevel = 2,
            Section = "A"
        }, ct));

        Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
        Assert.Equal(nameof(MemberViewModel.StudentNumber), exception.Field);
        await using var observer = Context();
        var unchanged = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var unchangedUser = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        Assert.Equal(seeded.StudentNumber, unchanged.StudentNumber);
        Assert.Equal("Original course", unchanged.Course);
        Assert.Equal("Original Member Name", unchangedUser.Name);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task AuditInsertFailureRollsBackTheMemberAndUserChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using var writer = Context(new FailWhenActivityLogIsSaved());
        var service = CreateService(writer, seeded.ActorId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateCurrentMemberAsync(service, new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "Should roll back",
            StudentNumber = seeded.StudentNumber,
            Course = "BSCS",
            YearLevel = 4,
            Section = "A"
        }, ct));

        await using var observer = Context();
        var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        Assert.Equal(seeded.StudentNumber, member.StudentNumber);
        Assert.Equal("Original course", member.Course);
        Assert.Equal(2, member.YearLevel);
        Assert.Equal("Original Member Name", user.Name);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task EditWaitsForTheLinkedUserRowLock()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        var signal = new UserLockCommandSignal();
        await using var writer = Context(signal);
        var service = CreateService(writer, seeded.ActorId);
        var original = MemberEditSnapshot.FromModel(await service.GetMemberAsync(seeded.MemberId, ct));
        await using var lockOwner = Context();
        await using var transaction = await lockOwner.Database.BeginTransactionAsync(ct);
        Assert.NotNull(await new UserRepository(new UnitOfWork(lockOwner)).LockUserAsync(seeded.UserId, ct));

        var pending = service.UpdateMemberAsync(new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "After lock release",
            StudentNumber = seeded.StudentNumber,
            Course = "BSIT",
            YearLevel = 2,
            Section = "A"
        }, ct, original);
        var committed = false;
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.NotSame(pending, await Task.WhenAny(pending, Task.Delay(300, ct)));
            await transaction.CommitAsync(ct);
            committed = true;
            await pending;
        }
        finally
        {
            if (!committed) await transaction.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
        }

        await using var observer = Context();
        Assert.Equal("After Lock Release", (await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct)).Name);
    }

    [Theory]
    [InlineData(nameof(MemberViewModel.StudentNumber), "2024001")]
    [InlineData(nameof(MemberViewModel.MobileNumber), "555-0111")]
    [InlineData(nameof(MemberViewModel.Section), "G")]
    public async Task ProfileValueOutsideTheSharedRulesIsRejectedOnTheMemberEditPath(string field, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using var writer = Context();
        var service = CreateService(writer, seeded.ActorId);

        var model = new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "Unchanged Member",
            StudentNumber = seeded.StudentNumber,
            Course = "BSCS",
            YearLevel = 2,
            Section = "A",
            MobileNumber = "09171234567"
        };
        typeof(MemberViewModel).GetProperty(field)!.SetValue(model, value);

        var exception = await Assert.ThrowsAsync<DomainException>(() => UpdateCurrentMemberAsync(service, model, ct));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(field, exception.Field);

        await using var observer = Context();
        var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        Assert.Equal(seeded.StudentNumber, member.StudentNumber);
        Assert.Equal("A", member.Section);
        Assert.Equal("555-0100", member.MobileNumber);
        Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task YearLevelOutsideOneToFourIsRejectedOnTheMemberEditPath()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using var writer = Context();
        var service = CreateService(writer, seeded.ActorId);

        var exception = await Assert.ThrowsAsync<DomainException>(() => UpdateCurrentMemberAsync(service, new MemberViewModel
        {
            MemberId = seeded.MemberId,
            FullName = "Unchanged Member",
            StudentNumber = seeded.StudentNumber,
            Course = "BSCS",
            YearLevel = 5,
            Section = "A"
        }, ct));

        Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
        Assert.Equal(nameof(MemberViewModel.YearLevel), exception.Field);
        Assert.Equal("Choose a year level from 1 to 4.", exception.Message);
    }

    [Fact]
    public async Task LowerCaseSectionIsStoredUpperCased()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using (var writer = Context())
        {
            await UpdateCurrentMemberAsync(CreateService(writer, seeded.ActorId), new MemberViewModel
            {
                MemberId = seeded.MemberId,
                FullName = "Section Case Member",
                StudentNumber = seeded.StudentNumber,
                Course = "BSIT",
                YearLevel = 2,
                Section = "b"
            }, ct);
        }

        await using var observer = Context();
        var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
        Assert.Equal("B", member.Section);
    }

    [Fact]
    public async Task NameIsStoredInTitleCaseOnTheMemberEditPath()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct);
        await using (var writer = Context())
        {
            await UpdateCurrentMemberAsync(CreateService(writer, seeded.ActorId), new MemberViewModel
            {
                MemberId = seeded.MemberId,
                FullName = "JOHN PAUL A. SINGCO",
                StudentNumber = seeded.StudentNumber,
                Course = "BSIT",
                YearLevel = 2,
                Section = "A"
            }, ct);
        }

        await using var observer = Context();
        var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
        Assert.Equal("John Paul A. Singco", user.Name);
    }

    [Fact]
    public async Task EditEndpointShowsReadOnlyEmailAndCorrectionValidationAndHasNoStatusToggle()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Inactive, ct, withLoginPasswords: true);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var managementClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(managementClient, seeded.ActorEmail, ManagementPassword, ct);

        using var page = await managementClient.GetAsync($"/Member/Edit/{seeded.MemberId}", ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(ct);
        Assert.Contains("name=\"Email\"", html, StringComparison.Ordinal);
        Assert.Contains("readonly", html, StringComparison.Ordinal);
        Assert.Contains("name=\"FullName\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"StudentNumberCorrectionReason\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Status\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleStatus", html, StringComparison.Ordinal);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));

        using (var missingToken = await managementClient.PostAsync("/Member/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["MemberId"] = seeded.MemberId.ToString()
        }), ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        }

        using var response = await managementClient.PostAsync("/Member/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["EditVersion"] = HiddenValue(html, "EditVersion"),
            ["MemberId"] = seeded.MemberId.ToString(),
            ["FullName"] = "Tampered attempt",
            ["Email"] = "attacker@example.invalid",
            ["StudentNumber"] = UniqueStudentNumber(),
            ["StudentNumberCorrectionReason"] = " ",
            ["Course"] = "BSCS",
            ["YearLevel"] = "3",
            ["Section"] = "B",
            ["Status"] = "active"
        }), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseHtml = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("A reason is required when correcting the student number.", responseHtml, StringComparison.Ordinal);
        Assert.Contains($"value=\"{seeded.MemberEmail}\"", responseHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker@example.invalid", responseHtml, StringComparison.Ordinal);

        using var invalidModelResponse = await managementClient.PostAsync("/Member/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["EditVersion"] = HiddenValue(html, "EditVersion"),
            ["MemberId"] = seeded.MemberId.ToString(),
            ["FullName"] = "",
            ["Email"] = "invalid-model@example.invalid",
            ["StudentNumber"] = seeded.StudentNumber,
            ["StudentNumberCorrectionReason"] = "",
            ["Course"] = "",
            ["YearLevel"] = "3",
            ["Section"] = "B"
        }), ct);
        Assert.Equal(HttpStatusCode.OK, invalidModelResponse.StatusCode);
        var invalidModelHtml = await invalidModelResponse.Content.ReadAsStringAsync(ct);
        Assert.Contains($"value=\"{seeded.MemberEmail}\"", invalidModelHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid-model@example.invalid", invalidModelHtml, StringComparison.Ordinal);

        using var offRuleResponse = await managementClient.PostAsync("/Member/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["EditVersion"] = HiddenValue(html, "EditVersion"),
            ["MemberId"] = seeded.MemberId.ToString(),
            ["FullName"] = "Off-rule profile",
            ["Email"] = "off-rule@example.invalid",
            ["StudentNumber"] = seeded.StudentNumber,
            ["StudentNumberCorrectionReason"] = "",
            ["Course"] = "BSIT",
            ["YearLevel"] = "5",
            ["Section"] = "G",
            ["MobileNumber"] = "555-0100"
        }), ct);
        Assert.Equal(HttpStatusCode.OK, offRuleResponse.StatusCode);
        var offRuleHtml = await offRuleResponse.Content.ReadAsStringAsync(ct);
        AssertFieldError(offRuleHtml, "MobileNumber");
        AssertFieldError(offRuleHtml, "YearLevel");
        AssertFieldError(offRuleHtml, "Section");
        Assert.Contains("value=\"555-0100\"", offRuleHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"5\" selected", offRuleHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"G\" selected", offRuleHtml, StringComparison.Ordinal);

        await using (var observer = Context())
        {
            var member = await observer.Members.AsNoTracking().SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            var user = await observer.Users.AsNoTracking().SingleAsync(item => item.Id == seeded.UserId, ct);
            Assert.Equal(seeded.StudentNumber, member.StudentNumber);
            Assert.Equal(MembershipStatusCode.Inactive, member.MembershipStatusId);
            Assert.Equal("Original Member Name", user.Name);
            Assert.Equal(seeded.MemberEmail, user.Email);
            Assert.False(await observer.ActivityLogs.AnyAsync(item => item.EntityType == "members" && item.EntityId == seeded.MemberId, ct));
        }

        using var togglePost = await managementClient.PostAsync($"/Member/ToggleStatus?id={seeded.MemberId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }), ct);
        Assert.Equal(HttpStatusCode.NotFound, togglePost.StatusCode);

        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(memberClient, seeded.MemberEmail, MemberPassword, ct);
        using var denied = await memberClient.GetAsync($"/Member/Edit/{seeded.MemberId}", ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("C", "C")]
    [InlineData("3A", "3A")]
    public async Task EditFormRendersTheStoredSectionAsTheSelectedOption(string storedSection, string expectedOption)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct, withLoginPasswords: true);
        await using (var writer = Context())
        {
            var member = await writer.Members.SingleAsync(item => item.MemberId == seeded.MemberId, ct);
            member.Section = storedSection;
            await writer.SaveChangesAsync(ct);
        }

        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var managementClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(managementClient, seeded.ActorEmail, ManagementPassword, ct);

        using var page = await managementClient.GetAsync($"/Member/Edit/{seeded.MemberId}", ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(ct);
        Assert.Contains($"value=\"{expectedOption}\" selected", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "FullName", "First Editor Name")]
    [InlineData(true, "StudentNumber", "87654321")]
    [InlineData(false, "Course", "BSCS")]
    [InlineData(true, "YearLevel", "3")]
    [InlineData(false, "Section", "B")]
    [InlineData(true, "MobileNumber", "09181112222")]
    [InlineData(false, "MembershipType", "Honors")]
    public async Task IndependentFormsRejectStaleEditsAcrossEditableFields(bool dialog, string field, string newValue)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedValidMemberAsync(ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        var first = await ReadEditFieldsAsync(client, seeded, dialog, ct);
        var second = await ReadEditFieldsAsync(client, seeded, dialog, ct);
        if (field == "StudentNumber") newValue = UniqueStudentNumber();
        first[field] = newValue;
        first["StudentNumberCorrectionReason"] = "Verified correction";
        second["FullName"] = "Second Editor Input";
        second["StudentNumberCorrectionReason"] = "Keep this reason";
        if (dialog) client.DefaultRequestHeaders.Add("X-Record-Dialog", "true");

        using var saved = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(first), ct);
        Assert.Equal(dialog ? HttpStatusCode.OK : HttpStatusCode.Redirect, saved.StatusCode);
        if (dialog) Assert.Contains("\"saved\":true", await saved.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        await using var before = Context();
        var beforeMember = await before.Members.AsNoTracking().SingleAsync(x => x.MemberId == seeded.MemberId, ct);
        var beforeUser = await before.Users.AsNoTracking().SingleAsync(x => x.Id == seeded.UserId, ct);
        var beforeSnapshot = MemberEditSnapshot.FromEntities(beforeMember, beforeUser.Name);
        var memberUpdated = (beforeMember.UpdatedAt, beforeMember.UpdatedBy);
        var userUpdated = (beforeUser.UpdatedTime, beforeUser.UpdatedById);

        using var rejected = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(second), ct);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var html = await rejected.Content.ReadAsStringAsync(ct);
        Assert.Contains("out of date", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Second Editor Input", html, StringComparison.Ordinal);
        Assert.Contains("Keep this reason", html, StringComparison.Ordinal);
        Assert.Equal(second["EditVersion"], HiddenValue(html, "EditVersion"));
        Assert.Contains($"/Member/Edit/{seeded.MemberId}", html, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", html, StringComparison.Ordinal);
        await using var after = Context();
        var afterMember = await after.Members.AsNoTracking().SingleAsync(x => x.MemberId == seeded.MemberId, ct);
        var afterUser = await after.Users.AsNoTracking().SingleAsync(x => x.Id == seeded.UserId, ct);
        Assert.Equal(beforeSnapshot, MemberEditSnapshot.FromEntities(afterMember, afterUser.Name));
        Assert.Equal(memberUpdated, (afterMember.UpdatedAt, afterMember.UpdatedBy));
        Assert.Equal(userUpdated, (afterUser.UpdatedTime, afterUser.UpdatedById));
        Assert.Equal(1, await after.ActivityLogs.CountAsync(x => x.EntityType == "members" && x.EntityId == seeded.MemberId, ct));
    }

    [Theory]
    [InlineData(false, "missing")]
    [InlineData(true, "malformed")]
    [InlineData(false, "tampered")]
    [InlineData(true, "wrong-member")]
    [InlineData(false, "wrong-member-invalid")]
    [InlineData(true, "wrong-member-invalid")]
    [InlineData(false, "other-key")]
    [InlineData(true, "missing-invalid")]
    public async Task InvalidVersionEvidenceCannotWrite(bool dialog, string variant)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedValidMemberAsync(ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        var fields = await ReadEditFieldsAsync(client, seeded, dialog, ct);
        fields["FullName"] = "Must Not Persist";
        switch (variant)
        {
            case "missing": fields.Remove("EditVersion"); break;
            case "missing-invalid": fields.Remove("EditVersion"); fields["YearLevel"] = "invalid"; break;
            case "malformed": fields["EditVersion"] = "not-a-token"; break;
            case "tampered": fields["EditVersion"] = fields["EditVersion"][..^3] + "xxx"; break;
            case "wrong-member":
            case "wrong-member-invalid":
                var other = await SeedValidMemberAsync(ct);
                fields["EditVersion"] = (await ReadEditFieldsAsync(client, other, dialog, ct))["EditVersion"];
                if (variant == "wrong-member-invalid") fields["YearLevel"] = "invalid";
                break;
            case "other-key":
                fields["EditVersion"] = new MemberEditSnapshotProtector(new EphemeralDataProtectionProvider())
                    .Protect(new MemberEditSnapshot(seeded.MemberId, seeded.UserId, "Original Member Name", seeded.StudentNumber, "BSIT", 2, "A", "09171234567", "Regular"));
                break;
        }
        if (dialog) client.DefaultRequestHeaders.Add("X-Record-Dialog", "true");
        using var response = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(fields), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("out of date", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("copy the changes you want to keep into the fresh form, then save that form", html, StringComparison.Ordinal);
        if (variant is "missing-invalid" or "wrong-member-invalid") AssertFieldError(html, "YearLevel");
        Assert.Contains("Must Not Persist", html, StringComparison.Ordinal);
        Assert.Contains($"/Member/Edit/{seeded.MemberId}", html, StringComparison.Ordinal);
        await using var observer = Context();
        var member = await observer.Members.AsNoTracking().SingleAsync(x => x.MemberId == seeded.MemberId, ct);
        var user = await observer.Users.AsNoTracking().SingleAsync(x => x.Id == seeded.UserId, ct);
        Assert.Equal("Original Member Name", user.Name);
        Assert.Null(member.UpdatedAt);
        Assert.Null(user.UpdatedTime);
        Assert.False(await observer.ActivityLogs.AnyAsync(x => x.EntityType == "members" && x.EntityId == seeded.MemberId, ct));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ValidationRetryKeepsOriginalVersionAndDetectsLaterChanges(bool dialog, bool becomesStale)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedValidMemberAsync(ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        var fields = await ReadEditFieldsAsync(client, seeded, dialog, ct);
        var original = fields["EditVersion"];
        fields["FullName"] = "Retry Input";
        fields["YearLevel"] = "invalid";
        if (dialog) client.DefaultRequestHeaders.Add("X-Record-Dialog", "true");
        using var invalid = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(fields), ct);
        var invalidHtml = await invalid.Content.ReadAsStringAsync(ct);
        AssertFieldError(invalidHtml, "YearLevel");
        Assert.DoesNotContain("data-record-conflict", invalidHtml, StringComparison.Ordinal);
        Assert.Equal(original, HiddenValue(invalidHtml, "EditVersion"));
        if (becomesStale)
        {
            await using var writer = Context();
            var service = CreateService(writer, seeded.ActorId);
            var model = await service.GetMemberEditorAsync(seeded.MemberId, ct);
            var snapshot = MemberEditSnapshot.FromModel(model);
            model.Section = "B";
            Assert.True(await service.UpdateMemberAsync(model, ct, snapshot));
        }
        fields["YearLevel"] = "2";
        fields["EditVersion"] = HiddenValue(invalidHtml, "EditVersion");
        using var retry = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(fields), ct);
        var html = await retry.Content.ReadAsStringAsync(ct);
        if (becomesStale)
        {
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            Assert.Contains("out of date", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Retry Input", html, StringComparison.Ordinal);
            Assert.Equal(original, HiddenValue(html, "EditVersion"));
        }
        else if (dialog) Assert.Contains("\"saved\":true", html, StringComparison.Ordinal);
        else Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        await using var observer = Context();
        Assert.Equal(becomesStale ? "Original Member Name" : "Retry Input", (await observer.Users.AsNoTracking().SingleAsync(x => x.Id == seeded.UserId, ct)).Name);
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(x => x.EntityType == "members" && x.EntityId == seeded.MemberId, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlreadyStaleFormKeepsVersionThroughValidationAndRejectsCorrectedRetry(bool dialog)
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedValidMemberAsync(ct);
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(client, seeded.ActorEmail, ManagementPassword, ct);
        var fields = await ReadEditFieldsAsync(client, seeded, dialog, ct);
        var original = fields["EditVersion"];
        fields["FullName"] = "Retry Input";
        fields["StudentNumberCorrectionReason"] = "Keep retry reason";
        fields["YearLevel"] = "invalid";
        await using (var writer = Context())
        {
            var service = CreateService(writer, seeded.ActorId);
            var winner = await service.GetMemberEditorAsync(seeded.MemberId, ct);
            var version = MemberEditSnapshot.FromModel(winner);
            winner.Section = "B";
            Assert.True(await service.UpdateMemberAsync(winner, ct, version));
        }
        if (dialog) client.DefaultRequestHeaders.Add("X-Record-Dialog", "true");
        using var invalid = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(fields), ct);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var invalidHtml = await invalid.Content.ReadAsStringAsync(ct);
        AssertFieldError(invalidHtml, "YearLevel");
        Assert.Contains("Retry Input", invalidHtml, StringComparison.Ordinal);
        Assert.Contains("Keep retry reason", invalidHtml, StringComparison.Ordinal);
        Assert.Equal(original, HiddenValue(invalidHtml, "EditVersion"));

        fields["YearLevel"] = "2";
        fields["EditVersion"] = HiddenValue(invalidHtml, "EditVersion");
        using var retry = await client.PostAsync("/Member/Edit", new FormUrlEncodedContent(fields), ct);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var conflictHtml = await retry.Content.ReadAsStringAsync(ct);
        Assert.Contains("out of date", conflictHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-record-conflict", conflictHtml, StringComparison.Ordinal);
        Assert.Contains("Retry Input", conflictHtml, StringComparison.Ordinal);
        Assert.Contains("Keep retry reason", conflictHtml, StringComparison.Ordinal);
        Assert.Equal(original, HiddenValue(conflictHtml, "EditVersion"));
        await using var observer = Context();
        Assert.Equal("B", (await observer.Members.AsNoTracking().SingleAsync(x => x.MemberId == seeded.MemberId, ct)).Section);
        Assert.Equal("Original Member Name", (await observer.Users.AsNoTracking().SingleAsync(x => x.Id == seeded.UserId, ct)).Name);
        Assert.Equal(1, await observer.ActivityLogs.CountAsync(x => x.EntityType == "members" && x.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task WaitingEditRechecksSnapshotAfterCooperatingWriterCommits()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedValidMemberAsync(ct);
        await using var initialContext = Context();
        var model = await CreateService(initialContext, seeded.ActorId).GetMemberEditorAsync(seeded.MemberId, ct);
        var original = MemberEditSnapshot.FromModel(model);
        model.FullName = "Waiting Editor";
        var signal = new UserLockCommandSignal();
        await using var waitingContext = Context(signal);
        var service = CreateService(waitingContext, seeded.ActorId);
        await using var owner = Context();
        await using var tx = await owner.Database.BeginTransactionAsync(ct);
        var user = await new UserRepository(new UnitOfWork(owner)).LockUserAsync(seeded.UserId, ct);
        var pending = service.UpdateMemberAsync(model, ct, original);
        var committed = false;
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.False(pending.IsCompleted);
            user.Name = "Winning Editor";
            await owner.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            committed = true;
            var error = await Assert.ThrowsAsync<DomainException>(() => pending);
            Assert.Equal(OutcomeCode.Stale, error.Outcome);
        }
        finally
        {
            if (!committed) await tx.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
        }
        await using var observer = Context();
        Assert.Equal("Winning Editor", (await observer.Users.AsNoTracking().SingleAsync(x => x.Id == seeded.UserId, ct)).Name);
        Assert.Null((await observer.Members.AsNoTracking().SingleAsync(x => x.MemberId == seeded.MemberId, ct)).UpdatedAt);
        Assert.False(await observer.ActivityLogs.AnyAsync(x => x.EntityType == "members" && x.EntityId == seeded.MemberId, ct));
    }

    [Fact]
    public async Task StaleEvidenceIsRejectedBeforeAnOtherwiseUnchangedEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var seeded = await SeedValidMemberAsync(ct);
        await using var context = Context();
        var service = CreateService(context, seeded.ActorId);
        var model = await service.GetMemberEditorAsync(seeded.MemberId, ct);
        var wrongUser = MemberEditSnapshot.FromModel(model) with { UserId = seeded.UserId + 1 };
        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateMemberAsync(model, ct, wrongUser));
        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        await using var observer = Context();
        Assert.Null((await observer.Members.AsNoTracking().SingleAsync(x => x.MemberId == seeded.MemberId, ct)).UpdatedAt);
        Assert.False(await observer.ActivityLogs.AnyAsync(x => x.EntityType == "members" && x.EntityId == seeded.MemberId, ct));
    }

    private async Task<SeededMember> SeedValidMemberAsync(CancellationToken ct)
    {
        var seeded = await SeedMemberAsync(MembershipStatusCode.Active, ct, withLoginPasswords: true);
        await using var context = Context();
        var member = await context.Members.SingleAsync(x => x.MemberId == seeded.MemberId, ct);
        member.Course = "BSIT";
        member.MobileNumber = "09171234567";
        await context.SaveChangesAsync(ct);
        return seeded;
    }

    private static async Task<Dictionary<string, string>> ReadEditFieldsAsync(HttpClient client, SeededMember seeded, bool dialog, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Member/Edit/{seeded.MemberId}");
        if (dialog) request.Headers.Add("X-Record-Dialog", "true");
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);
        return new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = HiddenValue(html, "__RequestVerificationToken"),
            ["EditVersion"] = HiddenValue(html, "EditVersion"), ["MemberId"] = seeded.MemberId.ToString(),
            ["FullName"] = "Original Member Name", ["StudentNumber"] = seeded.StudentNumber,
            ["Course"] = "BSIT", ["YearLevel"] = "2", ["Section"] = "A",
            ["MobileNumber"] = "09171234567", ["MembershipType"] = "Regular"
        };
    }

    private async Task<SeededMember> SeedMemberAsync(MembershipStatusCode status, CancellationToken cancellationToken, bool withLoginPasswords = false)
    {
        await using var context = Context();
        var actorEmail = UniqueEmail("management");
        var memberEmail = UniqueEmail("member-profile");
        var hasher = new Argon2PasswordHasher();
        var actor = new User
        {
            Email = actorEmail,
            Name = "Management Actor",
            Password = withLoginPasswords ? hasher.Hash(ManagementPassword) : "integration-password-hash",
            RoleId = RoleCode.Officer,
            IsActive = true
        };
        var linkedUser = new User
        {
            Email = memberEmail,
            Name = "Original Member Name",
            Password = withLoginPasswords ? hasher.Hash(MemberPassword) : "member-password-hash",
            RoleId = RoleCode.Member,
            IsActive = true
        };
        context.Users.AddRange(actor, linkedUser);
        await context.SaveChangesAsync(cancellationToken);

        var member = new Member
        {
            UserId = linkedUser.Id,
            StudentNumber = UniqueStudentNumber(),
            Course = "Original course",
            YearLevel = 2,
            Section = "A",
            MobileNumber = "555-0100",
            MembershipType = "Regular",
            MembershipStatusId = status,
            CreatedBy = actor.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(cancellationToken);
        return new SeededMember(actor.Id, actorEmail, member.MemberId, linkedUser.Id, memberEmail, linkedUser.Password, member.StudentNumber);
    }

    private async Task InsertCompetingMemberAsync(int userId, string studentNumber, CancellationToken cancellationToken)
    {
        await using var context = Context();
        context.Members.Add(new Member
        {
            UserId = userId,
            StudentNumber = studentNumber,
            Course = "Competitor course",
            YearLevel = 1,
            Section = "RACE",
            MembershipStatusId = MembershipStatusCode.Active,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static MemberService CreateService(
        AsiBasecodeDBContext context,
        int actorId,
        UnitOfWork? unitOfWork = null,
        IMemberRepository? memberRepository = null)
    {
        var work = unitOfWork ?? new UnitOfWork(context);
        var actor = new TestActor(actorId, "officer");
        return new MemberService(
            memberRepository ?? new MemberRepository(work),
            new UserRepository(work),
            work,
            TimeProvider.System,
            actor,
            new ActivityLogWriter(context, actor, TimeProvider.System));
    }

    private static async Task<bool> UpdateCurrentMemberAsync(MemberService service, MemberViewModel model, CancellationToken ct)
    {
        var current = await service.GetMemberAsync(model.MemberId, ct);
        return await service.UpdateMemberAsync(model, ct, MemberEditSnapshot.FromModel(current));
    }

    private async Task SignInAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        using var page = await client.GetAsync("/Account/Login", cancellationToken);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync(cancellationToken);
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string UniqueStudentNumber() => ProfileFixtures.NewStudentNumber();

    private static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Missing hidden field {name}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static void AssertFieldError(string html, string field)
    {
        var input = Regex.Match(html, $"<(?:input|select|textarea)\\b[^>]*\\bname=\"{field}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"No control for {field}.");
        Assert.Contains($"aria-describedby=\"{field}-validation\"", input.Value, StringComparison.Ordinal);
        var error = Regex.Match(html, $"<span[^>]*id=\"{field}-validation\"[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(error.Success, $"No error container for {field}.");
        Assert.False(string.IsNullOrWhiteSpace(Regex.Replace(error.Groups[1].Value, "<[^>]+>", "")), $"No server error for {field}.");
    }

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@attendta.test";

    private sealed record SeededMember(int ActorId, string ActorEmail, int MemberId, int UserId, string MemberEmail, string PasswordHash, string StudentNumber);

    private sealed class TestActor(int userId, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => userId;
        public string Role => role;
    }

    private sealed class InsertCompetingMemberRepository(
        IMemberRepository inner,
        Func<int, string, CancellationToken, Task> insertWinner) : IMemberRepository
    {
        private int inserted;
        public string StudentNumberForRace { get; set; } = string.Empty;
        public IQueryable<MemberRecord> GetMemberRecords() => inner.GetMemberRecords();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => inner.CountAsync(search, status, cancellationToken);
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => inner.GetPageAsync(search, status, skip, take, cancellationToken);
        public Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default) => inner.GetMemberRecordAsync(memberId, cancellationToken);
        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetMemberRecordByUserIdAsync(userId, cancellationToken);
        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetMemberByUserIdAsync(userId, cancellationToken);
        public Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) => inner.GetMemberAsync(memberId, cancellationToken);
        public void EnsureMemberCanRefresh(int memberId) => inner.EnsureMemberCanRefresh(memberId);
        public Task<Member> GetMemberForEditAsync(int memberId, CancellationToken cancellationToken = default) => inner.GetMemberForEditAsync(memberId, cancellationToken);
        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default) => inner.UserHasMemberProfileAsync(userId, cancellationToken);
        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default) => inner.StudentNumberExistsAsync(studentNumber, excludeId, cancellationToken);

        public async Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref inserted, 1) == 0)
            {
                await insertWinner(member.CreatedBy, StudentNumberForRace, cancellationToken);
            }

            await inner.UpdateMemberAsync(member, cancellationToken);
        }

        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default) => inner.AddMemberAsync(member, cancellationToken);
    }

    private sealed class FailWhenActivityLogIsSaved : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<ActivityLog>().Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new InvalidOperationException("Audit insert failed.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class UserLockCommandSignal : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("dbo.users", StringComparison.OrdinalIgnoreCase))
            {
                Started.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }
}
