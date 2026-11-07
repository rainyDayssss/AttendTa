/*
   Seeds the repeatable demo starting state for the 8.15 rehearsal.

   Run only in AttendTaMVP_Test, AttendTa_IT_<guid>, or AttendTa_Demo, after
   database/seed-local-admin.sql has created the active administrator. The script is
   idempotent: re-running it keeps the same accounts, members, venues, events and
   registrations and re-anchors the event dates around the current UTC time.

   Supply the six sqlcmd variables; never pass a plaintext password:
     -v OfficerName="..." -v OfficerEmail="..." -v OfficerPasswordHash="<PHC hash>"
     -v MemberName="..."  -v MemberEmail="..."  -v MemberPasswordHash="<PHC hash>"

   The two hashes come from the built WebApp --hash-password mode. The same member hash
   is used for the supporting participant accounts so the presenter only needs one
   member credential. Nothing here stores or logs a plaintext password or the hash.

   The starting state is three published events:
     - Leadership Summit 2026   started, eight confirmed registrations (completed in the
                                in-memory demo store, so it opens with attendance,
                                certificate reviews and an issued certificate)
     - Community Outreach Drive started, two confirmed registrations (ready to complete)
     - Founders' Week Assembly  upcoming, three confirmed and one pending registration
                                (the check-in window is opened by the demo seed)

   The demo store (ASI.Basecode.WebApp/DemoData) reads these rows at start-up and never
   writes SQL, a private file or the Activity Log. Venue photos and event covers are
   copied into Common:SetupRoot by the pre-flight in
   _bmad-output/initiative-attendta/demo-script.md.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF DB_NAME() <> N'AttendTaMVP_Test'
   AND DB_NAME() <> N'AttendTa_Demo'
   AND (DB_NAME() NOT LIKE N'AttendTa_IT[_]%' OR LEN(DB_NAME()) <= LEN(N'AttendTa_IT_'))
    THROW 51030, 'This demo scenario seed may only run in AttendTaMVP_Test, AttendTa_IT_<guid>, or AttendTa_Demo.', 1;
GO

DECLARE @OfficerName nvarchar(255) = N'$(OfficerName)';
DECLARE @OfficerEmail nvarchar(255) = N'$(OfficerEmail)';
DECLARE @OfficerPasswordHash varchar(255) = '$(OfficerPasswordHash)';
DECLARE @MemberName nvarchar(255) = N'$(MemberName)';
DECLARE @MemberEmail nvarchar(255) = N'$(MemberEmail)';
DECLARE @MemberPasswordHash varchar(255) = '$(MemberPasswordHash)';

IF NULLIF(LTRIM(RTRIM(@OfficerName)), N'') IS NULL
   OR CHARINDEX(NCHAR(36) + N'(', @OfficerName) > 0
    THROW 51031, 'Supply OfficerName for the demo officer account.', 1;

IF NULLIF(LTRIM(RTRIM(@OfficerEmail)), N'') IS NULL
   OR CHARINDEX(NCHAR(36) + N'(', @OfficerEmail) > 0
   OR CHARINDEX(N'@', @OfficerEmail) <= 1
   OR CHARINDEX(N'.', @OfficerEmail, CHARINDEX(N'@', @OfficerEmail)) <= 0
    THROW 51032, 'Supply a valid OfficerEmail for the demo officer account.', 1;

IF NULLIF(LTRIM(RTRIM(@MemberName)), N'') IS NULL
   OR CHARINDEX(NCHAR(36) + N'(', @MemberName) > 0
    THROW 51033, 'Supply MemberName for the demo member account.', 1;

IF NULLIF(LTRIM(RTRIM(@MemberEmail)), N'') IS NULL
   OR CHARINDEX(NCHAR(36) + N'(', @MemberEmail) > 0
   OR CHARINDEX(N'@', @MemberEmail) <= 1
   OR CHARINDEX(N'.', @MemberEmail, CHARINDEX(N'@', @MemberEmail)) <= 0
    THROW 51034, 'Supply a valid MemberEmail for the demo member account.', 1;

IF NULLIF(LTRIM(RTRIM(@OfficerPasswordHash)), '') IS NULL
   OR CHARINDEX(CHAR(36) + '(', @OfficerPasswordHash) > 0
   OR @OfficerPasswordHash NOT LIKE '$argon2id$v=19$m=%'
   OR LEN(@OfficerPasswordHash) > 255
    THROW 51035, 'Supply an Argon2id OfficerPasswordHash from --hash-password; plaintext passwords are not accepted.', 1;

IF NULLIF(LTRIM(RTRIM(@MemberPasswordHash)), '') IS NULL
   OR CHARINDEX(CHAR(36) + '(', @MemberPasswordHash) > 0
   OR @MemberPasswordHash NOT LIKE '$argon2id$v=19$m=%'
   OR LEN(@MemberPasswordHash) > 255
    THROW 51036, 'Supply an Argon2id MemberPasswordHash from --hash-password; plaintext passwords are not accepted.', 1;

DECLARE @AdminUserId int;
SELECT TOP (1) @AdminUserId = [user_id]
FROM dbo.users
WHERE [role_id] = 1
  AND [is_active] = 1
ORDER BY [user_id];

IF @AdminUserId IS NULL
    THROW 51037, 'Run database/seed-local-admin.sql before seeding the demo scenario.', 1;

DECLARE @Now datetime2(3) = SYSUTCDATETIME();
DECLARE @ScenarioCreated bit = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ---- Officer account and member record ---------------------------------- */

    DECLARE @OfficerUserId int;
    SELECT @OfficerUserId = [user_id] FROM dbo.users WHERE [email] = @OfficerEmail;

    IF @OfficerUserId IS NULL
    BEGIN
        INSERT INTO dbo.users ([full_name], [email], [password_hash], [role_id], [is_active], [created_by])
        VALUES (@OfficerName, @OfficerEmail, @OfficerPasswordHash, 2, 1, @AdminUserId);
        SET @OfficerUserId = CAST(SCOPE_IDENTITY() AS int);
    END
    ELSE IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE [user_id] = @OfficerUserId AND [role_id] = 2 AND [is_active] = 1)
        THROW 51038, 'The supplied OfficerEmail already exists but is not an active officer; resolve it manually.', 1;

    DECLARE @OfficerMemberId int;
    SELECT @OfficerMemberId = [member_id] FROM dbo.members WHERE [user_id] = @OfficerUserId;

    IF @OfficerMemberId IS NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.members WHERE [student_number] = N'UC-2026-0101')
            THROW 51039, 'The scenario officer student number already belongs to another member; resolve it manually.', 1;

        INSERT INTO dbo.members ([user_id], [student_number], [course], [year_level], [section], [mobile_number], [membership_type], [membership_status_id], [created_by])
        VALUES (@OfficerUserId, N'UC-2026-0101', N'BS Information Technology', 4, N'A', N'0917-000-0101', N'Officer', 1, @AdminUserId);
        SET @OfficerMemberId = CAST(SCOPE_IDENTITY() AS int);
    END

    /* ---- Participant accounts and member records ---------------------------- */

    DECLARE @Participants TABLE
    (
        [Ordinal] int PRIMARY KEY,
        [FullName] nvarchar(255) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [StudentNumber] nvarchar(50) NOT NULL,
        [Course] nvarchar(100) NOT NULL,
        [YearLevel] int NOT NULL,
        [Section] nvarchar(50) NOT NULL,
        [MembershipType] nvarchar(100) NOT NULL
    );

    INSERT INTO @Participants ([Ordinal], [FullName], [Email], [StudentNumber], [Course], [YearLevel], [Section], [MembershipType])
    VALUES
        (1, @MemberName,      @MemberEmail,                 N'UC-2026-0102', N'BS Information Technology', 3, N'B', N'Regular'),
        (2, N'Joshua Reyes',  N'joshua.reyes@uc.edu.ph',    N'UC-2026-0103', N'BS Computer Science',       3, N'A', N'Regular'),
        (3, N'Angela Dela Cruz', N'angela.delacruz@uc.edu.ph', N'UC-2026-0104', N'BS Information Systems', 2, N'C', N'Regular'),
        (4, N'Mark Villanueva', N'mark.villanueva@uc.edu.ph', N'UC-2026-0105', N'BS Computer Science',     4, N'B', N'Regular'),
        (5, N'Sofia Mendoza', N'sofia.mendoza@uc.edu.ph',   N'UC-2026-0106', N'BS Information Technology', 2, N'A', N'Regular'),
        (6, N'Daniel Navarro', N'daniel.navarro@uc.edu.ph', N'UC-2026-0107', N'BS Computer Science',       1, N'C', N'Regular'),
        (7, N'Beatrice Lim',  N'beatrice.lim@uc.edu.ph',    N'UC-2026-0108', N'BS Information Systems',    3, N'B', N'Regular'),
        (8, N'Paolo Gutierrez', N'paolo.gutierrez@uc.edu.ph', N'UC-2026-0109', N'BS Information Technology', 4, N'A', N'Regular');

    DECLARE @MemberIds TABLE ([Ordinal] int PRIMARY KEY, [UserId] int NOT NULL, [MemberId] int NOT NULL);

    DECLARE @Ordinal int, @FullName nvarchar(255), @Email nvarchar(255), @StudentNumber nvarchar(50),
            @Course nvarchar(100), @YearLevel int, @Section nvarchar(50), @MembershipType nvarchar(100),
            @ParticipantUserId int, @ParticipantMemberId int;

    DECLARE participant_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT [Ordinal], [FullName], [Email], [StudentNumber], [Course], [YearLevel], [Section], [MembershipType]
        FROM @Participants
        ORDER BY [Ordinal];

    OPEN participant_cursor;
    FETCH NEXT FROM participant_cursor INTO @Ordinal, @FullName, @Email, @StudentNumber, @Course, @YearLevel, @Section, @MembershipType;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @ParticipantUserId = NULL;
        SET @ParticipantMemberId = NULL;

        SELECT @ParticipantUserId = [user_id] FROM dbo.users WHERE [email] = @Email;

        IF @ParticipantUserId IS NULL
        BEGIN
            INSERT INTO dbo.users ([full_name], [email], [password_hash], [role_id], [is_active], [created_by])
            VALUES (@FullName, @Email, @MemberPasswordHash, 3, 1, @AdminUserId);
            SET @ParticipantUserId = CAST(SCOPE_IDENTITY() AS int);
        END
        ELSE IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE [user_id] = @ParticipantUserId AND [role_id] = 3 AND [is_active] = 1)
            THROW 51040, 'A scenario participant email already exists but is not an active member account; resolve it manually.', 1;

        SELECT @ParticipantMemberId = [member_id] FROM dbo.members WHERE [user_id] = @ParticipantUserId;

        IF @ParticipantMemberId IS NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.members WHERE [student_number] = @StudentNumber)
                THROW 51041, 'A scenario participant student number already belongs to another member; resolve it manually.', 1;

            INSERT INTO dbo.members ([user_id], [student_number], [course], [year_level], [section], [mobile_number], [membership_type], [membership_status_id], [created_by])
            VALUES (@ParticipantUserId, @StudentNumber, @Course, @YearLevel, @Section, NULL, @MembershipType, 1, @AdminUserId);
            SET @ParticipantMemberId = CAST(SCOPE_IDENTITY() AS int);
        END

        INSERT INTO @MemberIds ([Ordinal], [UserId], [MemberId])
        VALUES (@Ordinal, @ParticipantUserId, @ParticipantMemberId);

        FETCH NEXT FROM participant_cursor INTO @Ordinal, @FullName, @Email, @StudentNumber, @Course, @YearLevel, @Section, @MembershipType;
    END

    CLOSE participant_cursor;
    DEALLOCATE participant_cursor;

    /* Fill a realistic mobile number on the seeded participants only when it is still empty,
       so a later manual correction in the app is never overwritten. */
    UPDATE m
    SET [mobile_number] = CONCAT(N'0917-000-', RIGHT(N'0000' + CAST(p.[Ordinal] + 101 AS nvarchar(4)), 4))
    FROM dbo.members AS m
    INNER JOIN @MemberIds AS ids ON ids.[MemberId] = m.[member_id]
    INNER JOIN @Participants AS p ON p.[Ordinal] = ids.[Ordinal]
    WHERE m.[mobile_number] IS NULL;

    /* ---- Venues ------------------------------------------------------------- */

    DECLARE @AuditoriumVenueId int;
    SELECT @AuditoriumVenueId = [venue_id] FROM dbo.venues WHERE [room_name] = N'University Auditorium';
    IF @AuditoriumVenueId IS NULL
    BEGIN
        INSERT INTO dbo.venues ([room_name], [capacity], [location], [availability_notes], [contact_person], [is_active], [image_path], [created_by])
        VALUES (N'University Auditorium', 400, N'Main Campus, Second Floor',
                N'Available on weekday afternoons; multimedia equipment included.', N'Campus Facilities Office', 1,
                N'uploads/venues/university-auditorium.jpg', @AdminUserId);
        SET @AuditoriumVenueId = CAST(SCOPE_IDENTITY() AS int);
    END

    DECLARE @CourtVenueId int;
    SELECT @CourtVenueId = [venue_id] FROM dbo.venues WHERE [room_name] = N'Covered Court';
    IF @CourtVenueId IS NULL
    BEGIN
        INSERT INTO dbo.venues ([room_name], [capacity], [location], [availability_notes], [contact_person], [is_active], [image_path], [created_by])
        VALUES (N'Covered Court', 250, N'Main Campus, Sports Complex',
                N'Available on weekends; outdoor activity clearance required.', N'Athletics Office', 1,
                N'uploads/venues/covered-court.jpg', @AdminUserId);
        SET @CourtVenueId = CAST(SCOPE_IDENTITY() AS int);
    END

    /* ---- Event classifications --------------------------------------------- */

    DECLARE @SeminarTypeId int;
    SELECT @SeminarTypeId = [type_id] FROM dbo.event_types WHERE [type_name] = N'Seminar';
    IF @SeminarTypeId IS NULL
    BEGIN
        INSERT INTO dbo.event_types ([type_name], [description], [created_by])
        VALUES (N'Seminar', N'An educational session featuring presentations, discussions, or workshops.', @AdminUserId);
        SET @SeminarTypeId = CAST(SCOPE_IDENTITY() AS int);
    END

    DECLARE @OutreachTypeId int;
    SELECT @OutreachTypeId = [type_id] FROM dbo.event_types WHERE [type_name] = N'Outreach';
    IF @OutreachTypeId IS NULL
    BEGIN
        INSERT INTO dbo.event_types ([type_name], [description], [created_by])
        VALUES (N'Outreach', N'An activity that engages with and serves the broader community.', @AdminUserId);
        SET @OutreachTypeId = CAST(SCOPE_IDENTITY() AS int);
    END

    DECLARE @AssemblyTypeId int;
    SELECT @AssemblyTypeId = [type_id] FROM dbo.event_types WHERE [type_name] = N'General Assembly';
    IF @AssemblyTypeId IS NULL
    BEGIN
        INSERT INTO dbo.event_types ([type_name], [description], [created_by])
        VALUES (N'General Assembly', N'An organization-wide gathering for announcements and collective decisions.', @AdminUserId);
        SET @AssemblyTypeId = CAST(SCOPE_IDENTITY() AS int);
    END

    /* ---- Events ------------------------------------------------------------- */

    DECLARE @CompletedEventId int;
    SELECT @CompletedEventId = [event_id] FROM dbo.events WHERE [title] = N'Leadership Summit 2026';
    IF @CompletedEventId IS NULL
    BEGIN
        INSERT INTO dbo.events ([title], [description], [type_id], [venue_id], [starts_at], [registration_deadline],
                                [participant_limit], [event_status_id], [image_path], [published_by], [published_at],
                                [is_check_in_open], [created_by])
        VALUES (N'Leadership Summit 2026',
                N'A student leadership summit with workshops on team facilitation, event planning, and organizational communication.',
                @SeminarTypeId, @AuditoriumVenueId, DATEADD(day, -3, @Now), DATEADD(day, -5, @Now),
                120, 2, N'uploads/events/1a2b3c4d5e6f47a8b9c0d1e2f3a4b5c6.jpg', @AdminUserId, DATEADD(day, -10, @Now), 0, @AdminUserId);
        SET @CompletedEventId = CAST(SCOPE_IDENTITY() AS int);
        SET @ScenarioCreated = 1;
    END
    ELSE
    BEGIN
        UPDATE dbo.events
        SET [description] = N'A student leadership summit with workshops on team facilitation, event planning, and organizational communication.',
            [type_id] = @SeminarTypeId,
            [venue_id] = @AuditoriumVenueId,
            [starts_at] = DATEADD(day, -3, @Now),
            [registration_deadline] = DATEADD(day, -5, @Now),
            [participant_limit] = 120,
            [event_status_id] = 2,
            [image_path] = N'uploads/events/1a2b3c4d5e6f47a8b9c0d1e2f3a4b5c6.jpg',
            [published_by] = @AdminUserId,
            [published_at] = DATEADD(day, -10, @Now),
            [is_check_in_open] = 0,
            [check_in_opened_by] = NULL, [check_in_opened_at] = NULL,
            [check_in_closed_by] = NULL, [check_in_closed_at] = NULL,
            [completed_by] = NULL, [completed_at] = NULL,
            [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now
        WHERE [event_id] = @CompletedEventId;
    END

    DECLARE @ReadyEventId int;
    SELECT @ReadyEventId = [event_id] FROM dbo.events WHERE [title] = N'Community Outreach Drive';
    IF @ReadyEventId IS NULL
    BEGIN
        INSERT INTO dbo.events ([title], [description], [type_id], [venue_id], [starts_at], [registration_deadline],
                                [participant_limit], [event_status_id], [image_path], [published_by], [published_at],
                                [is_check_in_open], [created_by])
        VALUES (N'Community Outreach Drive',
                N'A coastal cleanup and school-supplies drive with partner barangays and volunteer student groups.',
                @OutreachTypeId, @CourtVenueId, DATEADD(day, -1, @Now), DATEADD(day, -3, @Now),
                80, 2, N'uploads/events/2b3c4d5e6f7a48b9c0d1e2f3a4b5c6d7.jpg', @AdminUserId, DATEADD(day, -8, @Now), 0, @AdminUserId);
        SET @ReadyEventId = CAST(SCOPE_IDENTITY() AS int);
    END
    ELSE
    BEGIN
        UPDATE dbo.events
        SET [description] = N'A coastal cleanup and school-supplies drive with partner barangays and volunteer student groups.',
            [type_id] = @OutreachTypeId,
            [venue_id] = @CourtVenueId,
            [starts_at] = DATEADD(day, -1, @Now),
            [registration_deadline] = DATEADD(day, -3, @Now),
            [participant_limit] = 80,
            [event_status_id] = 2,
            [image_path] = N'uploads/events/2b3c4d5e6f7a48b9c0d1e2f3a4b5c6d7.jpg',
            [published_by] = @AdminUserId,
            [published_at] = DATEADD(day, -8, @Now),
            [is_check_in_open] = 0,
            [check_in_opened_by] = NULL, [check_in_opened_at] = NULL,
            [check_in_closed_by] = NULL, [check_in_closed_at] = NULL,
            [completed_by] = NULL, [completed_at] = NULL,
            [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now
        WHERE [event_id] = @ReadyEventId;
    END

    DECLARE @UpcomingEventId int;
    SELECT @UpcomingEventId = [event_id] FROM dbo.events WHERE [title] = N'Founders'' Week Assembly';
    IF @UpcomingEventId IS NULL
    BEGIN
        INSERT INTO dbo.events ([title], [description], [type_id], [venue_id], [starts_at], [registration_deadline],
                                [participant_limit], [event_status_id], [image_path], [published_by], [published_at],
                                [is_check_in_open], [created_by])
        VALUES (N'Founders'' Week Assembly',
                N'The opening assembly for Founders'' Week, covering the activity calendar, committee assignments, and organization awards.',
                @AssemblyTypeId, @AuditoriumVenueId, DATEADD(day, 2, @Now), DATEADD(day, 1, @Now),
                100, 2, N'uploads/events/3c4d5e6f7a8b49c0d1e2f3a4b5c6d7e8.jpg', @AdminUserId, DATEADD(day, -2, @Now), 0, @AdminUserId);
        SET @UpcomingEventId = CAST(SCOPE_IDENTITY() AS int);
    END
    ELSE
    BEGIN
        UPDATE dbo.events
        SET [description] = N'The opening assembly for Founders'' Week, covering the activity calendar, committee assignments, and organization awards.',
            [type_id] = @AssemblyTypeId,
            [venue_id] = @AuditoriumVenueId,
            [starts_at] = DATEADD(day, 2, @Now),
            [registration_deadline] = DATEADD(day, 1, @Now),
            [participant_limit] = 100,
            [event_status_id] = 2,
            [image_path] = N'uploads/events/3c4d5e6f7a8b49c0d1e2f3a4b5c6d7e8.jpg',
            [published_by] = @AdminUserId,
            [published_at] = DATEADD(day, -2, @Now),
            [is_check_in_open] = 0,
            [check_in_opened_by] = NULL, [check_in_opened_at] = NULL,
            [check_in_closed_by] = NULL, [check_in_closed_at] = NULL,
            [completed_by] = NULL, [completed_at] = NULL,
            [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now
        WHERE [event_id] = @UpcomingEventId;
    END

    /* ---- Registrations ------------------------------------------------------ */

    DECLARE @RegistrationId int;

    /* Completed event: every participant is confirmed, in ordinal order, so the demo
       seed's first valid check-in (the demo member) receives the issued certificate. */
    DECLARE registration_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT [Ordinal], [MemberId] FROM @MemberIds ORDER BY [Ordinal];
    DECLARE @RegOrdinal int, @RegMemberId int;

    OPEN registration_cursor;
    FETCH NEXT FROM registration_cursor INTO @RegOrdinal, @RegMemberId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @RegistrationId = NULL; SELECT @RegistrationId = [registration_id]
        FROM dbo.registrations
        WHERE [event_id] = @CompletedEventId AND [member_id] = @RegMemberId;

        IF @RegistrationId IS NULL
        BEGIN
            INSERT INTO dbo.registrations ([member_id], [event_id], [registration_status_id], [course_snapshot], [year_level_snapshot], [section_snapshot],
                                           [submitted_at], [reviewed_by], [reviewed_at], [confirmed_by], [confirmed_at], [created_by])
            SELECT m.[MemberId], @CompletedEventId, 2, p.[Course], p.[YearLevel], p.[Section],
                   DATEADD(day, -7, @Now), @AdminUserId, DATEADD(day, -6, @Now), @AdminUserId, DATEADD(day, -6, @Now), @AdminUserId
            FROM @MemberIds AS m
            JOIN @Participants AS p ON p.[Ordinal] = m.[Ordinal]
            WHERE m.[Ordinal] = @RegOrdinal;
        END
        ELSE
        BEGIN
            UPDATE dbo.registrations
            SET [registration_status_id] = 2,
                [reviewed_by] = @AdminUserId, [reviewed_at] = DATEADD(day, -6, @Now),
                [confirmed_by] = @AdminUserId, [confirmed_at] = DATEADD(day, -6, @Now),
                [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
                [updated_by] = @AdminUserId, [updated_at] = @Now
            WHERE [registration_id] = @RegistrationId;
        END

        FETCH NEXT FROM registration_cursor INTO @RegOrdinal, @RegMemberId;
    END
    CLOSE registration_cursor;
    DEALLOCATE registration_cursor;

    /* Ready-to-complete event: two confirmed participants and the demo member's own
       cancelled registration, so My Events shows a cancelled record. */
    DECLARE @ReadyMemberId int;
    SET @ReadyMemberId = NULL; SELECT @ReadyMemberId = [MemberId] FROM @MemberIds WHERE [Ordinal] = 2;
    SET @RegistrationId = NULL; SELECT @RegistrationId = [registration_id] FROM dbo.registrations WHERE [event_id] = @ReadyEventId AND [member_id] = @ReadyMemberId;
    IF @RegistrationId IS NULL
    BEGIN
        INSERT INTO dbo.registrations ([member_id], [event_id], [registration_status_id], [course_snapshot], [year_level_snapshot], [section_snapshot],
                                       [submitted_at], [reviewed_by], [reviewed_at], [confirmed_by], [confirmed_at], [created_by])
        SELECT m.[MemberId], @ReadyEventId, 2, p.[Course], p.[YearLevel], p.[Section],
               DATEADD(day, -5, @Now), @AdminUserId, DATEADD(day, -4, @Now), @AdminUserId, DATEADD(day, -4, @Now), @AdminUserId
        FROM @MemberIds AS m
        JOIN @Participants AS p ON p.[Ordinal] = m.[Ordinal]
        WHERE m.[Ordinal] = 2;
    END
    ELSE
        UPDATE dbo.registrations SET [registration_status_id] = 2, [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now WHERE [registration_id] = @RegistrationId;

    SET @ReadyMemberId = NULL; SELECT @ReadyMemberId = [MemberId] FROM @MemberIds WHERE [Ordinal] = 3;
    SET @RegistrationId = NULL; SELECT @RegistrationId = [registration_id] FROM dbo.registrations WHERE [event_id] = @ReadyEventId AND [member_id] = @ReadyMemberId;
    IF @RegistrationId IS NULL
    BEGIN
        INSERT INTO dbo.registrations ([member_id], [event_id], [registration_status_id], [course_snapshot], [year_level_snapshot], [section_snapshot],
                                       [submitted_at], [reviewed_by], [reviewed_at], [confirmed_by], [confirmed_at], [created_by])
        SELECT m.[MemberId], @ReadyEventId, 2, p.[Course], p.[YearLevel], p.[Section],
               DATEADD(day, -5, @Now), @AdminUserId, DATEADD(day, -4, @Now), @AdminUserId, DATEADD(day, -4, @Now), @AdminUserId
        FROM @MemberIds AS m
        JOIN @Participants AS p ON p.[Ordinal] = m.[Ordinal]
        WHERE m.[Ordinal] = 3;
    END
    ELSE
        UPDATE dbo.registrations SET [registration_status_id] = 2, [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now WHERE [registration_id] = @RegistrationId;

    DECLARE @DemoMemberId int;
    SET @DemoMemberId = NULL; SELECT @DemoMemberId = [MemberId] FROM @MemberIds WHERE [Ordinal] = 1;
    SET @RegistrationId = NULL; SELECT @RegistrationId = [registration_id] FROM dbo.registrations WHERE [event_id] = @ReadyEventId AND [member_id] = @DemoMemberId;
    IF @RegistrationId IS NULL
    BEGIN
        INSERT INTO dbo.registrations ([member_id], [event_id], [registration_status_id], [course_snapshot], [year_level_snapshot], [section_snapshot],
                                       [submitted_at], [cancelled_by], [cancelled_at], [cancellation_reason], [created_by])
        SELECT m.[MemberId], @ReadyEventId, 4, p.[Course], p.[YearLevel], p.[Section],
               DATEADD(day, -5, @Now), @DemoMemberId, DATEADD(day, -4, @Now), N'Schedule conflict with a laboratory class.', @AdminUserId
        FROM @MemberIds AS m
        JOIN @Participants AS p ON p.[Ordinal] = m.[Ordinal]
        WHERE m.[Ordinal] = 1;
    END
    ELSE
        UPDATE dbo.registrations SET [registration_status_id] = 4, [cancelled_by] = @DemoMemberId, [cancelled_at] = DATEADD(day, -4, @Now),
            [cancellation_reason] = N'Schedule conflict with a laboratory class.', [updated_by] = @AdminUserId, [updated_at] = @Now
        WHERE [registration_id] = @RegistrationId;

    /* Upcoming event: three confirmed participants (including the demo member) and one
       pending registration for the officer's review queue. */
    DECLARE @UpcomingMemberId int;
    SET @UpcomingMemberId = NULL; SELECT @UpcomingMemberId = [MemberId] FROM @MemberIds WHERE [Ordinal] = 1;
    SET @RegistrationId = NULL; SELECT @RegistrationId = [registration_id] FROM dbo.registrations WHERE [event_id] = @UpcomingEventId AND [member_id] = @UpcomingMemberId;
    IF @RegistrationId IS NULL
    BEGIN
        INSERT INTO dbo.registrations ([member_id], [event_id], [registration_status_id], [course_snapshot], [year_level_snapshot], [section_snapshot],
                                       [submitted_at], [reviewed_by], [reviewed_at], [confirmed_by], [confirmed_at], [created_by])
        SELECT m.[MemberId], @UpcomingEventId, 2, p.[Course], p.[YearLevel], p.[Section],
               DATEADD(day, -1, @Now), @AdminUserId, @Now, @AdminUserId, @Now, @AdminUserId
        FROM @MemberIds AS m
        JOIN @Participants AS p ON p.[Ordinal] = m.[Ordinal]
        WHERE m.[Ordinal] = 1;
    END
    ELSE
        UPDATE dbo.registrations SET [registration_status_id] = 2, [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now WHERE [registration_id] = @RegistrationId;

    SET @UpcomingMemberId = NULL; SELECT @UpcomingMemberId = [MemberId] FROM @MemberIds WHERE [Ordinal] = 4;
    SET @RegistrationId = NULL; SELECT @RegistrationId = [registration_id] FROM dbo.registrations WHERE [event_id] = @UpcomingEventId AND [member_id] = @UpcomingMemberId;
    IF @RegistrationId IS NULL
    BEGIN
        INSERT INTO dbo.registrations ([member_id], [event_id], [registration_status_id], [course_snapshot], [year_level_snapshot], [section_snapshot],
                                       [submitted_at], [reviewed_by], [reviewed_at], [confirmed_by], [confirmed_at], [created_by])
        SELECT m.[MemberId], @UpcomingEventId, 2, p.[Course], p.[YearLevel], p.[Section],
               DATEADD(day, -1, @Now), @AdminUserId, @Now, @AdminUserId, @Now, @AdminUserId
        FROM @MemberIds AS m
        JOIN @Participants AS p ON p.[Ordinal] = m.[Ordinal]
        WHERE m.[Ordinal] = 4;
    END
    ELSE
        UPDATE dbo.registrations SET [registration_status_id] = 2, [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now WHERE [registration_id] = @RegistrationId;

    SET @UpcomingMemberId = NULL; SELECT @UpcomingMemberId = [MemberId] FROM @MemberIds WHERE [Ordinal] = 5;
    SET @RegistrationId = NULL; SELECT @RegistrationId = [registration_id] FROM dbo.registrations WHERE [event_id] = @UpcomingEventId AND [member_id] = @UpcomingMemberId;
    IF @RegistrationId IS NULL
    BEGIN
        INSERT INTO dbo.registrations ([member_id], [event_id], [registration_status_id], [course_snapshot], [year_level_snapshot], [section_snapshot],
                                       [submitted_at], [reviewed_by], [reviewed_at], [confirmed_by], [confirmed_at], [created_by])
        SELECT m.[MemberId], @UpcomingEventId, 2, p.[Course], p.[YearLevel], p.[Section],
               DATEADD(day, -1, @Now), @AdminUserId, @Now, @AdminUserId, @Now, @AdminUserId
        FROM @MemberIds AS m
        JOIN @Participants AS p ON p.[Ordinal] = m.[Ordinal]
        WHERE m.[Ordinal] = 5;
    END
    ELSE
        UPDATE dbo.registrations SET [registration_status_id] = 2, [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now WHERE [registration_id] = @RegistrationId;

    SET @UpcomingMemberId = NULL; SELECT @UpcomingMemberId = [MemberId] FROM @MemberIds WHERE [Ordinal] = 6;
    SET @RegistrationId = NULL; SELECT @RegistrationId = [registration_id] FROM dbo.registrations WHERE [event_id] = @UpcomingEventId AND [member_id] = @UpcomingMemberId;
    IF @RegistrationId IS NULL
    BEGIN
        INSERT INTO dbo.registrations ([member_id], [event_id], [registration_status_id], [course_snapshot], [year_level_snapshot], [section_snapshot],
                                       [submitted_at], [created_by])
        SELECT m.[MemberId], @UpcomingEventId, 1, p.[Course], p.[YearLevel], p.[Section],
               DATEADD(hour, -6, @Now), @AdminUserId
        FROM @MemberIds AS m
        JOIN @Participants AS p ON p.[Ordinal] = m.[Ordinal]
        WHERE m.[Ordinal] = 6;
    END
    ELSE
        UPDATE dbo.registrations SET [registration_status_id] = 1, [reviewed_by] = NULL, [reviewed_at] = NULL,
            [confirmed_by] = NULL, [confirmed_at] = NULL, [cancelled_by] = NULL, [cancelled_at] = NULL, [cancellation_reason] = NULL,
            [updated_by] = @AdminUserId, [updated_at] = @Now WHERE [registration_id] = @RegistrationId;

    /* One pending membership request, so the Account requests queue opens populated and the
       presenter can demonstrate approval without leaving the signed-in session. The request is
       only inserted when that email has no account and no pending request yet. */
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE [email] = N'camille.torres@uc.edu.ph')
       AND NOT EXISTS (SELECT 1 FROM dbo.account_requests WHERE [email] = N'camille.torres@uc.edu.ph' AND [account_request_status_id] = 1)
       AND NOT EXISTS (SELECT 1 FROM dbo.account_requests WHERE [student_number] = N'UC-2026-0110' AND [account_request_status_id] = 1)
    BEGIN
        INSERT INTO dbo.account_requests ([student_number], [full_name], [email], [password_hash], [course], [year_level], [section], [mobile_number], [account_request_status_id], [requested_at])
        VALUES (N'UC-2026-0110', N'Camille Torres', N'camille.torres@uc.edu.ph', @MemberPasswordHash,
                N'BS Information Technology', 2, N'C', N'0917-000-0110', 1, DATEADD(hour, -4, @Now));
    END

    /* One realistic system audit row on the first run, so the Activity Log opens with
       content before the presenter performs any action. Re-runs add nothing. */
    IF @ScenarioCreated = 1
    BEGIN
        INSERT INTO dbo.activity_logs ([actor_type_id], [actor_user_id], [action], [entity_type], [entity_id], [description], [occurred_at])
        VALUES (2, NULL, 'event.published', 'events', @CompletedEventId,
                N'Published Leadership Summit 2026 and opened registration.', DATEADD(day, -10, @Now));
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT [user_id], [full_name], [email], [role_id], [is_active]
FROM dbo.users
WHERE [email] IN (N'$(OfficerEmail)', N'$(MemberEmail)')
ORDER BY [role_id];
GO

SELECT [event_id], [title], [event_status_id], [starts_at], [registration_deadline], [participant_limit],
       (SELECT COUNT(*) FROM dbo.registrations AS r WHERE r.[event_id] = e.[event_id] AND r.[registration_status_id] = 2) AS [confirmed],
       (SELECT COUNT(*) FROM dbo.registrations AS r WHERE r.[event_id] = e.[event_id] AND r.[registration_status_id] = 1) AS [pending],
       (SELECT COUNT(*) FROM dbo.registrations AS r WHERE r.[event_id] = e.[event_id] AND r.[registration_status_id] = 4) AS [cancelled]
FROM dbo.events AS e
WHERE [title] IN (N'Leadership Summit 2026', N'Community Outreach Drive', N'Founders'' Week Assembly')
ORDER BY [starts_at];
GO
