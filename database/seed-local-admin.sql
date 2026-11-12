/*
   Seeds the one-time administrator for AttendTaMVP_Test, AttendTa_IT_<guid>, or AttendTa_Demo.

   Supply the three sqlcmd variables; never pass a plaintext password:
     -v AdminName="AttendTa Local Admin"
     -v AdminEmail="admin@example.edu"
     -v AdminPasswordHash="<the PHC hash printed by the WebApp --hash-password mode>"

   The script rejects a missing or malformed Argon2id PHC hash, creates the account as an
   active Administrator, and appends exactly one system audit row. It is idempotent: re-running
   it with the same email leaves the existing active administrator and its audit row untouched.
   It never stores or logs a plaintext password or the hash itself.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'AttendTaMVP_Test'
   AND DB_NAME() <> N'AttendTa_Demo'
   AND (DB_NAME() NOT LIKE N'AttendTa_IT[_]%' OR LEN(DB_NAME()) <= LEN(N'AttendTa_IT_'))
    THROW 51015, 'This admin seed may only run in AttendTaMVP_Test, AttendTa_IT_<guid>, or AttendTa_Demo.', 1;
GO

DECLARE @AdminName nvarchar(4000) = N'$(AdminName)';
DECLARE @AdminEmail nvarchar(4000) = N'$(AdminEmail)';
DECLARE @AdminPasswordHash varchar(4000) = '$(AdminPasswordHash)';

DECLARE @ExistingUserId int;
DECLARE @ExistingRoleId tinyint;
DECLARE @ExistingIsActive bit;
DECLARE @NewUserId int;

IF NULLIF(LTRIM(RTRIM(@AdminName)), N'') IS NULL
   OR CHARINDEX(NCHAR(36) + N'(', @AdminName) > 0
    THROW 51016, 'Supply AdminName for the initial administrator.', 1;

IF NULLIF(LTRIM(RTRIM(@AdminEmail)), N'') IS NULL
   OR CHARINDEX(NCHAR(36) + N'(', @AdminEmail) > 0
   OR CHARINDEX(N'@', @AdminEmail) <= 1
   OR CHARINDEX(N'.', @AdminEmail, CHARINDEX(N'@', @AdminEmail)) <= 0
    THROW 51017, 'Supply a valid AdminEmail for the initial administrator.', 1;

IF NULLIF(LTRIM(RTRIM(@AdminPasswordHash)), '') IS NULL
   OR CHARINDEX(CHAR(36) + '(', @AdminPasswordHash) > 0
   OR @AdminPasswordHash NOT LIKE '$argon2id$v=19$m=%'
   OR LEN(@AdminPasswordHash) > 255
    THROW 51018, 'Supply an Argon2id AdminPasswordHash from the Auth hasher (--hash-password); plaintext passwords are not accepted.', 1;

SELECT
    @ExistingUserId = [user_id],
    @ExistingRoleId = [role_id],
    @ExistingIsActive = [is_active]
FROM dbo.users
WHERE [email] = @AdminEmail;

IF @ExistingUserId IS NOT NULL
BEGIN
    IF @ExistingRoleId <> 1 OR @ExistingIsActive <> 1
        THROW 51019, 'The supplied AdminEmail already exists but is not an active administrator; resolve it manually.', 1;
END
ELSE
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.users ([full_name], [email], [password_hash], [role_id], [is_active], [created_by])
        VALUES (@AdminName, @AdminEmail, @AdminPasswordHash, 1, 1, NULL);

        SET @NewUserId = CAST(SCOPE_IDENTITY() AS int);

        INSERT INTO dbo.activity_logs ([actor_type_id], [actor_user_id], [action], [entity_type], [entity_id], [description])
        VALUES (2, NULL, 'user.provisioned', 'users', @NewUserId,
                N'Provisioned the initial administrator account for ' + @AdminEmail + N'.');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
GO

DECLARE @AdminEmail nvarchar(255) = N'$(AdminEmail)';

SELECT [user_id], [full_name], [email], [role_id], [is_active], [created_by]
FROM dbo.users
WHERE [email] = @AdminEmail;
GO
