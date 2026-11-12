using System.Text.RegularExpressions;
using ASI.Basecode.Data.Models.Lookups;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// The dossier's fact sheet, roster caption and count are rendered contracts, not only styling.
/// </summary>
public sealed class EventDossierLayoutTests(SqlServerDatabaseFixture fixture)
    : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private const string Password = "DossierPassword#2026";

    private AttendTaWebApplicationFactory factory = null!;
    private HttpClient client = null!;
    private int eventId;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var admin = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"dossier-admin-{suffix}@attendta.test", Password, RoleCode.Admin, ct);
        var memberUser = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"dossier-member-{suffix}@attendta.test", Password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(
            fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, admin.Id, ct);
        var starts = new DateTime(2099, 11, 10, 1, 0, 0, DateTimeKind.Utc);
        var item = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, admin.Id, typeId, venueId, EventStatusCode.Published,
            starts, starts.AddDays(-1), 100, null, "Dossier layout assembly", ct);
        await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, item.EventId, member.MemberId, RegistrationStatusCode.Confirmed, admin.Id, ct);
        eventId = item.EventId;

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await MemberEventTestSupport.SignInAsync(client, admin.UserId, Password, ct);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task PopulatedRosterShowsCaptionCountAndSecondaryCheckIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var html = await client.GetStringAsync($"/Event/Details?id={eventId}&status=confirmed", ct);

        Assert.Contains("<caption>Times shown in Asia/Manila.</caption>", html, StringComparison.Ordinal);
        Assert.Contains("Showing 1–1 of 1 registration.", html, StringComparison.Ordinal);

        var checkInButton = Regex.Match(html, "<button[^>]*data-dialog-open=\"check-in-[0-9]+\"[^>]*>").Value;
        Assert.Contains("button-secondary", checkInButton, StringComparison.Ordinal);
        Assert.DoesNotContain("button-primary", checkInButton, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyRosterShowsNeitherCaptionNorCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var html = await client.GetStringAsync($"/Event/Details?id={eventId}&status=waitlisted", ct);

        Assert.DoesNotContain("<caption>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("result-count", html, StringComparison.Ordinal);
    }
}
