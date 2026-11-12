using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Proves the My Events registrations tab reads one owner-scoped cross-event page: only the
/// actor's rows, newest start first, 25 per page, every status label including the derived
/// cutoff label, and the guarded "View event" link.
/// </summary>
public sealed class MyEventsRegistrationQueryTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task RegistrationsAreOwnerScopedPagedAndLabeledAcrossStatuses()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");

        var memberEmail = $"my-events-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);

        var otherEmail = $"my-events-other-{suffix}@attendta.test";
        var otherUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, otherEmail, MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var otherMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, otherUser.Id, MembershipStatusCode.Active, ct);

        var emptyEmail = $"my-events-empty-{suffix}@attendta.test";
        var emptyPassword = MemberEventTestSupport.NewPassword();
        var emptyUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, emptyEmail, emptyPassword, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, emptyUser.Id, MembershipStatusCode.Active, ct);

        var events = new Dictionary<int, Event>();
        var registrations = new Dictionary<int, Registration>();
        for (var index = 1; index <= 26; index++)
        {
            var status = index switch
            {
                26 => RegistrationStatusCode.Confirmed,
                25 => RegistrationStatusCode.Waitlisted,
                24 => RegistrationStatusCode.Cancelled,
                23 => RegistrationStatusCode.Pending,
                22 => RegistrationStatusCode.Confirmed,
                21 => RegistrationStatusCode.Waitlisted,
                2 => RegistrationStatusCode.Waitlisted,
                _ => index % 2 == 0 ? RegistrationStatusCode.Confirmed : RegistrationStatusCode.Pending
            };
            // Index 2 is a started Waitlisted row and index 1 a started Pending row, so both
            // unresolved statuses exercise the derived cutoff label. Index 2 stays the 25th row
            // of page 1; index 1 is the only row on page 2.
            var eventStatus = index == 1 ? EventStatusCode.Cancelled : EventStatusCode.Published;
            var startsAt = index switch
            {
                1 => new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                2 => new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index)
            };

            var eventItem = await MemberEventTestSupport.SeedEventAsync(
                fixture.ConnectionString, memberUser.Id, typeId, venueId, eventStatus,
                startsAt, startsAt.AddHours(-1), 10, null, $"My events event {index:D2}", ct);
            events[index] = eventItem;
            registrations[index] = await MemberEventTestSupport.SeedRegistrationAsync(
                fixture.ConnectionString, eventItem.EventId, member.MemberId, status, memberUser.Id, ct);
        }

        var otherEvent = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, otherUser.Id, typeId, venueId, EventStatusCode.Published,
            new DateTime(2099, 12, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2099, 11, 30, 0, 0, 0, DateTimeKind.Utc),
            10, null, "Other member only event", ct);
        var otherRegistration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, otherEvent.EventId, otherMember.MemberId, RegistrationStatusCode.Confirmed, otherUser.Id, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var anonymous = await client.GetAsync("/MyEvents/Registrations", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymous), StringComparison.Ordinal);

        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var firstPage = await client.GetStringAsync("/MyEvents/Registrations", ct);
        Assert.Contains("Showing 1–25 of 26 records.", firstPage, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 2", firstPage, StringComparison.Ordinal);

        var firstPageIds = RegistrationIds(firstPage);
        var expectedFirstPage = Enumerable.Range(2, 25)
            .OrderByDescending(index => events[index].StartsAt)
            .ThenByDescending(index => registrations[index].RegistrationId)
            .Select(index => registrations[index].RegistrationId)
            .ToArray();
        Assert.Equal(expectedFirstPage, firstPageIds);

        // Every stored status renders through RegistrationStatusDisplay.
        Assert.Contains("Confirmed", firstPage, StringComparison.Ordinal);
        Assert.Contains("Waitlisted", firstPage, StringComparison.Ordinal);
        Assert.Contains("Cancelled", firstPage, StringComparison.Ordinal);
        Assert.Contains("Pending review", firstPage, StringComparison.Ordinal);
        Assert.Contains("badge-open", firstPage, StringComparison.Ordinal);
        Assert.Contains("badge-inactive", firstPage, StringComparison.Ordinal);
        Assert.Contains("badge-full", firstPage, StringComparison.Ordinal);

        // The started Waitlisted row is the 25th row of page 1 and carries the derived cutoff label.
        Assert.Equal(registrations[2].RegistrationId, firstPageIds[24]);
        var waitlistedStartedCard = Regex.Match(
            firstPage,
            $"<article[^>]*data-registration-id=\"{registrations[2].RegistrationId}\"[^>]*>.*?</article>",
            RegexOptions.Singleline);
        Assert.True(waitlistedStartedCard.Success, "The started Waitlisted registration must render on page 1.");
        Assert.Contains("Not confirmed before cutoff", waitlistedStartedCard.Value, StringComparison.Ordinal);
        Assert.Contains("badge-closed", waitlistedStartedCard.Value, StringComparison.Ordinal);

        // Ownership is filtered inside SQL: another member's row never appears.
        Assert.DoesNotContain($"data-registration-id=\"{otherRegistration.RegistrationId}\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Other member only event", firstPage, StringComparison.Ordinal);

        // The newest published card links to the member detail page.
        Assert.Contains($"/MemberEvents/Details?id={events[26].EventId}", firstPage, StringComparison.Ordinal);
        Assert.Contains("View event", firstPage, StringComparison.Ordinal);

        var secondPage = await client.GetStringAsync("/MyEvents/Registrations?page=2", ct);
        Assert.Contains("Showing 26–26 of 26 records.", secondPage, StringComparison.Ordinal);
        Assert.Contains("Page 2 of 2", secondPage, StringComparison.Ordinal);
        Assert.Equal(new[] { registrations[1].RegistrationId }, RegistrationIds(secondPage));
        Assert.Contains("Not confirmed before cutoff", secondPage, StringComparison.Ordinal);
        Assert.Contains("badge-closed", secondPage, StringComparison.Ordinal);
        Assert.DoesNotContain("View event", secondPage, StringComparison.Ordinal);
        Assert.DoesNotContain("/MemberEvents/Details", secondPage, StringComparison.Ordinal);

        var pastEnd = await client.GetStringAsync("/MyEvents/Registrations?page=99", ct);
        Assert.Equal(RegistrationIds(secondPage), RegistrationIds(pastEnd));
        Assert.Contains("Page 2 of 2", pastEnd, StringComparison.Ordinal);

        // A member with no registrations sees the empty state and no pager.
        using var emptyClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(emptyClient, emptyEmail, emptyPassword, ct);
        var emptyPage = await emptyClient.GetStringAsync("/MyEvents/Registrations", ct);
        Assert.Contains("No registrations to show yet.", emptyPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Showing ", emptyPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Page ", emptyPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", emptyPage, StringComparison.Ordinal);

        // The derived cutoff label never rewrites the stored Pending status.
        await using var context = MemberEventTestSupport.Context(fixture.ConnectionString);
        var started = await context.Registrations.AsNoTracking()
            .SingleAsync(row => row.RegistrationId == registrations[1].RegistrationId, ct);
        Assert.Equal(RegistrationStatusCode.Pending, started.RegistrationStatusId);
    }

    private static int[] RegistrationIds(string html) =>
        Regex.Matches(html, "data-registration-id=\"(\\d+)\"")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
}
