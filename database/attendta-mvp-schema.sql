/*
   AttendTa MVP SQL Server schema, based on docs-local/ERD-DATABASE-DESIGN/new mvp.sql.

   This is the canonical AttendTa schema. The source ERD comments specify
   filtered/pending-only uniqueness; the executable SQL below implements those
   rules explicitly.
*/
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
    THROW 51000, 'This schema script may only run in AttendTaMVP_Test, AttendTa_IT_<guid>, or AttendTa_Demo.', 1;
GO

IF OBJECT_ID(N'dbo.users', N'U') IS NOT NULL
    THROW 51001, 'This database already contains schema objects. Use a fresh database or run the reset script for AttendTaMVP_Test.', 1;
GO

ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

CREATE TABLE [roles] (
  [role_id] tinyint NOT NULL PRIMARY KEY,
  [code] varchar(50) UNIQUE NOT NULL,
  [label] nvarchar(100) NOT NULL
)
GO

CREATE TABLE [account_request_statuses] (
  [account_request_status_id] tinyint NOT NULL PRIMARY KEY,
  [code] varchar(50) UNIQUE NOT NULL,
  [label] nvarchar(100) NOT NULL
)
GO

CREATE TABLE [membership_statuses] (
  [membership_status_id] tinyint NOT NULL PRIMARY KEY,
  [code] varchar(50) UNIQUE NOT NULL,
  [label] nvarchar(100) NOT NULL
)
GO

CREATE TABLE [event_statuses] (
  [event_status_id] tinyint NOT NULL PRIMARY KEY,
  [code] varchar(50) UNIQUE NOT NULL,
  [label] nvarchar(100) NOT NULL
)
GO

CREATE TABLE [registration_statuses] (
  [registration_status_id] tinyint NOT NULL PRIMARY KEY,
  [code] varchar(50) UNIQUE NOT NULL,
  [label] nvarchar(100) NOT NULL
)
GO

CREATE TABLE [check_in_methods] (
  [check_in_method_id] tinyint NOT NULL PRIMARY KEY,
  [code] varchar(50) UNIQUE NOT NULL,
  [label] nvarchar(100) NOT NULL
)
GO

CREATE TABLE [certificate_statuses] (
  [certificate_status_id] tinyint NOT NULL PRIMARY KEY,
  [code] varchar(50) UNIQUE NOT NULL,
  [label] nvarchar(100) NOT NULL
)
GO

CREATE TABLE [actor_types] (
  [actor_type_id] tinyint NOT NULL PRIMARY KEY,
  [code] varchar(50) UNIQUE NOT NULL,
  [label] nvarchar(100) NOT NULL
)
GO

INSERT INTO [roles] ([role_id], [code], [label]) VALUES
  (1, 'admin', N'Administrator'),
  (2, 'officer', N'Officer'),
  (3, 'member', N'Member')
GO

INSERT INTO [account_request_statuses] ([account_request_status_id], [code], [label]) VALUES
  (1, 'pending', N'Pending'),
  (2, 'approved', N'Approved'),
  (3, 'rejected', N'Rejected')
GO

INSERT INTO [membership_statuses] ([membership_status_id], [code], [label]) VALUES
  (1, 'active', N'Active'),
  (2, 'inactive', N'Inactive')
GO

INSERT INTO [event_statuses] ([event_status_id], [code], [label]) VALUES
  (1, 'draft', N'Draft'),
  (2, 'published', N'Published'),
  (3, 'cancelled', N'Cancelled'),
  (4, 'completed', N'Completed')
GO

INSERT INTO [registration_statuses] ([registration_status_id], [code], [label]) VALUES
  (1, 'pending', N'Pending'),
  (2, 'confirmed', N'Confirmed'),
  (3, 'waitlisted', N'Waitlisted'),
  (4, 'cancelled', N'Cancelled')
GO

INSERT INTO [check_in_methods] ([check_in_method_id], [code], [label]) VALUES
  (1, 'qr_code', N'QR Code'),
  (2, 'manual_search', N'Manual Search'),
  (3, 'student_number', N'Student Number')
GO

INSERT INTO [certificate_statuses] ([certificate_status_id], [code], [label]) VALUES
  (1, 'pending_approval', N'Pending Approval'),
  (2, 'eligible', N'Eligible'),
  (3, 'not_eligible', N'Not Eligible'),
  (4, 'issued', N'Issued')
GO

INSERT INTO [actor_types] ([actor_type_id], [code], [label]) VALUES
  (1, 'user', N'User'),
  (2, 'system', N'System')
GO

CREATE TABLE [users] (
  [user_id] integer PRIMARY KEY IDENTITY(1, 1),
  [full_name] nvarchar(255) NOT NULL,
  [email] nvarchar(255) UNIQUE NOT NULL,
  [password_hash] varchar(255) NOT NULL,
  [role_id] tinyint NOT NULL DEFAULT (3),
  [is_active] bit NOT NULL DEFAULT (0),
  [created_by] integer,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_by] integer,
  [updated_at] datetime2(3)
)
GO

CREATE TABLE [account_requests] (
  [request_id] integer PRIMARY KEY IDENTITY(1, 1),
  [student_number] nvarchar(50) NOT NULL,
  [full_name] nvarchar(255) NOT NULL,
  [email] nvarchar(255) NOT NULL,
  [password_hash] varchar(255),
  [course] nvarchar(100) NOT NULL,
  [year_level] integer NOT NULL,
  [section] nvarchar(50) NOT NULL,
  [mobile_number] varchar(30),
  [account_request_status_id] tinyint NOT NULL DEFAULT (1),
  [requested_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [reviewed_by] integer,
  [reviewed_at] datetime2(3),
  [review_notes] nvarchar(max),
  [created_user_id] integer,
  CONSTRAINT [chk_account_requests_positive_year_level] CHECK (year_level > 0),
  CONSTRAINT [chk_account_requests_password_by_status] CHECK
  (
      ([account_request_status_id] = 1 AND [password_hash] IS NOT NULL)
      OR ([account_request_status_id] IN (2, 3) AND [password_hash] IS NULL)
  )
)
GO

CREATE TABLE [members] (
  [member_id] integer PRIMARY KEY IDENTITY(1, 1),
  [user_id] integer NOT NULL,
  [student_number] nvarchar(50) UNIQUE NOT NULL,
  [course] nvarchar(100) NOT NULL,
  [year_level] integer NOT NULL,
  [section] nvarchar(50) NOT NULL,
  [mobile_number] varchar(30),
  [membership_type] nvarchar(100),
  [membership_status_id] tinyint NOT NULL DEFAULT (1),
  [created_by] integer NOT NULL,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_by] integer,
  [updated_at] datetime2(3),
  CONSTRAINT [chk_members_positive_year_level] CHECK (year_level > 0)
)
GO

CREATE TABLE [event_types] (
  [type_id] integer PRIMARY KEY IDENTITY(1, 1),
  [type_name] nvarchar(100) UNIQUE NOT NULL,
  [description] nvarchar(max) NOT NULL,
  [is_active] bit NOT NULL DEFAULT (1),
  [created_by] integer NOT NULL,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_by] integer,
  [updated_at] datetime2(3)
)
GO

CREATE TABLE [venues] (
  [venue_id] integer PRIMARY KEY IDENTITY(1, 1),
  [room_name] nvarchar(150) NOT NULL,
  [capacity] integer NOT NULL,
  [location] nvarchar(255),
  [availability_notes] nvarchar(max),
  [contact_person] nvarchar(150),
  [is_active] bit NOT NULL DEFAULT (1),
  [image_path] nvarchar(500) NOT NULL,
  [created_by] integer NOT NULL,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_by] integer,
  [updated_at] datetime2(3),
  CONSTRAINT [chk_venues_positive_capacity] CHECK (capacity > 0),
  CONSTRAINT [chk_venues_required_photo] CHECK (LEN(LTRIM(RTRIM(image_path))) > 0)
)
GO

CREATE TABLE [events] (
  [event_id] integer PRIMARY KEY IDENTITY(1, 1),
  [title] nvarchar(255) NOT NULL,
  [description] nvarchar(max),
  [type_id] integer NOT NULL,
  [venue_id] integer NOT NULL,
  [starts_at] datetime2(3) NOT NULL,
  [registration_deadline] datetime2(3) NOT NULL,
  [participant_limit] integer NOT NULL,
  [event_status_id] tinyint NOT NULL DEFAULT (1),
  [image_path] nvarchar(500),
  [published_by] integer,
  [published_at] datetime2(3),
  [is_check_in_open] bit NOT NULL DEFAULT (0),
  [check_in_opened_by] integer,
  [check_in_opened_at] datetime2(3),
  [check_in_closed_by] integer,
  [check_in_closed_at] datetime2(3),
  [completed_by] integer,
  [completed_at] datetime2(3),
  [cancelled_by] integer,
  [cancelled_at] datetime2(3),
  [cancellation_reason] nvarchar(max),
  [created_by] integer NOT NULL,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_by] integer,
  [updated_at] datetime2(3),
  CONSTRAINT [chk_events_positive_participant_limit] CHECK (participant_limit > 0),
  CONSTRAINT [chk_events_registration_deadline] CHECK (registration_deadline <= starts_at)
)
GO

CREATE TABLE [registrations] (
  [registration_id] integer PRIMARY KEY IDENTITY(1, 1),
  [member_id] integer NOT NULL,
  [event_id] integer NOT NULL,
  [registration_status_id] tinyint NOT NULL DEFAULT (1),
  [course_snapshot] nvarchar(100) NOT NULL,
  [year_level_snapshot] integer NOT NULL,
  [section_snapshot] nvarchar(50) NOT NULL,
  [submitted_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [reviewed_by] integer,
  [reviewed_at] datetime2(3),
  [review_notes] nvarchar(max),
  [confirmed_by] integer,
  [confirmed_at] datetime2(3),
  [qr_token] varchar(64),
  [cancelled_by] integer,
  [cancelled_at] datetime2(3),
  [cancellation_reason] nvarchar(max),
  [created_by] integer NOT NULL,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_by] integer,
  [updated_at] datetime2(3),
  CONSTRAINT [chk_registrations_positive_year_level] CHECK (year_level_snapshot > 0)
)
GO

CREATE TABLE [attendance] (
  [attendance_id] integer PRIMARY KEY IDENTITY(1, 1),
  [registration_id] integer UNIQUE NOT NULL,
  [check_in_method_id] tinyint NOT NULL,
  [recorded_by] integer NOT NULL,
  [recorded_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [invalidated_by] integer,
  [invalidated_at] datetime2(3),
  [invalidation_reason] nvarchar(max)
)
GO

CREATE TABLE [certificates] (
  [certificate_id] integer PRIMARY KEY IDENTITY(1, 1),
  [registration_id] integer UNIQUE NOT NULL,
  [certificate_status_id] tinyint NOT NULL DEFAULT (1),
  [reviewed_by] integer,
  [reviewed_at] datetime2(3),
  [review_notes] nvarchar(max),
  [certificate_number] varchar(100),
  [recipient_name_snapshot] nvarchar(255),
  [event_title_snapshot] nvarchar(255),
  [event_starts_at_snapshot] datetime2(3),
  [issuer_name_snapshot] nvarchar(255),
  [file_path] nvarchar(500),
  [issued_by] integer,
  [issued_at] datetime2(3),
  [created_by] integer,
  [created_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
  [updated_by] integer,
  [updated_at] datetime2(3)
)
GO

CREATE TABLE [activity_logs] (
  [log_id] bigint PRIMARY KEY IDENTITY(1, 1),
  [actor_type_id] tinyint NOT NULL,
  [actor_user_id] integer,
  [action] varchar(100) NOT NULL,
  [entity_type] varchar(100) NOT NULL,
  [entity_id] integer NOT NULL,
  [description] nvarchar(max) NOT NULL,
  [reason] nvarchar(max),
  [old_values] nvarchar(max),
  [new_values] nvarchar(max),
  [occurred_at] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME())
)
GO

CREATE UNIQUE INDEX [UX_account_requests_created_user_id_not_null]
    ON [account_requests] ([created_user_id])
    WHERE [created_user_id] IS NOT NULL
GO
CREATE UNIQUE INDEX [UX_account_requests_pending_email]
    ON [account_requests] ([email])
    WHERE [account_request_status_id] = 1
GO
CREATE UNIQUE INDEX [UX_account_requests_pending_student_number]
    ON [account_requests] ([student_number])
    WHERE [account_request_status_id] = 1
GO
CREATE UNIQUE INDEX [UX_members_user_id]
    ON [members] ([user_id])
GO
CREATE UNIQUE INDEX [UX_registrations_qr_token_not_null]
    ON [registrations] ([qr_token])
    WHERE [qr_token] IS NOT NULL
GO
CREATE UNIQUE INDEX [UX_certificates_certificate_number_not_null]
    ON [certificates] ([certificate_number])
    WHERE [certificate_number] IS NOT NULL
GO

CREATE INDEX [account_requests_index_0] ON [account_requests] ([account_request_status_id], [requested_at], [request_id])
GO
CREATE INDEX [account_requests_index_1] ON [account_requests] ([email])
GO
CREATE INDEX [account_requests_index_2] ON [account_requests] ([student_number])
GO
CREATE INDEX [events_index_3] ON [events] ([event_status_id], [starts_at], [event_id])
GO
CREATE INDEX [events_index_4] ON [events] ([type_id])
GO
CREATE INDEX [events_index_5] ON [events] ([venue_id])
GO
CREATE UNIQUE INDEX [unique_event_member_registration] ON [registrations] ([event_id], [member_id])
GO
CREATE INDEX [registrations_index_7] ON [registrations] ([event_id], [registration_status_id], [reviewed_at], [registration_id])
GO
CREATE INDEX [registrations_index_8] ON [registrations] ([member_id], [submitted_at], [registration_id])
GO
CREATE INDEX [activity_logs_index_9] ON [activity_logs] ([occurred_at], [log_id])
GO
CREATE INDEX [activity_logs_index_10] ON [activity_logs] ([actor_user_id], [occurred_at], [log_id])
GO
CREATE INDEX [activity_logs_index_11] ON [activity_logs] ([entity_type], [entity_id], [occurred_at], [log_id])
GO

ALTER TABLE [users] ADD FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [users] ADD FOREIGN KEY ([updated_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [account_requests] ADD FOREIGN KEY ([reviewed_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [account_requests] ADD FOREIGN KEY ([created_user_id]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [members] ADD FOREIGN KEY ([user_id]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [members] ADD FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [members] ADD FOREIGN KEY ([updated_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [event_types] ADD FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [event_types] ADD FOREIGN KEY ([updated_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [venues] ADD FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [venues] ADD FOREIGN KEY ([updated_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([type_id]) REFERENCES [event_types] ([type_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([venue_id]) REFERENCES [venues] ([venue_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([published_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([check_in_opened_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([check_in_closed_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([completed_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([cancelled_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [events] ADD FOREIGN KEY ([updated_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [registrations] ADD FOREIGN KEY ([member_id]) REFERENCES [members] ([member_id])
GO
ALTER TABLE [registrations] ADD FOREIGN KEY ([event_id]) REFERENCES [events] ([event_id])
GO
ALTER TABLE [registrations] ADD FOREIGN KEY ([reviewed_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [registrations] ADD FOREIGN KEY ([confirmed_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [registrations] ADD FOREIGN KEY ([cancelled_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [registrations] ADD FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [registrations] ADD FOREIGN KEY ([updated_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [attendance] ADD FOREIGN KEY ([registration_id]) REFERENCES [registrations] ([registration_id])
GO
ALTER TABLE [attendance] ADD FOREIGN KEY ([recorded_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [attendance] ADD FOREIGN KEY ([invalidated_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [certificates] ADD FOREIGN KEY ([registration_id]) REFERENCES [registrations] ([registration_id])
GO
ALTER TABLE [certificates] ADD FOREIGN KEY ([reviewed_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [certificates] ADD FOREIGN KEY ([issued_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [certificates] ADD FOREIGN KEY ([created_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [certificates] ADD FOREIGN KEY ([updated_by]) REFERENCES [users] ([user_id])
GO
ALTER TABLE [activity_logs] ADD FOREIGN KEY ([actor_user_id]) REFERENCES [users] ([user_id])
GO

ALTER TABLE [users] ADD CONSTRAINT [FK_users_roles] FOREIGN KEY ([role_id]) REFERENCES [roles] ([role_id])
GO
ALTER TABLE [account_requests] ADD CONSTRAINT [FK_account_requests_statuses] FOREIGN KEY ([account_request_status_id]) REFERENCES [account_request_statuses] ([account_request_status_id])
GO
ALTER TABLE [members] ADD CONSTRAINT [FK_members_statuses] FOREIGN KEY ([membership_status_id]) REFERENCES [membership_statuses] ([membership_status_id])
GO
ALTER TABLE [events] ADD CONSTRAINT [FK_events_statuses] FOREIGN KEY ([event_status_id]) REFERENCES [event_statuses] ([event_status_id])
GO
ALTER TABLE [registrations] ADD CONSTRAINT [FK_registrations_statuses] FOREIGN KEY ([registration_status_id]) REFERENCES [registration_statuses] ([registration_status_id])
GO
ALTER TABLE [attendance] ADD CONSTRAINT [FK_attendance_methods] FOREIGN KEY ([check_in_method_id]) REFERENCES [check_in_methods] ([check_in_method_id])
GO
ALTER TABLE [certificates] ADD CONSTRAINT [FK_certificates_statuses] FOREIGN KEY ([certificate_status_id]) REFERENCES [certificate_statuses] ([certificate_status_id])
GO
ALTER TABLE [activity_logs] ADD CONSTRAINT [FK_activity_logs_actor_types] FOREIGN KEY ([actor_type_id]) REFERENCES [actor_types] ([actor_type_id])
GO

CREATE TRIGGER [dbo].[TR_activity_logs_append_only]
ON [dbo].[activity_logs]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51002, 'activity_logs is append-only.', 1;
END
GO

EXEC sp_addextendedproperty @name=N'Table_Description', @value=N'Enforce pending-only uniqueness separately for email and student number in SQL Server. Password hash is required while pending and cleared after approval/rejection.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'account_requests';
GO
EXEC sp_addextendedproperty @name=N'Column_Description', @value=N'Account created upon approval; unique filtered index excludes NULL values.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'account_requests', @level2type=N'Column', @level2name=N'created_user_id';
GO
EXEC sp_addextendedproperty @name=N'Table_Description', @value=N'A validated venue photo is required at creation; replacement is permitted, removal without replacement is not.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'venues';
GO
EXEC sp_addextendedproperty @name=N'Column_Description', @value=N'Required relative local image path.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'venues', @level2type=N'Column', @level2name=N'image_path';
GO
EXEC sp_addextendedproperty @name=N'Table_Description', @value=N'Publishing opens signup before the deadline and requires a cover. After publication only title, description and cover may change. At deadline signup closes without changing event status.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'events';
GO
EXEC sp_addextendedproperty @name=N'Column_Description', @value=N'Optional for draft only; validated nonblank cover required for published/completed events.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'events', @level2type=N'Column', @level2name=N'image_path';
GO
EXEC sp_addextendedproperty @name=N'Table_Description', @value=N'One row per member/event, no reapplication. Officer-triggered promotion only; queue: reviewed_at ASC, registration_id ASC. At start pending/waitlisted remain non-actionable history, not no-shows. Capacity/cutoff/eligibility checks require transactional server enforcement.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'registrations';
GO
EXEC sp_addextendedproperty @name=N'Column_Description', @value=N'Nullable; unique filtered index WHERE qr_token IS NOT NULL.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'registrations', @level2type=N'Column', @level2name=N'qr_token';
GO
EXEC sp_addextendedproperty @name=N'Table_Description', @value=N'One fixed PDF template; no bulk issuance, revocation or reissue. Issued certificates require review, issuance metadata, snapshots, certificate number and a retained landscape A4 PDF at file_path (application/pdf).', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'certificates';
GO
EXEC sp_addextendedproperty @name=N'Column_Description', @value=N'Assigned at issuance; unique filtered index excludes NULL values.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'certificates', @level2type=N'Column', @level2name=N'certificate_number';
GO
EXEC sp_addextendedproperty @name=N'Column_Description', @value=N'Relative private server-local PDF file path; required when issued. Serve through an authorized backend endpoint, not static files.', @level0type=N'Schema', @level0name=N'dbo', @level1type=N'Table', @level1name=N'certificates', @level2type=N'Column', @level2name=N'file_path';
GO
