/*
   Seeds the MVP's six starter event classifications.
   Run only in AttendTaMVP_Test, AttendTa_IT_<guid>, or AttendTa_Demo, after the
   local admin seed has created the initial active administrator.
   The active administrator is selected by role, not by a hard-coded email, and its
   user_id fills the required created_by field. This file never stores credentials.
   Never use local test credentials outside a disposable local database.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'AttendTaMVP_Test'
   AND DB_NAME() <> N'AttendTa_Demo'
   AND (DB_NAME() NOT LIKE N'AttendTa_IT[_]%' OR LEN(DB_NAME()) <= LEN(N'AttendTa_IT_'))
    THROW 51020, 'This seed script may only run in AttendTaMVP_Test, AttendTa_IT_<guid>, or AttendTa_Demo.', 1;
GO

DECLARE @CreatedByUserId int;

SELECT TOP (1) @CreatedByUserId = [user_id]
FROM dbo.users
WHERE [role_id] = 1
  AND [is_active] = 1
ORDER BY [user_id];

IF @CreatedByUserId IS NULL
    THROW 51021, 'Run the local admin seed before seeding event types.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO dbo.event_types ([type_name], [description], [created_by])
    SELECT seed.[type_name], seed.[description], @CreatedByUserId
    FROM (VALUES
        (N'Seminar', N'An educational session featuring presentations, discussions, or workshops.'),
        (N'Outreach', N'An activity that engages with and serves the broader community.'),
        (N'Meeting', N'A gathering for organization members to discuss plans and business.'),
        (N'Competition', N'An organized contest in which participants demonstrate their skills.'),
        (N'General Assembly', N'An organization-wide gathering for announcements and collective decisions.'),
        (N'Training', N'A session focused on developing practical knowledge or skills.')
    ) AS seed([type_name], [description])
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.event_types AS existing
        WHERE existing.[type_name] = seed.[type_name]
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT [type_id], [type_name], [description], [is_active], [created_by]
FROM dbo.event_types
ORDER BY [type_name];
GO
