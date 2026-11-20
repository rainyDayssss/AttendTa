using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// The administrator history must show recognizable records and only allowlisted, recorded
/// changes. Each class gets its own isolated database, so the assertions can own every row.
/// </summary>
public sealed class ActivityLogPresentationTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture fixture = new();
    private readonly Argon2PasswordHasher hasher = new();
    private AttendTaWebApplicationFactory factory;
    private HttpClient client;
    private string password;
    private string adminEmail;

    public async ValueTask InitializeAsync()
    {
        await fixture.InitializeAsync();
        var suffix = Guid.NewGuid().ToString("N");
        password = $"ActivityLog#{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";
        adminEmail = $"presentation-admin-{suffix}@attendta.test";

        factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await SeedAsync(adminEmail, suffix);
        await SignInAsync(adminEmail);
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task HistoryShowsCurrentRecordsFriendlyActionsAndOnlyRecordedChanges()
    {
        var html = await client.GetStringAsync("/admin/auditlogs?pageSize=25", TestContext.Current.CancellationToken);
        // The default encoder turns the arrow into a numeric entity; read the visible text.
        var text = WebUtility.HtmlDecode(html);

        // Slim table: the entity-type column is replaced by the affected record.
        Assert.Matches("<th[^>]*>Occurred</th>", text);
        Assert.Matches("<th[^>]*>Actor</th>", text);
        Assert.Matches("<th[^>]*>Action</th>", text);
        Assert.Matches("<th[^>]*>Affected record</th>", text);
        Assert.Matches("<th[^>]*>Actions</th>", text);
        Assert.DoesNotMatch("<th[^>]*>Entity type</th>", text);

        // Friendly action and the current account name for a recorded role change.
        Assert.Contains("Role changed", text, StringComparison.Ordinal);
        Assert.Contains("Participant Name", text, StringComparison.Ordinal);
        Assert.Contains("Names shown reflect current records.", text, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(text, "Names shown reflect current records\\."));
        Assert.Contains("Member → Administrator", text, StringComparison.Ordinal);
        Assert.Contains("Approved in test.", text, StringComparison.Ordinal);
        var roleRow = RowFor(text, "Role changed");
        Assert.Contains("Participant Name", roleRow, StringComparison.Ordinal);
        Assert.Contains("Member → Administrator", roleRow, StringComparison.Ordinal);
        Assert.Contains("Approved in test.", roleRow, StringComparison.Ordinal);
        Assert.Matches("<dt>Record ID</dt><dd>\\d+</dd>", roleRow);
        Assert.DoesNotContain("<dt>Action code</dt>", roleRow, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Actor type</dt>", roleRow, StringComparison.Ordinal);

        // Event update: the live title is the affected record and the recorded diff is allowlisted.
        Assert.Contains("Event updated", text, StringComparison.Ordinal);
        Assert.Contains("Spring Fest → Summer Fest", text, StringComparison.Ordinal);
        Assert.Contains("Draft → Published", text, StringComparison.Ordinal);
        Assert.Contains("50 → 80", text, StringComparison.Ordinal);
        Assert.Contains("2026-12-01 06:00 PM", text, StringComparison.Ordinal);
        var eventRow = RowFor(text, "Event updated", "Spring Fest");
        Assert.Contains("Summer Fest", eventRow, StringComparison.Ordinal);
        Assert.Contains("Spring Fest → Summer Fest", eventRow, StringComparison.Ordinal);
        Assert.Contains("Draft → Published", eventRow, StringComparison.Ordinal);

        // Registration/attendance rows identify the current participant and event.
        Assert.Contains("Registration approved", text, StringComparison.Ordinal);
        Assert.Contains("<dt>Related event</dt><dd>Summer Fest</dd>", RowFor(text, "Registration approved"), StringComparison.Ordinal);
        Assert.Contains("Pending → Confirmed", text, StringComparison.Ordinal);
        var registrationRow = RowFor(text, "Registration approved");
        Assert.Contains("Participant Name", registrationRow, StringComparison.Ordinal);
        Assert.Contains("Pending → Confirmed", registrationRow, StringComparison.Ordinal);
        Assert.Contains("Attendance recorded", text, StringComparison.Ordinal);
        Assert.Contains("QR scan", text, StringComparison.Ordinal);
        Assert.Contains("2026-11-05 09:30 PM", text, StringComparison.Ordinal);

        // Missing relation and unknown action/type keep a legible fallback with the record id.
        Assert.Contains("Unavailable", RowFor(text, "Event updated", "424242"), StringComparison.Ordinal);
        Assert.Contains("Unavailable (record 424242)", text, StringComparison.Ordinal);
        Assert.Contains("<dt>Record ID</dt><dd>424242</dd>", text, StringComparison.Ordinal);
        Assert.Contains("Some change details are unavailable", text, StringComparison.Ordinal);
        Assert.Contains("<dt>Title (recorded new)</dt><dd>Ghost</dd>", text, StringComparison.Ordinal);
        Assert.Contains("Recorded changes", text, StringComparison.Ordinal);
        Assert.Contains("audit.test.unknown", text, StringComparison.Ordinal);
        Assert.Contains("Unavailable", RowFor(text, "audit.test.unknown"), StringComparison.Ordinal);
        Assert.Contains("mystery_records", text, StringComparison.Ordinal);

        var certificateRow = RowFor(text, "Certificate declined");
        Assert.Contains("Participant Name", certificateRow, StringComparison.Ordinal);
        Assert.Contains("<dt>Related event</dt><dd>Summer Fest</dd>", certificateRow, StringComparison.Ordinal);
        Assert.Contains("<dt>Decision</dt><dd>Not eligible</dd>", certificateRow, StringComparison.Ordinal);
        var attendanceRow = RowFor(text, "Attendance recorded");
        Assert.Contains("Participant Name", attendanceRow, StringComparison.Ordinal);
        Assert.Contains("<dt>Related event</dt><dd>Summer Fest</dd>", attendanceRow, StringComparison.Ordinal);

        // Recorded personal contact data is outside the allowlist and never rendered.
        Assert.DoesNotContain("09171234567", text, StringComparison.Ordinal);
        Assert.DoesNotContain("09998887777", text, StringComparison.Ordinal);

        // Recorded text is HTML-encoded, not injected into the page.
        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FriendlySearchMatchesPartialLabelsAndIntersectsActionFilter()
    {
        var partial = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/admin/auditlogs?search=OLE%20CHAN&pageSize=5", TestContext.Current.CancellationToken));
        Assert.NotEmpty(Regex.Matches(partial, "<tr data-log-id="));
        Assert.Contains("Member → Administrator", partial, StringComparison.Ordinal);
        Assert.All(Regex.Matches(partial, """<tr data-log-id="[^"]+">.*?</tr>""", RegexOptions.Singleline),
            row => Assert.Contains("Role changed", row.Value, StringComparison.Ordinal));

        var filtered = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/admin/auditlogs?search=updated&filter={Uri.EscapeDataString(AuditActions.MemberEdited)}&pageSize=5", TestContext.Current.CancellationToken));
        Assert.NotEmpty(Regex.Matches(filtered, "<tr data-log-id="));
        Assert.All(Regex.Matches(filtered, """<tr data-log-id="[^"]+">.*?</tr>""", RegexOptions.Singleline),
            row => Assert.Contains("Member updated", row.Value, StringComparison.Ordinal));

        var noIntersection = WebUtility.HtmlDecode(await client.GetStringAsync(
            $"/admin/auditlogs?search=role%20chan&filter={Uri.EscapeDataString(AuditActions.MemberEdited)}", TestContext.Current.CancellationToken));
        Assert.Empty(Regex.Matches(noIntersection, "<tr data-log-id="));

        var codeSearch = WebUtility.HtmlDecode(await client.GetStringAsync(
            "/admin/auditlogs?search=audit.test.unknown", TestContext.Current.CancellationToken));
        Assert.NotEmpty(Regex.Matches(codeSearch, "<tr data-log-id="));
        Assert.Contains("mystery_records", codeSearch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FriendlySearchCountsAllMatchesBeforePaging()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context();
        for (var i = 0; i < 6; i++)
        {
            context.ActivityLogs.Add(new ActivityLog
            {
                ActorTypeId = ActorTypeCode.System,
                Action = AuditActions.UserRoleAssigned,
                EntityType = "users",
                EntityId = 900000 + i,
                Description = "Role assigned",
                OccurredAt = new DateTime(2026, 11, 6, 0, i, 0, DateTimeKind.Utc)
            });
        }
        await context.SaveChangesAsync(ct);
        var expected = await context.ActivityLogs.CountAsync(row => row.Action == AuditActions.UserRoleAssigned, ct);
        var first = await client.GetStringAsync("/admin/auditlogs?search=role%20chan&pageSize=5&page=1", ct);
        var second = await client.GetStringAsync("/admin/auditlogs?search=role%20chan&pageSize=5&page=2", ct);
        Assert.Contains($"data-paging-total=\"{expected}\"", first, StringComparison.Ordinal);
        Assert.Contains($"data-paging-total=\"{expected}\"", second, StringComparison.Ordinal);
        var firstIds = Regex.Matches(first, """<tr data-log-id="(\d+)">""").Select(match => match.Groups[1].Value).ToArray();
        var secondIds = Regex.Matches(second, """<tr data-log-id="(\d+)">""").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(5, firstIds.Length);
        Assert.NotEmpty(secondIds);
        Assert.Empty(firstIds.Intersect(secondIds));
    }

    [Fact]
    public async Task HistoryStillRequiresAnAdministrator()
    {
        using var officer = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await SignInAsync(officer, await SeedUserAsync("History Officer", RoleCode.Officer, $"history-officer-{Guid.NewGuid():N}@attendta.test"));

        var response = await officer.GetAsync("/admin/auditlogs", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", GetLocationPath(response), StringComparison.Ordinal);
    }

    private async Task SignInAsync(string email) => await SignInAsync(client, email);

    private async Task SignInAsync(HttpClient targetClient, string email)
    {
        var loginPage = await targetClient.GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);
        var token = Regex.Match(loginPage, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));
        var response = await targetClient.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private async Task SeedAsync(string administratorEmail, string suffix)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context();

        var admin = new User
        {
            Email = administratorEmail,
            Name = "History Administrator",
            Password = hasher.Hash(password),
            RoleId = RoleCode.Admin,
            IsActive = true
        };
        var participantUser = new User
        {
            Email = $"participant-{suffix}@attendta.test",
            Name = "Participant Name",
            Password = hasher.Hash(password),
            RoleId = RoleCode.Member,
            IsActive = true
        };
        context.Users.AddRange(admin, participantUser);
        await context.SaveChangesAsync(ct);

        var eventType = new EventType
        {
            TypeName = $"Seminar {suffix}",
            Description = "Seminar",
            IsActive = true,
            CreatedBy = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        var venue = new Venue
        {
            RoomName = $"Room 101 {suffix}",
            Capacity = 100,
            ImagePath = "uploads/venues/test.jpg",
            IsActive = true,
            CreatedBy = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.EventTypes.Add(eventType);
        context.Venues.Add(venue);
        await context.SaveChangesAsync(ct);

        var eventItem = new Event
        {
            Title = "Summer Fest",
            TypeId = eventType.TypeId,
            VenueId = venue.VenueId,
            StartsAt = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc),
            RegistrationDeadline = new DateTime(2026, 11, 30, 10, 0, 0, DateTimeKind.Utc),
            ParticipantLimit = 80,
            EventStatusId = EventStatusCode.Published,
            CreatedBy = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Events.Add(eventItem);
        await context.SaveChangesAsync(ct);

        var member = new Member
        {
            UserId = participantUser.Id,
            StudentNumber = $"2024-{suffix}",
            Course = "BSCS",
            YearLevel = 2,
            Section = "A",
            MobileNumber = "09171234567",
            MembershipType = "Regular",
            MembershipStatusId = MembershipStatusCode.Active,
            CreatedBy = admin.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Members.Add(member);
        await context.SaveChangesAsync(ct);

        var registration = new Registration
        {
            MemberId = member.MemberId,
            EventId = eventItem.EventId,
            RegistrationStatusId = RegistrationStatusCode.Confirmed,
            CourseSnapshot = "BSCS",
            YearLevelSnapshot = 2,
            SectionSnapshot = "A",
            SubmittedAt = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc),
            CreatedBy = participantUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Registrations.Add(registration);
        await context.SaveChangesAsync(ct);

        var attendance = new Attendance
        {
            RegistrationId = registration.RegistrationId,
            CheckInMethodId = CheckInMethodCode.QrCode,
            RecordedBy = admin.Id,
            RecordedAt = new DateTime(2026, 11, 5, 13, 30, 0, DateTimeKind.Utc)
        };
        context.AttendanceRecords.Add(attendance);
        await context.SaveChangesAsync(ct);

        var certificate = new Certificate
        {
            RegistrationId = registration.RegistrationId,
            CertificateStatusId = CertificateStatusCode.NotEligible,
            CreatedAt = DateTime.UtcNow
        };
        context.Certificates.Add(certificate);
        await context.SaveChangesAsync(ct);

        const string memberOldValues = """
            {"FullName":"Old Participant","StudentNumber":"2024-0001","Course":"BSCS","YearLevel":2,"Section":"A","MobileNumber":"09171234567","MembershipType":"Regular","Status":"active"}
            """;
        const string memberNewValues = """
            {"FullName":"<script>alert('x')</script>","StudentNumber":"2024-0002","Course":"BSIT","YearLevel":3,"Section":"B","MobileNumber":"09998887777","MembershipType":"Regular","Status":"active"}
            """;

        var logs = new List<ActivityLog>
        {
            new()
            {
                ActorTypeId = ActorTypeCode.User,
                ActorUserId = admin.Id,
                Action = AuditActions.CertificateDeclined,
                EntityType = "certificates",
                EntityId = certificate.CertificateId,
                Description = "Certificate declined",
                NewValues = """{"Status":"not_eligible","ReviewedAt":"2026-11-05T13:30:00Z"}""",
                OccurredAt = new DateTime(2026, 11, 5, 22, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                ActorTypeId = ActorTypeCode.User,
                ActorUserId = admin.Id,
                Action = AuditActions.UserRoleAssigned,
                EntityType = "users",
                EntityId = participantUser.Id,
                Description = "Role assigned",
                Reason = "Approved in test.",
                OldValues = """{"RoleCode":"member"}""",
                NewValues = """{"RoleCode":"admin"}""",
                OccurredAt = new DateTime(2026, 11, 5, 21, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                ActorTypeId = ActorTypeCode.User,
                ActorUserId = admin.Id,
                Action = AuditActions.EventUpdated,
                EntityType = "events",
                EntityId = eventItem.EventId,
                Description = "Event updated",
                OldValues = """{"Title":"Spring Fest","TypeName":"Seminar","VenueName":"Room 101","StartsAt":"2026-12-01T09:00:00Z","RegistrationDeadline":"2026-11-30T09:00:00Z","ParticipantLimit":50,"Status":1}""",
                NewValues = """{"Title":"Summer Fest","TypeName":"Seminar","VenueName":"Room 101","StartsAt":"2026-12-01T10:00:00Z","RegistrationDeadline":"2026-11-30T10:00:00Z","ParticipantLimit":80,"Status":2}""",
                OccurredAt = new DateTime(2026, 11, 5, 20, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                ActorTypeId = ActorTypeCode.User,
                ActorUserId = admin.Id,
                Action = AuditActions.RegistrationApproved,
                EntityType = "registrations",
                EntityId = registration.RegistrationId,
                Description = "Registration approved",
                OldValues = """{"Status":"pending"}""",
                NewValues = """{"Status":"confirmed"}""",
                OccurredAt = new DateTime(2026, 11, 5, 19, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                ActorTypeId = ActorTypeCode.User,
                ActorUserId = admin.Id,
                Action = AuditActions.AttendanceRecorded,
                EntityType = "attendance",
                EntityId = attendance.AttendanceId,
                Description = "Attendance recorded",
                NewValues = $$"""{"EventId":{{eventItem.EventId}},"RegistrationId":{{registration.RegistrationId}},"CheckInMethodId":1,"RecordedAt":"2026-11-05T13:30:00Z"}""",
                OccurredAt = new DateTime(2026, 11, 5, 18, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                ActorTypeId = ActorTypeCode.User,
                ActorUserId = admin.Id,
                Action = AuditActions.MemberEdited,
                EntityType = "members",
                EntityId = member.MemberId,
                Description = "Member updated",
                OldValues = memberOldValues,
                NewValues = memberNewValues,
                OccurredAt = new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                ActorTypeId = ActorTypeCode.User,
                ActorUserId = admin.Id,
                Action = AuditActions.EventUpdated,
                EntityType = "events",
                EntityId = 424242,
                Description = "Malformed snapshot",
                OldValues = "{not-json",
                NewValues = """{"Title":"Ghost"}""",
                OccurredAt = new DateTime(2026, 11, 5, 16, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                ActorTypeId = ActorTypeCode.System,
                ActorUserId = null,
                Action = "audit.test.unknown",
                EntityType = "mystery_records",
                EntityId = 777,
                Description = "Unknown entry",
                Reason = "Unknown reason.",
                OccurredAt = new DateTime(2026, 11, 5, 15, 0, 0, DateTimeKind.Utc)
            }
        };

        context.ActivityLogs.AddRange(logs);
        await context.SaveChangesAsync(ct);
    }

    private async Task<string> SeedUserAsync(string name, RoleCode role, string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var context = Context();
        context.Users.Add(new User
        {
            Email = email,
            Name = name,
            Password = hasher.Hash(password),
            RoleId = role,
            IsActive = true
        });
        await context.SaveChangesAsync(ct);
        return email;
    }

    private AsiBasecodeDBContext Context() =>
        new(new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    private static string RowFor(string html, string action, string? marker = null)
    {
        var rows = Regex.Matches(html, """<tr data-log-id="[^"]+">.*?</tr>""", RegexOptions.Singleline)
            .Select(match => match.Value)
            .Where(row => row.Contains(action, StringComparison.Ordinal) && (marker is null || row.Contains(marker, StringComparison.Ordinal)))
            .ToList();
        return Assert.Single(rows);
    }

    private static string GetLocationPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.ToString();
    }
}
