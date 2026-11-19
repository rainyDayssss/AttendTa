using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Security;
using Microsoft.Data.SqlClient;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>
/// Seeds accounts into an already-created, empty schema. Not a provisioning entry point:
/// the caller must independently establish that the server and database are disposable.
/// </summary>
internal static class DemoAccountSeeder
{
    // Ordered to match the stable private credential keys in DemoCredentials.AccountIds.
    internal static readonly (string Name, string Email)[] Accounts =
    [
        ("Alex Mercado", "alexmercado@gmail.com"), ("Jamie Villareal", "jamievillareal@gmail.com"),
        ("Jerhon Gabutan", "jerhongabutan@gmail.com"), ("Jasper Taguiam", "jaspertaguiam@gmail.com"),
        ("Jhon Talisic", "jhontalisic@gmail.com"), ("John Paul Sinco", "johnpaulsinco@gmail.com"),
        ("Seth Pinca", "sethpinca@gmail.com"), ("Riley Sarmiento", "rileysarmiento@gmail.com"),
        ("Drew Alonzo", "drewalonzo@gmail.com"), ("Casey Valdez", "caseyvaldez@gmail.com"),
        ("Jules Navarro", "julesnavarro@gmail.com"), ("Noel Soriano", "noelsoriano@gmail.com")
    ];

    // Invented supporting identities; these are not part of the presenter credential handover.
    private static readonly (string Name, string Email)[] SupportingAccounts =
    [
        ("Mika Alcaraz", "mika.alcaraz.demo@gmail.com"), ("Tomas Belmonte", "tomas.belmonte.demo@gmail.com"),
        ("Lea Cabigas", "lea.cabigas.demo@gmail.com"), ("Paolo Dela Cruz", "paolo.delacruz.demo@gmail.com"),
        ("Nina Escalante", "nina.escalante.demo@gmail.com"), ("Enzo Fajardo", "enzo.fajardo.demo@gmail.com"),
        ("Amara Galang", "amara.galang.demo@gmail.com"), ("Luis Hilario", "luis.hilario.demo@gmail.com"),
        ("Bea Ilagan", "bea.ilagan.demo@gmail.com"), ("Marco Jacinto", "marco.jacinto.demo@gmail.com"),
        ("Dana Kalaw", "dana.kalaw.demo@gmail.com"), ("Ivo Ledesma", "ivo.ledesma.demo@gmail.com"),
        ("Mira Malvar", "mira.malvar.demo@gmail.com"), ("Ramon Natividad", "ramon.natividad.demo@gmail.com"),
        ("Alyssa Ocampo", "alyssa.ocampo.demo@gmail.com"), ("Nico Pangan", "nico.pangan.demo@gmail.com"),
        ("Celia Quiambao", "celia.quiambao.demo@gmail.com"), ("Adrian Robles", "adrian.robles.demo@gmail.com"),
        ("Maya Salonga", "maya.salonga.demo@gmail.com"), ("Felix Trinidad", "felix.trinidad.demo@gmail.com"),
        ("Ina Ubaldo", "ina.ubaldo.demo@gmail.com"), ("Carlo Ventura", "carlo.ventura.demo@gmail.com"),
        ("Lara Yambao", "lara.yambao.demo@gmail.com"), ("Rafael Zamora", "rafael.zamora.demo@gmail.com"),
        ("Sofia Abad", "sofia.abad.demo@gmail.com"), ("Diego Bautista", "diego.bautista.demo@gmail.com"),
        ("Tala Coronel", "tala.coronel.demo@gmail.com"), ("Miguel Dimaano", "miguel.dimaano.demo@gmail.com")
    ];

    internal static async Task SeedAsync(SqlConnection connection, IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        // Validate again at this boundary, before hashing or opening a transaction.
        credentials = DemoCredentials.Parse(JsonSerializer.Serialize(credentials));
        var hashes = new string[DemoCredentials.AccountIds.Length];
        var hasher = new Argon2PasswordHasher();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < hashes.Length; i++)
        {
            if (!credentials.TryGetValue(DemoCredentials.AccountIds[i], out var password)
                || string.IsNullOrEmpty(password) || !unique.Add(password))
                throw new ArgumentException("Invalid demo credentials.");
            hashes[i] = hasher.Hash(password);
        }
        var applicantHash = hasher.Hash(credentials["applicant"]);
        // No shared or recoverable credential is assigned to the historical inactive account.
        var inactiveHash = hasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await using (var check = new SqlCommand("""
                IF EXISTS (SELECT 1 FROM dbo.users WITH (UPDLOCK, HOLDLOCK))
                   OR EXISTS (SELECT 1 FROM dbo.members WITH (UPDLOCK, HOLDLOCK))
                   OR EXISTS (SELECT 1 FROM dbo.account_requests WITH (UPDLOCK, HOLDLOCK))
                   OR EXISTS (SELECT 1 FROM dbo.events WITH (UPDLOCK, HOLDLOCK))
                   OR EXISTS (SELECT 1 FROM dbo.venues WITH (UPDLOCK, HOLDLOCK))
                   OR EXISTS (SELECT 1 FROM dbo.event_types WITH (UPDLOCK, HOLDLOCK))
                   OR EXISTS (SELECT 1 FROM dbo.activity_logs WITH (UPDLOCK, HOLDLOCK))
                    THROW 51200, 'Demo account seed requires a fresh empty schema.', 1;
                """, connection, transaction))
                await check.ExecuteNonQueryAsync(cancellationToken);

            int adminId = 0;
            for (var i = 0; i < hashes.Length; i++)
            {
                await using var user = new SqlCommand("""
                    INSERT dbo.users (full_name, email, password_hash, role_id, is_active, created_by)
                    OUTPUT INSERTED.user_id VALUES (@name, @email, @hash, @role, 1, @actor)
                    """, connection, transaction);
                user.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = Accounts[i].Name;
                user.Parameters.Add("@email", SqlDbType.NVarChar, 255).Value = Accounts[i].Email;
                user.Parameters.Add("@hash", SqlDbType.VarChar, 255).Value = hashes[i];
                user.Parameters.Add("@role", SqlDbType.TinyInt).Value = i == 0 ? 1 : i == 1 ? 2 : 3;
                user.Parameters.Add("@actor", SqlDbType.Int).Value = i == 0 ? DBNull.Value : adminId;
                var userId = (int)await user.ExecuteScalarAsync(cancellationToken);
                if (i == 0) { adminId = userId; continue; }

                await using var member = new SqlCommand("""
                    INSERT dbo.members (user_id, student_number, course, year_level, section,
                        mobile_number, membership_type, membership_status_id, created_by)
                    VALUES (@user, @student, @course, @year, @section, @mobile, @membership, 1, @actor)
                    """, connection, transaction);
                member.Parameters.Add("@user", SqlDbType.Int).Value = userId;
                member.Parameters.Add("@student", SqlDbType.NVarChar, 50).Value = (24000000 + i).ToString();
                member.Parameters.Add("@course", SqlDbType.NVarChar, 100).Value = i % 2 == 0 ? "BSCS" : "BSIT";
                member.Parameters.Add("@year", SqlDbType.Int).Value = 1 + (i % 4);
                member.Parameters.Add("@section", SqlDbType.NVarChar, 50).Value = ((char)('A' + i % 3)).ToString();
                member.Parameters.Add("@mobile", SqlDbType.VarChar, 30).Value = "0917000" + i.ToString("D4");
                member.Parameters.Add("@membership", SqlDbType.NVarChar, 100).Value = i == 1 ? "Officer" : "Regular";
                member.Parameters.Add("@actor", SqlDbType.Int).Value = adminId;
                await member.ExecuteNonQueryAsync(cancellationToken);
            }
            for (var i = 0; i < SupportingAccounts.Length; i++)
            {
                // Random credentials are never retained, returned, or written to logs.
                var hash = hasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
                await using var support = new SqlCommand("""
                    INSERT dbo.users (full_name, email, password_hash, role_id, is_active, created_by)
                    VALUES (@name, @email, @hash, 3, 1, @admin);
                    DECLARE @user int = CONVERT(int, SCOPE_IDENTITY());
                    INSERT dbo.members (user_id, student_number, course, year_level, section,
                        mobile_number, membership_type, membership_status_id, created_by)
                    VALUES (@user, @student, @course, @year, @section, @mobile, N'Regular', 1, @admin);
                    """, connection, transaction);
                support.Parameters.Add("@name", SqlDbType.NVarChar, 255).Value = SupportingAccounts[i].Name;
                support.Parameters.Add("@email", SqlDbType.NVarChar, 255).Value = SupportingAccounts[i].Email;
                support.Parameters.Add("@hash", SqlDbType.VarChar, 255).Value = hash;
                support.Parameters.Add("@admin", SqlDbType.Int).Value = adminId;
                support.Parameters.Add("@student", SqlDbType.NVarChar, 50).Value = (24000200 + i).ToString();
                support.Parameters.Add("@course", SqlDbType.NVarChar, 100).Value = i % 2 == 0 ? "BSCS" : "BSIT";
                support.Parameters.Add("@year", SqlDbType.Int).Value = 1 + i % 4;
                support.Parameters.Add("@section", SqlDbType.NVarChar, 50).Value = ((char)('A' + i % 3)).ToString();
                support.Parameters.Add("@mobile", SqlDbType.VarChar, 30).Value = "0918000" + i.ToString("D4");
                await support.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var audit = new SqlCommand("""
                INSERT dbo.activity_logs (actor_type_id, action, entity_type, entity_id, description)
                VALUES (2, 'user.provisioned', 'users', @id, N'Provisioned initial demo administrator.')
                """, connection, transaction))
            {
                audit.Parameters.Add("@id", SqlDbType.Int).Value = adminId;
                await audit.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var scenario = new SqlCommand("""
                DECLARE @now datetime2(3) = SYSUTCDATETIME();
                INSERT dbo.account_requests (student_number, full_name, email, password_hash, course,
                    year_level, section, mobile_number, account_request_status_id, requested_at)
                VALUES (N'24000099', N'Taylor Ramos', N'applicant@gmail.com', @applicantHash,
                    N'BSIT', 2, N'C', '09170000099', 1, DATEADD(hour,-4,@now));
                INSERT dbo.event_types (type_name, description, created_by)
                VALUES (N'Seminar', N'Presentations and workshops.', @admin),
                       (N'Outreach', N'Community service.', @admin),
                       (N'Meeting', N'Organization business.', @admin),
                       (N'Competition', N'Student competitions.', @admin),
                       (N'General Assembly', N'Organization-wide gathering.', @admin),
                       (N'Training', N'Practical skills.', @admin);
                INSERT dbo.venues (room_name, capacity, location, image_path, created_by)
                VALUES (N'Covered Court', 250, N'UC Campus, athletics area',
                         N'uploads/venues/5e6f7a8b9c0d41e2f3a4b5c6d7e8f9a0.png', @admin),
                       (N'UC–Main Jones Building Auditorium', 300, N'UC–Main Jones Building (fictional demo details)',
                         N'uploads/venues/aaaaaaaaaaaa4aaa8aaaaaaaaaaaaaaa.png', @admin),
                       (N'Doña Alicia Gotianuy Audio-Visual Room (AVR)', 100, N'UC–Main (fictional demo details)',
                         N'uploads/venues/bbbbbbbbbbbb4bbb8bbbbbbbbbbbbbbb.png', @admin),
                       (N'room 530-ABC', 60, N'UC–Main (fictional demo details)',
                         N'uploads/venues/cccccccccccc4ccc8ccccccccccccccc.png', @admin);
                INSERT dbo.venues (room_name, capacity, location, contact_person, image_path, created_by)
                VALUES (N'Cebu Coliseum', 2000, N'Cebu City (fictional demo details)',
                        N'Demo contact (fictional)', N'uploads/venues/dddddddddddd4ddd8ddddddddddddddd.png', @admin);
                DECLARE @jones int = (SELECT venue_id FROM dbo.venues WHERE room_name = N'UC–Main Jones Building Auditorium');
                DECLARE @avr int = (SELECT venue_id FROM dbo.venues WHERE room_name = N'Doña Alicia Gotianuy Audio-Visual Room (AVR)');
                DECLARE @room530 int = (SELECT venue_id FROM dbo.venues WHERE room_name = N'room 530-ABC');
                DECLARE @coliseum int = (SELECT venue_id FROM dbo.venues WHERE room_name = N'Cebu Coliseum');
                INSERT dbo.events (title, description, type_id, venue_id, starts_at, registration_deadline,
                    participant_limit, event_status_id, image_path, published_by, published_at, created_by)
                SELECT N'CCS AI and Responsible Computing Summit', N'CCS students explore practical AI tools, responsible use and computing project leadership.', type_id, @coliseum,
                    DATEADD(day,-3,@now), DATEADD(day,-5,@now), 120, 2,
                    N'uploads/events/1a2b3c4d5e6f47a8b9c0d1e2f3a4b5c6.png', @admin, DATEADD(day,-10,@now), @admin
                FROM dbo.event_types WHERE type_name = N'Seminar';
                INSERT dbo.events (title, description, type_id, venue_id, starts_at, registration_deadline,
                    participant_limit, event_status_id, image_path, published_by, published_at,
                    cancelled_by, cancelled_at, cancellation_reason, created_by)
                SELECT N'CCS Community Tech Outreach', N'CCS volunteers introduce basic digital safety and device skills to community learners.', type_id, @jones,
                    DATEADD(day,-1,@now), DATEADD(day,-3,@now), 80, 3,
                    N'uploads/events/2b3c4d5e6f7a48b9c0d1e2f3a4b5c6d7.png', @admin,
                    DATEADD(day,-8,@now), @admin, DATEADD(day,-2,@now), N'Venue unavailable.', @admin
                FROM dbo.event_types WHERE type_name = N'Outreach';
                INSERT dbo.events (title, description, type_id, venue_id, starts_at, registration_deadline,
                    participant_limit, event_status_id, image_path, published_by, published_at, created_by)
                SELECT N'CCS Computing Community Assembly', N'CCS members share project updates, committee plans and opportunities for the computing community.', type_id, @jones,
                    DATEADD(day,2,@now), DATEADD(day,1,@now), 100, 2,
                    N'uploads/events/3c4d5e6f7a8b49c0d1e2f3a4b5c6d7e8.png', @admin, DATEADD(day,-2,@now), @admin
                FROM dbo.event_types WHERE type_name = N'General Assembly';
                DECLARE @summit int = (SELECT event_id FROM dbo.events WHERE title = N'CCS AI and Responsible Computing Summit');
                DECLARE @upcoming int = (SELECT event_id FROM dbo.events WHERE title = N'CCS Computing Community Assembly');
                DECLARE @cancelled int = (SELECT event_id FROM dbo.events WHERE title = N'CCS Community Tech Outreach');
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, reviewed_by, reviewed_at,
                    confirmed_by, confirmed_at, qr_token, created_by)
                SELECT m.member_id, @summit, 2, m.course, m.year_level, m.section, DATEADD(day,-7,@now),
                    @admin, DATEADD(day,-6,@now), @admin, DATEADD(day,-6,@now),
                    LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)), @admin
                FROM dbo.members m JOIN dbo.users u ON u.user_id = m.user_id WHERE u.role_id = 3;
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, reviewed_by, reviewed_at,
                    confirmed_by, confirmed_at, qr_token, created_by)
                SELECT m.member_id, @upcoming, 2, m.course, m.year_level, m.section, DATEADD(day,-1,@now),
                    @admin, @now, @admin, @now, LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)), @admin
                FROM dbo.members m JOIN dbo.users u ON u.user_id = m.user_id
                WHERE u.email IN (N'jerhongabutan@gmail.com', N'jaspertaguiam@gmail.com', N'jhontalisic@gmail.com');
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, reviewed_by, reviewed_at,
                    confirmed_by, confirmed_at, qr_token, created_by)
                SELECT m.member_id, @upcoming, 2, m.course, m.year_level, m.section,
                    DATEADD(hour,-30,@now), @admin, DATEADD(hour,-24,@now), @admin, DATEADD(hour,-24,@now),
                    LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)), @admin
                FROM dbo.members m WHERE m.student_number BETWEEN N'24000200' AND N'24000209';
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, created_by)
                SELECT m.member_id, @upcoming, 1, m.course, m.year_level, m.section,
                    DATEADD(hour,-5,@now), @admin
                FROM dbo.members m WHERE m.student_number BETWEEN N'24000210' AND N'24000213';
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, created_by)
                SELECT m.member_id, @upcoming, 1, m.course, m.year_level, m.section, DATEADD(hour,-6,@now), @admin
                FROM dbo.members m JOIN dbo.users u ON u.user_id = m.user_id WHERE u.email = N'johnpaulsinco@gmail.com';
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, cancelled_by, cancelled_at,
                    cancellation_reason, created_by)
                SELECT m.member_id, @cancelled, 4, m.course, m.year_level, m.section,
                    DATEADD(day,-5,@now), @admin, DATEADD(day,-2,@now), N'Event cancelled.', @admin
                FROM dbo.members m JOIN dbo.users u ON u.user_id = m.user_id
                WHERE u.email IN (N'jerhongabutan@gmail.com', N'jaspertaguiam@gmail.com', N'jhontalisic@gmail.com');

                -- A separate completed event supplies report and certificate-review history
                -- without consuming the summit's live completion demonstration.
                INSERT dbo.events (title, description, type_id, venue_id, starts_at, registration_deadline,
                    participant_limit, event_status_id, image_path, published_by, published_at,
                    check_in_opened_by, check_in_opened_at, check_in_closed_by, check_in_closed_at,
                    completed_by, completed_at, created_by, created_at)
                SELECT N'CCS Accessible Web Workshop',
                    N'A practical student workshop on inclusive web design and accessible interfaces.',
                    type_id, @avr, DATEADD(day,-14,@now), DATEADD(day,-16,@now), 40, 4,
                    N'uploads/events/4d4d4d4d4d4d44d4a4d4d4d4d4d4d4d4.png',
                    @admin, DATEADD(day,-24,@now), @admin, DATEADD(hour,-337,@now),
                    @admin, DATEADD(hour,-334,@now), @admin, DATEADD(hour,-333,@now),
                    @admin, DATEADD(day,-25,@now)
                FROM dbo.event_types WHERE type_name=N'Training';
                DECLARE @workshop int = (SELECT event_id FROM dbo.events WHERE title=N'CCS Accessible Web Workshop');
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, reviewed_by, reviewed_at,
                    confirmed_by, confirmed_at, qr_token, created_by, created_at)
                SELECT m.member_id, @workshop, 2, m.course, m.year_level, m.section,
                    DATEADD(day,-20,@now), @admin, DATEADD(day,-19,@now), @admin, DATEADD(day,-19,@now),
                    LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)), @admin, DATEADD(day,-20,@now)
                FROM dbo.members m JOIN dbo.users u ON u.user_id=m.user_id
                WHERE u.email IN (N'jerhongabutan@gmail.com', N'jaspertaguiam@gmail.com',
                    N'jhontalisic@gmail.com', N'johnpaulsinco@gmail.com', N'sethpinca@gmail.com')
                    OR (m.student_number BETWEEN N'24000200' AND N'24000219');
                INSERT dbo.attendance (registration_id, check_in_method_id, recorded_by, recorded_at,
                    invalidated_by, invalidated_at, invalidation_reason)
                SELECT r.registration_id, CASE WHEN u.email=N'jerhongabutan@gmail.com' THEN 1 ELSE 2 END,
                    @admin, DATEADD(hour,-336,@now),
                    CASE WHEN u.email=N'johnpaulsinco@gmail.com' THEN @admin END,
                    CASE WHEN u.email=N'johnpaulsinco@gmail.com' THEN DATEADD(hour,-335,@now) END,
                    CASE WHEN u.email=N'johnpaulsinco@gmail.com' THEN N'Duplicate check-in recorded in error.' END
                FROM dbo.registrations r JOIN dbo.members m ON m.member_id=r.member_id
                JOIN dbo.users u ON u.user_id=m.user_id
                WHERE r.event_id=@workshop AND u.email<>N'sethpinca@gmail.com'
                    AND (m.student_number NOT BETWEEN N'24000200' AND N'24000219'
                         OR CONVERT(int, m.student_number) % 4 <> 0);
                INSERT dbo.certificates (registration_id, certificate_status_id, reviewed_by, reviewed_at,
                    review_notes, created_by, created_at)
                SELECT r.registration_id,
                    CASE u.email WHEN N'jaspertaguiam@gmail.com' THEN 2
                                 WHEN N'jhontalisic@gmail.com' THEN 3 ELSE 1 END,
                    CASE WHEN u.email IN (N'jaspertaguiam@gmail.com',N'jhontalisic@gmail.com') THEN @admin END,
                    CASE WHEN u.email IN (N'jaspertaguiam@gmail.com',N'jhontalisic@gmail.com') THEN DATEADD(day,-13,@now) END,
                    CASE WHEN u.email=N'jhontalisic@gmail.com' THEN N'Participation requirement not met.' END,
                    @admin, DATEADD(hour,-333,@now)
                FROM dbo.registrations r JOIN dbo.members m ON m.member_id=r.member_id
                JOIN dbo.users u ON u.user_id=m.user_id
                WHERE r.event_id=@workshop AND u.email IN
                    (N'jerhongabutan@gmail.com',N'jaspertaguiam@gmail.com',N'jhontalisic@gmail.com');

                -- A full small-group session makes a genuine waitlist visible.
                INSERT dbo.events (title, description, type_id, venue_id, starts_at, registration_deadline,
                    participant_limit, event_status_id, image_path, published_by, published_at, created_by, created_at)
                SELECT N'CCS Coding Peer Mentoring Lab', N'A small-group coding and problem-solving session led by student peer mentors.',
                    type_id, @room530, DATEADD(day,8,@now), DATEADD(day,6,@now), 2, 2,
                    N'uploads/events/5e5e5e5e5e5e45e5a5e5e5e5e5e5e5e5.png',
                    @admin, DATEADD(day,-5,@now), @admin, DATEADD(day,-6,@now)
                FROM dbo.event_types WHERE type_name=N'Training';
                DECLARE @lab int = (SELECT event_id FROM dbo.events WHERE title=N'CCS Coding Peer Mentoring Lab');
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, reviewed_by, reviewed_at,
                    confirmed_by, confirmed_at, qr_token, created_by, created_at)
                SELECT m.member_id, @lab, CASE WHEN u.email=N'sethpinca@gmail.com' THEN 3 ELSE 2 END,
                    m.course, m.year_level, m.section, DATEADD(day,-4,@now), @admin, DATEADD(day,-3,@now),
                    CASE WHEN u.email<>N'sethpinca@gmail.com' THEN @admin END,
                    CASE WHEN u.email<>N'sethpinca@gmail.com' THEN DATEADD(day,-3,@now) END,
                    CASE WHEN u.email<>N'sethpinca@gmail.com'
                        THEN LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)) END,
                    @admin, DATEADD(day,-4,@now)
                FROM dbo.members m JOIN dbo.users u ON u.user_id=m.user_id
                WHERE u.email IN (N'jerhongabutan@gmail.com',N'jaspertaguiam@gmail.com',N'sethpinca@gmail.com');

                -- Fixed 2026 showcase dates; later setups deliberately retain these historical dates.
                INSERT dbo.events (title, description, type_id, venue_id, starts_at, registration_deadline,
                    participant_limit, event_status_id, image_path, published_by, published_at, created_by, created_at)
                SELECT v.title, v.description, t.type_id, CASE WHEN v.title=N'CCS Game Development Jam' THEN @avr ELSE @room530 END, v.starts_at,
                    DATEADD(day,-2,v.starts_at), v.capacity, 2, v.image_path,
                    @admin, DATEADD(day,-3,@now), @admin, DATEADD(day,-4,@now)
                FROM (VALUES
                    (N'CCS Cybersecurity Capture the Flag', N'Teams solve beginner-friendly security puzzles in a safe, fictional lab.', N'Competition', CONVERT(datetime2(3),'2026-11-25T09:00:00'), 60, N'uploads/events/6f6f6f6f6f6f46f6a6f6f6f6f6f6f6f6.png'),
                    (N'CCS Data Science and AI Studio', N'Students practice preparing sample data and evaluating simple machine learning models.', N'Training', CONVERT(datetime2(3),'2026-11-28T09:00:00'), 50, N'uploads/events/7a7a7a7a7a7a47a7a7a7a7a7a7a7a7a7.png'),
                    (N'CCS Game Development Jam', N'Small teams prototype original games and share their creative process.', N'Competition', CONVERT(datetime2(3),'2026-12-02T09:00:00'), 80, N'uploads/events/8b8b8b8b8b8b48b8a8b8b8b8b8b8b8b8.png')
                ) v(title,description,type_name,starts_at,capacity,image_path)
                JOIN dbo.event_types t ON t.type_name=v.type_name;
                INSERT dbo.registrations (member_id, event_id, registration_status_id, course_snapshot,
                    year_level_snapshot, section_snapshot, submitted_at, reviewed_by, reviewed_at,
                    confirmed_by, confirmed_at, qr_token, created_by, created_at)
                SELECT m.member_id, e.event_id, 2, m.course, m.year_level, m.section,
                    DATEADD(day,-2,@now), @admin, DATEADD(day,-1,@now), @admin, DATEADD(day,-1,@now),
                    LOWER(CONVERT(varchar(64), CRYPT_GEN_RANDOM(32), 2)), @admin, DATEADD(day,-2,@now)
                FROM dbo.members m CROSS JOIN dbo.events e
                WHERE m.student_number BETWEEN N'24000214' AND N'24000219'
                  AND e.title IN (N'CCS Cybersecurity Capture the Flag', N'CCS Data Science and AI Studio', N'CCS Game Development Jam');

                -- Historical access examples are separate from every presenter login.
                INSERT dbo.users (full_name, email, password_hash, role_id, is_active, created_by, created_at)
                VALUES (N'Morgan Dizon', N'morgan.dizon@example.invalid', @inactiveHash, 3, 0,
                        @admin, DATEADD(day,-90,@now));
                DECLARE @inactiveUser int = CONVERT(int, SCOPE_IDENTITY());
                INSERT dbo.members (user_id, student_number, course, year_level, section,
                    membership_type, membership_status_id, created_by, created_at)
                VALUES (@inactiveUser, N'23000125', N'BSIT', 4, N'B', N'Regular', 2,
                        @admin, DATEADD(day,-90,@now));
                INSERT dbo.account_requests (student_number, full_name, email, password_hash, course,
                    year_level, section, account_request_status_id, requested_at, reviewed_by,
                    reviewed_at, review_notes, created_user_id)
                VALUES (N'23000125', N'Morgan Dizon', N'morgan.dizon@example.invalid', NULL,
                        N'BSIT', 4, N'B', 2, DATEADD(day,-92,@now), @admin,
                        DATEADD(day,-90,@now), N'Membership approved.', @inactiveUser),
                       (N'24000126', N'Robin Salcedo', N'robin.salcedo@example.invalid', NULL,
                        N'BSCS', 2, N'A', 3, DATEADD(day,-12,@now), @admin,
                        DATEADD(day,-11,@now), N'Please submit a legible enrollment record.', NULL);

                -- Backdate the fictional records consistently with their event histories.
                UPDATE dbo.users SET created_at=DATEADD(day,-100,@now) WHERE user_id<>@inactiveUser;
                UPDATE dbo.members SET created_at=DATEADD(day,-99,@now) WHERE user_id<>@inactiveUser;
                UPDATE dbo.event_types SET created_at=DATEADD(day,-30,@now);
                UPDATE dbo.venues SET created_at=DATEADD(day,-30,@now);
                UPDATE dbo.events SET created_at=DATEADD(day,-1,published_at)
                    WHERE event_id IN (@summit,@upcoming,@cancelled);
                UPDATE dbo.registrations SET created_at=submitted_at
                    WHERE event_id IN (@summit,@upcoming,@cancelled);

                INSERT dbo.activity_logs (actor_type_id, actor_user_id, action, entity_type, entity_id, description, occurred_at)
                VALUES (1, @admin, 'event.published', 'events', @workshop,
                            N'Published CCS Accessible Web Workshop.', DATEADD(day,-24,@now)),
                       (1, @admin, 'event.completed', 'events', @workshop,
                            N'Completed CCS Accessible Web Workshop.', DATEADD(hour,-333,@now)),
                       (1, @admin, 'event.published', 'events', @lab,
                            N'Published CCS Coding Peer Mentoring Lab.', DATEADD(day,-5,@now));
                """, connection, transaction))
            {
                scenario.Parameters.Add("@admin", SqlDbType.Int).Value = adminId;
                scenario.Parameters.Add("@applicantHash", SqlDbType.VarChar, 255).Value = applicantHash;
                scenario.Parameters.Add("@inactiveHash", SqlDbType.VarChar, 255).Value = inactiveHash;
                await scenario.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
