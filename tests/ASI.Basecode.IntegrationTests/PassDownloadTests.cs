using System.Globalization;
using System.Net;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Proves the server-rendered door pass: the card shows a server QR image and a download link, the
/// authorized GET streams a valid PNG named <c>door-pass-&lt;id&gt;.png</c>, and every unavailable
/// request — another member's row, an unknown id, a cancelled, unconfirmed, unpublished or
/// token-less registration — is the same 403 with the same body and no image bytes. The
/// decode-to-token proof stays with QR-01, so no decoder dependency is added here.
/// </summary>
public sealed class PassDownloadTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private const string UnavailableBody = "This pass is unavailable.";

    [Fact]
    public async Task PassPngIsOwnerScopedAndEveryUnavailableRequestIsTheSameForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");

        var memberEmail = $"passes-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);

        var otherEmail = $"passes-other-{suffix}@attendta.test";
        var otherUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, otherEmail, MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var otherMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, otherUser.Id, MembershipStatusCode.Active, ct);

        var emptyEmail = $"passes-empty-{suffix}@attendta.test";
        var emptyPassword = MemberEventTestSupport.NewPassword();
        var emptyUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, emptyEmail, emptyPassword, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, emptyUser.Id, MembershipStatusCode.Active, ct);

        var upcoming = await SeedPassAsync("Passes upcoming event", EventStatusCode.Published, RegistrationStatusCode.Confirmed, new string('a', 64));
        var past = await SeedPassAsync("Passes past event", EventStatusCode.Published, RegistrationStatusCode.Confirmed, new string('b', 64));
        var cancelled = await SeedPassAsync("Passes cancelled registration", EventStatusCode.Published, RegistrationStatusCode.Cancelled, null);
        var unconfirmed = await SeedPassAsync("Passes unconfirmed registration", EventStatusCode.Published, RegistrationStatusCode.Pending, null);
        var unpublished = await SeedPassAsync("Passes unpublished event", EventStatusCode.Draft, RegistrationStatusCode.Confirmed, new string('c', 64));
        var tokenless = await SeedPassAsync("Passes token-less registration", EventStatusCode.Published, RegistrationStatusCode.Confirmed, null);
        var foreign = await SeedPassAsync("Passes other member event", EventStatusCode.Published, RegistrationStatusCode.Confirmed, new string('d', 64), otherMember.MemberId, otherUser.Id);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var anonymous = await client.GetAsync($"/Passes/Qr/{upcoming.RegistrationId}", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymous), StringComparison.Ordinal);

        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        // The card shows the server image and the download link, both pointing at the one authorized GET.
        var page = await client.GetStringAsync("/Passes", ct);
        Assert.Contains($"src=\"/Passes/Qr/{upcoming.RegistrationId}\"", page, StringComparison.Ordinal);
        Assert.Contains($"src=\"/Passes/Qr/{past.RegistrationId}\"", page, StringComparison.Ordinal);
        // Routing renders the boolean as "download=True"; the query key is what matters.
        Assert.Contains($"/Passes/Qr/{upcoming.RegistrationId}?download=true", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Download pass", page, StringComparison.Ordinal);

        // No card and no token for any unavailable registration, and none for another member's row.
        Assert.DoesNotContain($"data-registration-id=\"{cancelled.RegistrationId}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-registration-id=\"{unconfirmed.RegistrationId}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-registration-id=\"{unpublished.RegistrationId}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-registration-id=\"{tokenless.RegistrationId}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"data-registration-id=\"{foreign.RegistrationId}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('a', 64), page, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('b', 64), page, StringComparison.Ordinal);
        Assert.DoesNotContain("/lib/qrcode", page, StringComparison.Ordinal);
        Assert.DoesNotContain("passes.js", page, StringComparison.Ordinal);

        // The inline image is a valid PNG with a no-store cache directive.
        var inline = await client.GetAsync($"/Passes/Qr/{upcoming.RegistrationId}", ct);
        Assert.Equal(HttpStatusCode.OK, inline.StatusCode);
        Assert.Equal("image/png", inline.Content.Headers.ContentType?.MediaType);
        Assert.True(inline.Headers.CacheControl?.NoStore, "The pass image response must not be cached.");
        AssertPng(await inline.Content.ReadAsByteArrayAsync(ct));

        // The download is the same bytes under the door-pass-<id>.png name.
        var download = await client.GetAsync($"/Passes/Qr/{upcoming.RegistrationId}?download=true", ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"door-pass-{upcoming.RegistrationId}.png", download.Content.Headers.ContentDisposition?.FileNameStar);
        AssertPng(await download.Content.ReadAsByteArrayAsync(ct));

        // Every refusal is one indistinguishable answer: the same status, body and no image bytes.
        var unknownId = Math.Max(foreign.RegistrationId, tokenless.RegistrationId) + 100_000;
        foreach (var id in new[]
                 {
                     unknownId,
                     foreign.RegistrationId,
                     cancelled.RegistrationId,
                     unconfirmed.RegistrationId,
                     unpublished.RegistrationId,
                     tokenless.RegistrationId
                 })
        {
            await AssertUnavailableAsync(client, id, ct);
        }

        // A member with no registrations sees the empty state.
        using var emptyClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(emptyClient, emptyEmail, emptyPassword, ct);
        var emptyPage = await emptyClient.GetStringAsync("/Passes", ct);
        Assert.Contains("No active passes.", emptyPage, StringComparison.Ordinal);
        Assert.Contains("No past passes yet.", emptyPage, StringComparison.Ordinal);

        async Task<Registration> SeedPassAsync(
            string title,
            EventStatusCode eventStatus,
            RegistrationStatusCode registrationStatus,
            string? token,
            int? memberId = null,
            int? actorId = null)
        {
            var startsAt = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(Random.Shared.Next(0, 300));
            if (title.Contains("past", StringComparison.Ordinal))
            {
                startsAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            var eventItem = await MemberEventTestSupport.SeedEventAsync(
                fixture.ConnectionString, memberUser.Id, typeId, venueId, eventStatus,
                startsAt, startsAt.AddHours(-1), 10, null, title, ct);
            var registration = await MemberEventTestSupport.SeedRegistrationAsync(
                fixture.ConnectionString, eventItem.EventId, memberId ?? member.MemberId, registrationStatus,
                actorId ?? memberUser.Id, ct);

            if (token != null)
            {
                await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
                var row = await context.Registrations.SingleAsync(r => r.RegistrationId == registration.RegistrationId, ct);
                row.QrToken = token;
                await context.SaveChangesAsync(ct);
            }

            return registration;
        }
    }

    private static async Task AssertUnavailableAsync(HttpClient client, int registrationId, CancellationToken cancellationToken)
    {
        foreach (var url in new[]
                 {
                     $"/Passes/Qr/{registrationId}",
                     $"/Passes/Qr/{registrationId}?download=true"
                 })
        {
            var response = await client.GetAsync(url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden,
                $"{url} returned {(int)response.StatusCode} {response.Content.Headers.ContentType}.");
            Assert.Equal(UnavailableBody, body);
            Assert.NotEqual("image/png", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.NoStore, "A refusal must not be cached.");
        }
    }

    /// <summary>Checks the PNG signature and the IHDR header, without any image or QR dependency.</summary>
    private static void AssertPng(byte[] bytes)
    {
        Assert.True(bytes.Length > 33, "The pass image must contain a PNG header and dimensions.");
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(bytes, 12, 4));

        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        Assert.True(width >= 168, $"The pass image must be at least 168 px wide, was {width.ToString(CultureInfo.InvariantCulture)}.");
        Assert.Equal(width, height);
    }
}
