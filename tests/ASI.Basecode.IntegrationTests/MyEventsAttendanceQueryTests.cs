using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Proves the My Events attendance tab reads one owner-scoped cross-event page: only the actor's
/// rows, newest recorded first, 25 per page, the method label and the valid/invalidated state with
/// the stored reason.
/// </summary>
public sealed class MyEventsAttendanceQueryTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime RecordedBase = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AttendanceIsOwnerScopedPagedAndLabeled()
    {
        var ct = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");

        var memberEmail = $"my-attendance-{suffix}@attendta.test";
        var password = MemberEventTestSupport.NewPassword();
        var memberUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, memberUser.Id, ct);

        var otherEmail = $"my-attendance-other-{suffix}@attendta.test";
        var otherUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, otherEmail, MemberEventTestSupport.NewPassword(), RoleCode.Member, ct);
        var otherMember = await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, otherUser.Id, MembershipStatusCode.Active, ct);

        var emptyEmail = $"my-attendance-empty-{suffix}@attendta.test";
        var emptyPassword = MemberEventTestSupport.NewPassword();
        var emptyUser = await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, emptyEmail, emptyPassword, RoleCode.Member, ct);
        await MemberEventTestSupport.SeedMemberAsync(fixture.ConnectionString, emptyUser.Id, MembershipStatusCode.Active, ct);

        var attendance = new Dictionary<int, Attendance>();
        for (var index = 1; index <= 26; index++)
        {
            var eventItem = await MemberEventTestSupport.SeedEventAsync(
                fixture.ConnectionString, memberUser.Id, typeId, venueId, EventStatusCode.Published,
                RecordedBase.AddDays(index), RecordedBase.AddDays(index).AddHours(-1), 10, null,
                $"Attendance event {index:D2}", ct);

            var registration = await MemberEventTestSupport.SeedRegistrationAsync(
                fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, memberUser.Id, ct);

            // Index 5 is the invalidated row on page 1; every third index cycles the method label.
            var method = index % 3 == 0
                ? CheckInMethodCode.QrCode
                : index % 3 == 1 ? CheckInMethodCode.ManualSearch : CheckInMethodCode.StudentNumber;
            var recordedAt = RecordedBase.AddMinutes(index * 10);
            attendance[index] = await SeedAttendanceAsync(
                fixture.ConnectionString,
                registration.RegistrationId,
                memberUser.Id,
                recordedAt,
                method,
                index == 5 ? recordedAt.AddMinutes(1) : null,
                index == 5 ? "Pass could not be verified at the door." : null,
                ct);
        }

        var otherEvent = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, otherUser.Id, typeId, venueId, EventStatusCode.Published,
            RecordedBase.AddDays(40), RecordedBase.AddDays(39), 10, null, "Other member only event", ct);
        var otherRegistration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, otherEvent.EventId, otherMember.MemberId, RegistrationStatusCode.Confirmed, otherUser.Id, ct);
        var otherAttendance = await SeedAttendanceAsync(
            fixture.ConnectionString, otherRegistration.RegistrationId, otherUser.Id,
            RecordedBase.AddDays(41), CheckInMethodCode.QrCode, null, null, ct);

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var anonymous = await client.GetAsync("/MyEvents/Attendance", ct);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymous), StringComparison.Ordinal);

        await MemberEventTestSupport.SignInAsync(client, memberEmail, password, ct);

        var firstPage = await client.GetStringAsync("/MyEvents/Attendance", ct);
        Assert.Contains("Showing 1–25 of 26 records.", firstPage, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 2", firstPage, StringComparison.Ordinal);

        var firstPageIds = AttendanceIds(firstPage);
        var expectedFirstPage = Enumerable.Range(2, 25)
            .OrderByDescending(index => attendance[index].RecordedAt)
            .ThenByDescending(index => attendance[index].AttendanceId)
            .Select(index => attendance[index].AttendanceId)
            .ToArray();
        Assert.Equal(expectedFirstPage, firstPageIds);

        // The newest row renders its Manila recorded time, method label and Valid badge.
        Assert.Contains("2026-01-01 12:20 Asia/Manila", firstPage, StringComparison.Ordinal);
        Assert.Contains("QR code", firstPage, StringComparison.Ordinal);
        Assert.Contains("Manual search", firstPage, StringComparison.Ordinal);
        Assert.Contains("Student number", firstPage, StringComparison.Ordinal);
        Assert.Contains("badge-active", firstPage, StringComparison.Ordinal);
        Assert.Contains("Valid", firstPage, StringComparison.Ordinal);

        // The invalidated row stays visible with its badge and stored reason.
        Assert.Contains("badge-inactive", firstPage, StringComparison.Ordinal);
        Assert.Contains("Invalidated", firstPage, StringComparison.Ordinal);
        Assert.Contains("Pass could not be verified at the door.", firstPage, StringComparison.Ordinal);
        var invalidatedRow = Regex.Match(
            firstPage,
            $"<tr[^>]*data-attendance-id=\"{attendance[5].AttendanceId}\"[^>]*>.*?</tr>",
            RegexOptions.Singleline);
        Assert.True(invalidatedRow.Success, "The invalidated attendance row must render on page 1.");
        Assert.Contains("Attendance event 05", invalidatedRow.Value, StringComparison.Ordinal);
        Assert.Contains("Invalidated", invalidatedRow.Value, StringComparison.Ordinal);

        // Ownership is filtered inside SQL: another member's row never appears.
        Assert.DoesNotContain($"data-attendance-id=\"{otherAttendance.AttendanceId}\"", firstPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Other member only event", firstPage, StringComparison.Ordinal);

        var secondPage = await client.GetStringAsync("/MyEvents/Attendance?page=2", ct);
        Assert.Contains("Showing 26–26 of 26 records.", secondPage, StringComparison.Ordinal);
        Assert.Contains("Page 2 of 2", secondPage, StringComparison.Ordinal);
        Assert.Equal(new[] { attendance[1].AttendanceId }, AttendanceIds(secondPage));
        Assert.Contains("Attendance event 01", secondPage, StringComparison.Ordinal);

        var pastEnd = await client.GetStringAsync("/MyEvents/Attendance?page=99", ct);
        Assert.Equal(AttendanceIds(secondPage), AttendanceIds(pastEnd));
        Assert.Contains("Page 2 of 2", pastEnd, StringComparison.Ordinal);

        // A member with no attendance sees the empty state and no pager.
        using var emptyClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(emptyClient, emptyEmail, emptyPassword, ct);
        var emptyPage = await emptyClient.GetStringAsync("/MyEvents/Attendance", ct);
        Assert.Contains("No attendance to show yet.", emptyPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Showing ", emptyPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Page ", emptyPage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", emptyPage, StringComparison.Ordinal);
    }

    private static async Task<Attendance> SeedAttendanceAsync(
        string connectionString,
        int registrationId,
        int recordedBy,
        DateTime recordedAt,
        CheckInMethodCode method,
        DateTime? invalidatedAt,
        string? invalidationReason,
        CancellationToken cancellationToken)
    {
        await using var context = MemberEventTestSupport.Context(connectionString);
        var attendance = new Attendance
        {
            RegistrationId = registrationId,
            CheckInMethodId = method,
            RecordedBy = recordedBy,
            RecordedAt = recordedAt,
            InvalidatedBy = invalidatedAt is null ? null : recordedBy,
            InvalidatedAt = invalidatedAt,
            InvalidationReason = invalidationReason
        };
        context.Set<Attendance>().Add(attendance);
        await context.SaveChangesAsync(cancellationToken);
        return attendance;
    }

    private static int[] AttendanceIds(string html) =>
        Regex.Matches(html, "data-attendance-id=\"(\\d+)\"")
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
}
