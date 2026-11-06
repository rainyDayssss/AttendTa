/* Run as a demo database administrator, after creating the runtime login and user.
   Supply -v RuntimeUser=<database-user>; no passwords are accepted here. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'AttendTa_Demo'
    THROW 51100, 'Run only in AttendTa_Demo.', 1;

DECLARE @user sysname = N'$(RuntimeUser)';
IF @user IS NULL OR @user LIKE N'%$(%' OR @user IN (N'dbo', N'guest', N'public', N'INFORMATION_SCHEMA', N'sys')
   OR DATABASE_PRINCIPAL_ID(@user) IS NULL
   OR NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @user AND type IN ('S','U'))
    THROW 51101, 'Supply an existing, dedicated runtime database user.', 1;
IF IS_ROLEMEMBER(N'db_owner', @user) = 1 OR IS_ROLEMEMBER(N'db_datareader', @user) = 1
   OR IS_ROLEMEMBER(N'db_datawriter', @user) = 1 OR IS_ROLEMEMBER(N'db_ddladmin', @user) = 1
   OR EXISTS (SELECT 1 FROM sys.database_role_members WHERE member_principal_id = DATABASE_PRINCIPAL_ID(@user))
    THROW 51102, 'Runtime user must not belong to a database role.', 1;
IF OBJECT_ID(N'dbo.activity_logs', N'U') IS NULL
    THROW 51103, 'Canonical schema must be installed first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @sql nvarchar(max) = N'';
    SELECT @sql += N'GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.' + QUOTENAME(name)
                 + N' TO ' + QUOTENAME(@user) + N';' + NCHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name <> N'activity_logs';
    SET @sql += N'GRANT SELECT, INSERT ON OBJECT::dbo.activity_logs TO ' + QUOTENAME(@user) + N';';
    EXEC sys.sp_executesql @sql;

    EXECUTE AS USER = @user;
    IF HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CONTROL') <> 0
       OR HAS_PERMS_BY_NAME(N'dbo.activity_logs', 'OBJECT', 'SELECT') <> 1
       OR HAS_PERMS_BY_NAME(N'dbo.activity_logs', 'OBJECT', 'INSERT') <> 1
       OR HAS_PERMS_BY_NAME(N'dbo.activity_logs', 'OBJECT', 'UPDATE') <> 0
       OR HAS_PERMS_BY_NAME(N'dbo.activity_logs', 'OBJECT', 'DELETE') <> 0
       OR EXISTS (
            SELECT 1 FROM sys.columns AS column_definition
            WHERE column_definition.object_id = OBJECT_ID(N'dbo.activity_logs')
              AND HAS_PERMS_BY_NAME(N'dbo.activity_logs', N'OBJECT', N'UPDATE', column_definition.name, N'COLUMN') = 1)
    BEGIN
        REVERT;
        THROW 51104, 'Runtime effective permissions are unsafe or incomplete; grants rolled back.', 1;
    END;
    REVERT;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
PRINT 'Runtime object grants applied. Validate using the actual runtime login.';
