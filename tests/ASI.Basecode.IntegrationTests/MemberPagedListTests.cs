using System.Net;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class MemberPagedListTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task OfficerSearchesAndNavigatesStablePagesWhileStatusRemainsReadOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var key = Guid.NewGuid().ToString("N")[..10];
        var password = $"Test#{Guid.NewGuid():N}a1";
        var hash = new Argon2PasswordHasher().Hash(password);
        var options = new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options;
        var officer = new User { UserId = $"officer-{key}@attendta.test", Name = "Officer", Password = hash, RoleId = RoleCode.Officer, IsActive = true };
        var users = Enumerable.Range(1, 26).Select(i => new User
        {
            UserId = $"page-{key}-{i}@attendta.test",
            Name = $"{key} Member {Math.Min(i, 25):D2}",
            Password = hash,
            RoleId = RoleCode.Member,
            IsActive = true
        }).ToList();
        var otherUsers = Enumerable.Range(1, 25).Select(i => new User
        {
            UserId = $"unrelated-{i}@attendta.test",
            Name = $"Unrelated Member {i:D2}",
            Password = hash,
            RoleId = RoleCode.Member,
            IsActive = true
        }).ToList();
        int[] ids;
        await using (var context = new AsiBasecodeDBContext(options))
        {
            context.Users.Add(officer);
            context.Users.AddRange(users);
            context.Users.AddRange(otherUsers);
            await context.SaveChangesAsync(ct);
            var members = users.Select((user, i) => new Member
            {
                UserId = user.Id,
                StudentNumber = $"STU{key}-{i + 1:D2}",
                Course = "Computing",
                YearLevel = 1,
                Section = "A",
                MembershipStatusId = i == 25 ? MembershipStatusCode.Inactive : MembershipStatusCode.Active,
                CreatedBy = officer.Id,
                CreatedAt = DateTime.UtcNow
            }).ToList();
            context.Members.AddRange(members);
            context.Members.AddRange(otherUsers.Select((user, i) => new Member
            {
                UserId = user.Id,
                StudentNumber = $"OTHER-{i + 1:D2}",
                Course = "Computing",
                YearLevel = 1,
                Section = "A",
                MembershipStatusId = MembershipStatusCode.Active,
                CreatedBy = officer.Id,
                CreatedAt = DateTime.UtcNow
            }));
            await context.SaveChangesAsync(ct);
            ids = members.OrderBy(m => users.Single(u => u.Id == m.UserId).Name, StringComparer.Ordinal)
                .ThenBy(m => m.MemberId).Select(m => m.MemberId).ToArray();
        }

        await using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var officerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var redirect = await anonymous.GetAsync("/Member", ct);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Contains("/Account/Login", redirect.Headers.Location!.ToString(), StringComparison.Ordinal);
        await SignInAsync(memberClient, users[0].UserId, password, ct);
        using var denied = await memberClient.GetAsync("/Member", ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/Account/AccessDenied", denied.Headers.Location!.ToString(), StringComparison.Ordinal);
        await SignInAsync(officerClient, officer.UserId, password, ct);

        var unfiltered = await officerClient.GetStringAsync("/Member", ct);
        Assert.Equal(ids[..25], MemberIds(unfiltered));
        Assert.Contains("<td class=\"numeric\">", unfiltered, StringComparison.Ordinal);
        Assert.Contains("<td><span class=\"badge badge-", unfiltered, StringComparison.Ordinal);

        var inactiveOnly = await officerClient.GetStringAsync("/Member?status=inactive", ct);
        Assert.Equal(new[] { ids[25] }, MemberIds(inactiveOnly));
        var activeOnly = await officerClient.GetStringAsync("/Member?status=active", ct);
        Assert.DoesNotContain(ids[25], MemberIds(activeOnly));
        Assert.Contains("Page 1 of 3", unfiltered, StringComparison.Ordinal);
        var nameSearch = $"{key}%20Member";
        var first = await officerClient.GetStringAsync($"/Member?search={nameSearch}", ct);
        Assert.Equal(ids[..25], MemberIds(first));
        Assert.Contains("Page 1 of 2", first, StringComparison.Ordinal);
        Assert.Contains($"search={nameSearch}&amp;page=2", first, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleStatus", first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/Member/Edit/{ids[0]}", first, StringComparison.Ordinal);

        var second = await officerClient.GetStringAsync($"/Member?search={nameSearch}&page=2", ct);
        Assert.Equal(ids[25..], MemberIds(second));
        Assert.Contains("Page 2 of 2", second, StringComparison.Ordinal);
        Assert.Contains($"search={nameSearch}&amp;page=1", second, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", second, StringComparison.Ordinal);
        Assert.Contains("inactive", second, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ToggleStatus", second, StringComparison.OrdinalIgnoreCase);

        foreach (var page in new[] { "0", "-1", "not-a-number", "2147483648" })
            Assert.Equal(ids[..25], MemberIds(await officerClient.GetStringAsync($"/Member?search={nameSearch}&page={page}", ct)));
        Assert.Equal(ids[25..], MemberIds(await officerClient.GetStringAsync($"/Member?search={nameSearch}&page=999999", ct)));

        var studentFirst = await officerClient.GetStringAsync($"/Member?search=STU{key}", ct);
        Assert.Equal(ids[..25], MemberIds(studentFirst));
        Assert.Contains("Page 1 of 2", studentFirst, StringComparison.Ordinal);
        Assert.Contains($"search=STU{key}&amp;page=2", studentFirst, StringComparison.Ordinal);
        var studentSearch = await officerClient.GetStringAsync($"/Member?search=STU{key}&page=2", ct);
        Assert.Equal(ids[25..], MemberIds(studentSearch));
        Assert.Contains("Page 2 of 2", studentSearch, StringComparison.Ordinal);
        Assert.Contains($"search=STU{key}&amp;page=1", studentSearch, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Next page\"", studentSearch, StringComparison.Ordinal);
        var emailSearch = await officerClient.GetStringAsync($"/Member?search=page-{key}-1%40attendta.test", ct);
        Assert.Equal(new[] { ids[0] }, MemberIds(emailSearch));
        var empty = await officerClient.GetStringAsync($"/Member?search={key}-missing", ct);
        Assert.Empty(MemberIds(empty));
        Assert.Contains("No members match your search.", empty, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Pagination\"", empty, StringComparison.Ordinal);
    }

    private static int[] MemberIds(string html) => Regex.Matches(html, "data-member-id=\"(\\d+)\"")
        .Select(match => int.Parse(match.Groups[1].Value)).ToArray();

    private static async Task SignInAsync(HttpClient client, string email, string password, CancellationToken ct)
    {
        var html = await client.GetStringAsync("/Account/Login", ct);
        var token = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
