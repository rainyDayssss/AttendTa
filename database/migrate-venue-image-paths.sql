/*
   Prefix legacy venue image keys while preserving their files in place.
   Stop the application before running this script, then deploy the version
   that resolves uploads/venues/<name> from Common:SetupRoot.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'AttendTaMVP_Test'
   AND DB_NAME() <> N'AttendTa_Demo'
   AND (DB_NAME() NOT LIKE N'AttendTa_IT[_]%' OR LEN(DB_NAME()) <= LEN(N'AttendTa_IT_'))
BEGIN
    RAISERROR('This venue image path migration may only run in AttendTaMVP_Test, AttendTa_IT_<guid>, or AttendTa_Demo.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.venues', N'U') IS NULL
BEGIN
    RAISERROR('The venues table does not exist in the current database.', 16, 1);
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TooLong TABLE
    (
        venue_id int NOT NULL,
        migrated_length int NOT NULL
    );

    INSERT INTO @TooLong (venue_id, migrated_length)
    SELECT venue_id, DATALENGTH(N'uploads/' + image_path) / 2
    FROM dbo.venues WITH (UPDLOCK, HOLDLOCK)
    WHERE image_path LIKE N'venues/%'
      AND DATALENGTH(N'uploads/' + image_path) / 2 > 500;

    IF EXISTS (SELECT 1 FROM @TooLong)
    BEGIN
        SELECT venue_id, migrated_length
        FROM @TooLong
        ORDER BY venue_id;

        RAISERROR('Venue image path migration stopped: one or more prefixed paths exceed the 500-character column limit. No rows were changed.', 16, 1);
    END;

    UPDATE dbo.venues
    SET image_path = N'uploads/' + image_path
    WHERE image_path LIKE N'venues/%';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
