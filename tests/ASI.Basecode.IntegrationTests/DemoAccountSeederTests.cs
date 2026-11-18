using ASI.Basecode.Services.Security;
using ASI.Basecode.WebApp.Demo;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoAccountSeederTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task FreshSchemaGetsDistinctUsableLinkedAccountsAndRepeatRefusesWithoutMutation()
    {
        var credentials = DemoCredentials.CredentialIds
            .Select((id, index) => (id, password: $"ExamplePassword{index}"))
            .ToDictionary(pair => pair.id, pair => pair.password);
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await DemoAccountSeeder.SeedAsync(connection, credentials, TestContext.Current.CancellationToken);

        var accounts = new Dictionary<string, (string Key, string Name)>
        {
            ["alexmercado@gmail.com"] = ("admin", "Alex Mercado"),
            ["jamievillareal@gmail.com"] = ("officer", "Jamie Villareal"),
            ["jerhongabutan@gmail.com"] = ("gabutan", "Jerhon Gabutan"),
            ["jaspertaguiam@gmail.com"] = ("taguiam", "Jasper Taguiam"),
            ["jhontalisic@gmail.com"] = ("talisic", "Jhon Talisic"),
            ["johnpaulsinco@gmail.com"] = ("sinco", "John Paul Sinco"),
            ["sethpinca@gmail.com"] = ("pinca", "Seth Pinca"),
            ["rileysarmiento@gmail.com"] = ("member06", "Riley Sarmiento"),
            ["drewalonzo@gmail.com"] = ("member07", "Drew Alonzo"),
            ["caseyvaldez@gmail.com"] = ("member08", "Casey Valdez"),
            ["julesnavarro@gmail.com"] = ("member09", "Jules Navarro"),
            ["noelsoriano@gmail.com"] = ("member10", "Noel Soriano")
        };
        var hasher = new Argon2PasswordHasher();
        await using (var command = new SqlCommand("""
            SELECT u.email, u.password_hash, u.role_id,
                   u.full_name,
                   (SELECT COUNT(*) FROM dbo.members m WHERE m.user_id = u.user_id)
            FROM dbo.users u WHERE u.email NOT LIKE '%.demo@gmail.com' AND u.is_active=1
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            var count = 0;
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                var email = reader.GetString(0);
                Assert.True(accounts.TryGetValue(email, out var account));
                var id = account.Key;
                Assert.Equal(account.Name, reader.GetString(3));
                Assert.True(hasher.Verify(credentials[id], reader.GetString(1)));
                Assert.Equal(id == "admin" ? 0 : 1, reader.GetInt32(4));
                Assert.Equal(id == "admin" ? 1 : id == "officer" ? 2 : 3, reader.GetByte(2));
                count++;
            }
            Assert.Equal(accounts.Count, count);
        }

        var error = await Assert.ThrowsAsync<SqlException>(() =>
            DemoAccountSeeder.SeedAsync(connection, credentials, TestContext.Current.CancellationToken));
        Assert.Equal(51200, error.Number);
        await using var countCommand = new SqlCommand("SELECT COUNT(*) FROM dbo.users", connection);
        Assert.Equal(41, Convert.ToInt32(await countCommand.ExecuteScalarAsync(TestContext.Current.CancellationToken)));
        await using var scenario = new SqlCommand("""
            SELECT (SELECT COUNT(*) FROM dbo.events),
                   (SELECT COUNT(*) FROM dbo.registrations WHERE registration_status_id = 2),
                   (SELECT COUNT(*) FROM dbo.registrations WHERE registration_status_id = 1),
                   (SELECT COUNT(*) FROM dbo.registrations WHERE registration_status_id = 4),
                   (SELECT COUNT(*) FROM dbo.venues WHERE image_path LIKE 'uploads/venues/%.png'),
                   (SELECT COUNT(DISTINCT image_path) FROM dbo.events)
            """, connection);
        await using var scenarioReader = await scenario.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await scenarioReader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(8, scenarioReader.GetInt32(0));
        Assert.Equal(96, scenarioReader.GetInt32(1));
        Assert.Equal(5, scenarioReader.GetInt32(2));
        Assert.Equal(3, scenarioReader.GetInt32(3));
        Assert.Equal(5, scenarioReader.GetInt32(4));
        Assert.Equal(8, scenarioReader.GetInt32(5));
        await scenarioReader.DisposeAsync();
        await using (var venues = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM dbo.venues WHERE
                (room_name=N'UC–Main Jones Building Auditorium' AND capacity=300 AND image_path=N'uploads/venues/aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa.png')
                OR (room_name=N'Doña Alicia Gotianuy Audio-Visual Room (AVR)' AND capacity=100 AND image_path=N'uploads/venues/bbbbbbbbbbbb4bbb8bbbbbbbbbbbbbbb.png')
                OR (room_name=N'room 530-ABC' AND capacity=60 AND image_path=N'uploads/venues/cccccccccccc4ccc8ccccccccccccccc.png')
                OR (room_name=N'Cebu Coliseum' AND capacity=2000 AND contact_person=N'Demo contact (fictional)' AND image_path=N'uploads/venues/dddddddddddd4ddd8ddddddddddddddd.png')),
              (SELECT COUNT(*) FROM dbo.events e JOIN dbo.venues v ON v.venue_id=e.venue_id
                WHERE (e.title=N'CCS Computing Community Assembly' AND v.room_name=N'UC–Main Jones Building Auditorium')
                   OR (e.title=N'CCS Accessible Web Workshop' AND v.room_name=N'Doña Alicia Gotianuy Audio-Visual Room (AVR)')
                   OR (e.title=N'CCS Cybersecurity Capture the Flag' AND v.room_name=N'room 530-ABC')
                   OR (e.title=N'CCS AI and Responsible Computing Summit' AND v.room_name=N'Cebu Coliseum')
                   OR (e.title=N'CCS Coding Peer Mentoring Lab' AND v.room_name=N'room 530-ABC' AND e.participant_limit=2)
                   OR (e.title=N'CCS Data Science and AI Studio' AND v.room_name=N'room 530-ABC' AND e.participant_limit=50)
                   OR (e.title=N'CCS Game Development Jam' AND v.room_name=N'Doña Alicia Gotianuy Audio-Visual Room (AVR)' AND e.participant_limit=80)
                   OR (e.title=N'CCS Community Tech Outreach' AND v.room_name=N'UC–Main Jones Building Auditorium' AND e.participant_limit=80 AND v.capacity=300)),
              (SELECT COUNT(*) FROM dbo.events e JOIN dbo.venues v ON v.venue_id=e.venue_id
                WHERE e.participant_limit > v.capacity),
              (SELECT COUNT(*) FROM dbo.venues WHERE room_name=N'University Auditorium')
            """, connection))
        await using (var venueReader = await venues.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            Assert.True(await venueReader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(4, venueReader.GetInt32(0));
            Assert.Equal(8, venueReader.GetInt32(1));
            Assert.Equal(0, venueReader.GetInt32(2));
            Assert.Equal(0, venueReader.GetInt32(3));
        }
        await using (var eventCoverage = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM dbo.events WHERE title IN
                (N'CCS Cybersecurity Capture the Flag', N'CCS Data Science and AI Studio',
                 N'CCS Game Development Jam') AND event_status_id=2
                 AND starts_at>registration_deadline
                 AND ((title=N'CCS Cybersecurity Capture the Flag' AND starts_at='2026-11-25T09:00:00')
                   OR (title=N'CCS Data Science and AI Studio' AND starts_at='2026-11-28T09:00:00')
                   OR (title=N'CCS Game Development Jam' AND starts_at='2026-12-02T09:00:00'))),
              (SELECT COUNT(*) FROM dbo.registrations r JOIN dbo.events e ON e.event_id=r.event_id
                WHERE e.title IN (N'CCS Cybersecurity Capture the Flag', N'CCS Data Science and AI Studio',
                                  N'CCS Game Development Jam') AND r.registration_status_id=2),
              (SELECT COUNT(*) FROM dbo.events WHERE image_path LIKE 'uploads/events/%.png'),
              (SELECT COUNT(*) FROM dbo.events WHERE is_check_in_open=1)
            """, connection))
        await using (var eventReader = await eventCoverage.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            Assert.True(await eventReader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(3, eventReader.GetInt32(0));
            Assert.Equal(18, eventReader.GetInt32(1));
            Assert.Equal(8, eventReader.GetInt32(2));
            Assert.Equal(0, eventReader.GetInt32(3));
        }
        await using (var datedEvents = new SqlCommand("""
            SELECT e.title, COUNT(r.registration_id)
            FROM dbo.events e LEFT JOIN dbo.registrations r ON r.event_id=e.event_id AND r.registration_status_id=2
            WHERE e.title IN (N'CCS Cybersecurity Capture the Flag', N'CCS Data Science and AI Studio', N'CCS Game Development Jam')
            GROUP BY e.title
            """, connection))
        await using (var datedReader = await datedEvents.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            var rows = 0;
            while (await datedReader.ReadAsync(TestContext.Current.CancellationToken))
            {
                Assert.Equal(6, datedReader.GetInt32(1));
                rows++;
            }
            Assert.Equal(3, rows);
        }
        await using var applicant = new SqlCommand("""
            SELECT password_hash FROM dbo.account_requests
            WHERE email = 'applicant@gmail.com' AND student_number = '24000099'
              AND account_request_status_id = 1
            """, connection);
        var requestHash = (string?)await applicant.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(requestHash);
        Assert.True(hasher.Verify(credentials["applicant"], requestHash));
        await using var accessHistory = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM dbo.users u JOIN dbo.members m ON m.user_id=u.user_id
               WHERE u.email=N'morgan.dizon@example.invalid' AND u.is_active=0 AND m.membership_status_id=2),
              (SELECT COUNT(*) FROM dbo.account_requests ar JOIN dbo.users u ON u.user_id=ar.created_user_id
               WHERE ar.email=u.email AND ar.account_request_status_id=2 AND ar.password_hash IS NULL
                 AND ar.reviewed_by IS NOT NULL AND ar.reviewed_at>=ar.requested_at),
              (SELECT COUNT(*) FROM dbo.account_requests WHERE email=N'robin.salcedo@example.invalid'
               AND account_request_status_id=3 AND password_hash IS NULL AND created_user_id IS NULL
               AND reviewed_by IS NOT NULL AND reviewed_at>=requested_at),
              (SELECT COUNT(*) FROM dbo.users WHERE is_active=1)
            """, connection);
        await using (var accessReader = await accessHistory.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            Assert.True(await accessReader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, accessReader.GetInt32(0));
            Assert.Equal(1, accessReader.GetInt32(1));
            Assert.Equal(1, accessReader.GetInt32(2));
            Assert.Equal(40, accessReader.GetInt32(3));
        }

        await using var prerequisites = new SqlCommand("""
            SELECT
                (SELECT COUNT(*) FROM dbo.registrations r JOIN dbo.events e ON e.event_id=r.event_id
                 WHERE e.title=N'CCS AI and Responsible Computing Summit' AND e.event_status_id=2
                   AND e.starts_at<SYSUTCDATETIME() AND r.registration_status_id=2
                   AND LEN(r.qr_token)=64),
                (SELECT COUNT(DISTINCT r.qr_token) FROM dbo.registrations r
                 WHERE r.registration_status_id=2),
                (SELECT COUNT(*) FROM dbo.events WHERE title=N'CCS Computing Community Assembly'
                   AND event_status_id=2 AND starts_at>SYSUTCDATETIME()
                   AND registration_deadline>SYSUTCDATETIME() AND is_check_in_open=0),
                (SELECT COUNT(*) FROM dbo.events WHERE title=N'CCS Community Tech Outreach'
                   AND event_status_id=3 AND cancelled_at IS NOT NULL),
                (SELECT COUNT(*) FROM dbo.attendance),
                (SELECT COUNT(*) FROM dbo.certificates)
            """, connection);
        await using var prerequisitesReader = await prerequisites.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await prerequisitesReader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(38, prerequisitesReader.GetInt32(0));
        Assert.Equal(96, prerequisitesReader.GetInt32(1));
        Assert.Equal(1, prerequisitesReader.GetInt32(2));
        Assert.Equal(1, prerequisitesReader.GetInt32(3));
        Assert.Equal(19, prerequisitesReader.GetInt32(4));
        Assert.Equal(3, prerequisitesReader.GetInt32(5));
        await prerequisitesReader.DisposeAsync();
        await using var history = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM dbo.registrations r JOIN dbo.events e ON e.event_id=r.event_id
               WHERE e.title=N'CCS Coding Peer Mentoring Lab' AND r.registration_status_id=3
                 AND r.reviewed_at IS NOT NULL AND r.qr_token IS NULL),
              (SELECT COUNT(*) FROM dbo.attendance WHERE invalidated_at IS NOT NULL),
              (SELECT COUNT(DISTINCT certificate_status_id) FROM dbo.certificates
               WHERE certificate_status_id IN (1,2,3)),
              (SELECT COUNT(*) FROM dbo.certificates WHERE certificate_status_id=4 OR file_path IS NOT NULL),
              (SELECT COUNT(*) FROM dbo.events WHERE title=N'CCS Accessible Web Workshop'
               AND event_status_id=4 AND completed_at>starts_at)
            """, connection);
        await using var historyReader = await history.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await historyReader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, historyReader.GetInt32(0));
        Assert.Equal(1, historyReader.GetInt32(1));
        Assert.Equal(3, historyReader.GetInt32(2));
        Assert.Equal(0, historyReader.GetInt32(3));
        Assert.Equal(1, historyReader.GetInt32(4));
        await historyReader.DisposeAsync();
        await using var profiles = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM dbo.users WHERE email LIKE '%@gmail.com'),
              (SELECT COUNT(*) FROM dbo.users WHERE full_name IN
                (N'Jerhon Gabutan', N'Jasper Taguiam', N'Jhon Talisic', N'John Paul Sinco', N'Seth Pinca')),
              (SELECT COUNT(DISTINCT course) FROM dbo.members),
              (SELECT COUNT(DISTINCT year_level) FROM dbo.members)
            """, connection);
        await using var profileReader = await profiles.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await profileReader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(40, profileReader.GetInt32(0));
        Assert.Equal(5, profileReader.GetInt32(1));
        Assert.Equal(2, profileReader.GetInt32(2));
        Assert.Equal(4, profileReader.GetInt32(3));
        await profileReader.DisposeAsync();
        await using (var courses = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM dbo.members WHERE course IS NULL OR course NOT IN (N'BSCS', N'BSIT')),
              (SELECT COUNT(*) FROM dbo.account_requests WHERE course IS NULL OR course NOT IN (N'BSCS', N'BSIT')),
              (SELECT COUNT(*) FROM dbo.registrations WHERE course_snapshot IS NULL OR course_snapshot NOT IN (N'BSCS', N'BSIT')),
              (SELECT COUNT(*) FROM dbo.registrations r JOIN dbo.members m ON m.member_id=r.member_id
               WHERE r.course_snapshot<>m.course),
              (SELECT COUNT(DISTINCT course) FROM dbo.account_requests),
              (SELECT COUNT(DISTINCT course_snapshot) FROM dbo.registrations)
            """, connection))
        await using (var courseReader = await courses.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            Assert.True(await courseReader.ReadAsync(TestContext.Current.CancellationToken));
            for (var i = 0; i < 4; i++) Assert.Equal(0, courseReader.GetInt32(i));
            Assert.Equal(2, courseReader.GetInt32(4));
            Assert.Equal(2, courseReader.GetInt32(5));
        }
        await using (var invariants = new SqlCommand("""
            SELECT (SELECT COUNT(*) FROM dbo.members),
                   (SELECT COUNT(DISTINCT password_hash) FROM dbo.users),
                   (SELECT COUNT(*) FROM dbo.users),
                   (SELECT COUNT(*) FROM dbo.events e WHERE
                       (SELECT COUNT(*) FROM dbo.registrations r
                        WHERE r.event_id=e.event_id AND r.registration_status_id=2) > e.participant_limit),
                   (SELECT COUNT(*) FROM dbo.users u JOIN dbo.members m ON m.user_id=u.user_id
                    WHERE u.email LIKE '%.demo@gmail.com' AND u.is_active=1 AND m.membership_status_id=1)
            """, connection))
        await using (var invariantReader = await invariants.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            Assert.True(await invariantReader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(40, invariantReader.GetInt32(0));
            Assert.Equal(invariantReader.GetInt32(2), invariantReader.GetInt32(1));
            Assert.Equal(0, invariantReader.GetInt32(3));
            Assert.Equal(28, invariantReader.GetInt32(4));
        }
        await using var associations = new SqlCommand("""
            SELECT u.email, r.registration_status_id
            FROM dbo.registrations r
            JOIN dbo.members m ON m.member_id = r.member_id
            JOIN dbo.users u ON u.user_id = m.user_id
            JOIN dbo.events e ON e.event_id = r.event_id
            WHERE e.title = N'CCS Computing Community Assembly'
              AND u.email IN (N'jerhongabutan@gmail.com', N'jaspertaguiam@gmail.com',
                              N'jhontalisic@gmail.com', N'johnpaulsinco@gmail.com')
            """, connection);
        await using var associationReader = await associations.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var registrations = new Dictionary<string, int>();
        while (await associationReader.ReadAsync(TestContext.Current.CancellationToken))
            registrations.Add(associationReader.GetString(0), Convert.ToInt32(associationReader.GetValue(1)));
        Assert.Equal(4, registrations.Count);
        Assert.Equal(2, registrations["jerhongabutan@gmail.com"]);
        Assert.Equal(2, registrations["jaspertaguiam@gmail.com"]);
        Assert.Equal(2, registrations["jhontalisic@gmail.com"]);
        Assert.Equal(1, registrations["johnpaulsinco@gmail.com"]);
        await associationReader.DisposeAsync();

        // Exercise the real authenticated read surfaces against the same freshly seeded database.
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var member = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await SignInAsync(admin, "alexmercado@gmail.com", credentials["admin"]);
        await SignInAsync(member, "jerhongabutan@gmail.com", credentials["gabutan"]);
        var ct = TestContext.Current.CancellationToken;
        var eventList = WebUtility.HtmlDecode(await admin.GetStringAsync("/Event/Index?pageSize=8", ct));
        foreach (var (title, venue) in new[]
        {
            ("CCS Computing Community Assembly", "UC–Main Jones Building Auditorium"),
            ("CCS Accessible Web Workshop", "Doña Alicia Gotianuy Audio-Visual Room (AVR)"),
            ("CCS Cybersecurity Capture the Flag", "room 530-ABC"),
            ("CCS AI and Responsible Computing Summit", "Cebu Coliseum"),
            ("CCS Coding Peer Mentoring Lab", "room 530-ABC"),
            ("CCS Data Science and AI Studio", "room 530-ABC"),
            ("CCS Game Development Jam", "Doña Alicia Gotianuy Audio-Visual Room (AVR)"),
            ("CCS Community Tech Outreach", "UC–Main Jones Building Auditorium")
        })
        {
            Assert.Matches($@"(?s)<tr data-event-id=""\d+"">(?:(?!</tr>).)*{Regex.Escape(title)}(?:(?!</tr>).)*<td data-label=""Venue"">{Regex.Escape(venue)}</td>", eventList);
        }
        Assert.Contains("CCS Community Tech Outreach", eventList);
        Assert.DoesNotContain("University Auditorium", eventList);
        var venueList = WebUtility.HtmlDecode(await admin.GetStringAsync("/Venue/Index?pageSize=20", ct));
        Assert.DoesNotContain("University Auditorium", venueList);
        Assert.Contains("UC–Main Jones Building Auditorium", venueList);
        Assert.Contains("Doña Alicia Gotianuy Audio-Visual Room (AVR)", venueList);
        Assert.Contains("room 530-ABC", venueList);
        Assert.Contains("Cebu Coliseum", venueList);
        Assert.Contains("Taylor Ramos", await admin.GetStringAsync("/AccountRequest?status=pending", ct));
        Assert.Contains("Morgan Dizon", await admin.GetStringAsync("/AccountRequest?status=approved", ct));
        Assert.Contains("Robin Salcedo", await admin.GetStringAsync("/AccountRequest?status=rejected", ct));
        Assert.Contains("Morgan Dizon", await admin.GetStringAsync("/admin/accounts?search=Morgan", ct));
        Assert.Contains("Seth Pinca", await admin.GetStringAsync("/RegistrationReview/Index?status=waitlisted", ct));
        Assert.Contains("John Paul Sinco", await admin.GetStringAsync("/RegistrationReview/Index?status=pending", ct));
        Assert.Contains("CCS Accessible Web Workshop", await admin.GetStringAsync("/Reports/Participation", ct));
        Assert.Contains("CCS Accessible Web Workshop", await admin.GetStringAsync("/Certificates", ct));
        Assert.Contains("CCS Coding Peer Mentoring Lab", await member.GetStringAsync("/MyEvents/Registrations", ct));
        Assert.Contains("CCS Accessible Web Workshop", await member.GetStringAsync("/MyEvents/Attendance", ct));
        Assert.Contains("CCS Coding Peer Mentoring Lab", await member.GetStringAsync("/Passes", ct));
    }

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        var page = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(page, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.NotEmpty(token);
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(response.Headers, header => header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
            && header.Value.Any(value => value.StartsWith("AttendTa.Auth=", StringComparison.Ordinal)));
    }
}
