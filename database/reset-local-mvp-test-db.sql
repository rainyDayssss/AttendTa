/*
   DESTRUCTIVE: resets only the disposable AttendTaMVP_Test database.
   Run this from the master database on a local SQL Server instance.
   It does not touch AsiBasecodeDB, main/dev, or any remote database.
*/
USE [master];
GO

IF DB_NAME() <> N'master'
    THROW 51010, 'Run this reset script from master.', 1;
GO

IF DB_ID(N'AttendTaMVP_Test') IS NOT NULL
BEGIN
    ALTER DATABASE [AttendTaMVP_Test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [AttendTaMVP_Test];
END
GO

CREATE DATABASE [AttendTaMVP_Test];
GO
