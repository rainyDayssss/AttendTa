using System;
using System.Linq.Expressions;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data
{
    public partial class AsiBasecodeDBContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            ConfigureLookup<RoleLookup, RoleCode>(modelBuilder, "roles", "role_id", entity => entity.RoleId);
            ConfigureLookup<AccountRequestStatusLookup, AccountRequestStatusCode>(modelBuilder, "account_request_statuses", "account_request_status_id", entity => entity.AccountRequestStatusId);
            ConfigureLookup<MembershipStatusLookup, MembershipStatusCode>(modelBuilder, "membership_statuses", "membership_status_id", entity => entity.MembershipStatusId);
            ConfigureLookup<EventStatusLookup, EventStatusCode>(modelBuilder, "event_statuses", "event_status_id", entity => entity.EventStatusId);
            ConfigureLookup<RegistrationStatusLookup, RegistrationStatusCode>(modelBuilder, "registration_statuses", "registration_status_id", entity => entity.RegistrationStatusId);
            ConfigureLookup<CheckInMethodLookup, CheckInMethodCode>(modelBuilder, "check_in_methods", "check_in_method_id", entity => entity.CheckInMethodId);
            ConfigureLookup<CertificateStatusLookup, CertificateStatusCode>(modelBuilder, "certificate_statuses", "certificate_status_id", entity => entity.CertificateStatusId);
            ConfigureLookup<ActorTypeLookup, ActorTypeCode>(modelBuilder, "actor_types", "actor_type_id", entity => entity.ActorTypeId);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(user => user.Id);
                entity.HasIndex(user => user.UserId).IsUnique();
                entity.Property(user => user.Id).ValueGeneratedOnAdd().HasColumnName("user_id");
                entity.Property(user => user.UserId).IsRequired().HasMaxLength(255).HasColumnName("email");
                entity.Property(user => user.Name).IsRequired().HasMaxLength(255).HasColumnName("full_name");
                entity.Property(user => user.Password).IsRequired().HasColumnType("varchar(255)").HasMaxLength(255).HasColumnName("password_hash");
                entity.Property(user => user.RoleId).HasConversion<byte>().HasColumnType("tinyint").HasColumnName("role_id").HasDefaultValue(RoleCode.Member);
                entity.Property(user => user.IsActive).IsRequired().HasColumnName("is_active").HasDefaultValue(false);
                entity.Property(user => user.CreatedById).HasColumnName("created_by");
                entity.Property(user => user.CreatedTime).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(user => user.UpdatedById).HasColumnName("updated_by");
                entity.Property(user => user.UpdatedTime).HasColumnType("datetime2(3)").HasColumnName("updated_at");
                entity.Ignore(user => user.CreatedBy);
                entity.Ignore(user => user.UpdatedBy);
                entity.HasOne<RoleLookup>().WithMany().HasForeignKey(user => user.RoleId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(user => user.CreatedById).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(user => user.UpdatedById).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<AccountRequest>(entity =>
            {
                entity.ToTable("account_requests", table =>
                {
                    table.HasCheckConstraint("chk_account_requests_positive_year_level", "year_level > 0");
                    table.HasCheckConstraint("chk_account_requests_password_by_status", "(account_request_status_id = 1 AND password_hash IS NOT NULL) OR (account_request_status_id IN (2, 3) AND password_hash IS NULL)");
                });
                entity.HasKey(request => request.RequestId);
                entity.HasIndex(request => request.CreatedUserId).IsUnique().HasFilter("[created_user_id] IS NOT NULL").HasDatabaseName("UX_account_requests_created_user_id_not_null");
                entity.HasIndex(request => request.Email).IsUnique().HasFilter("[account_request_status_id] = 1").HasDatabaseName("UX_account_requests_pending_email");
                entity.HasIndex(request => request.StudentNumber).IsUnique().HasFilter("[account_request_status_id] = 1").HasDatabaseName("UX_account_requests_pending_student_number");
                entity.HasIndex(request => new { request.AccountRequestStatusId, request.RequestedAt, request.RequestId }).HasDatabaseName("account_requests_index_0");
                entity.HasIndex(request => request.Email).HasDatabaseName("account_requests_index_1");
                entity.HasIndex(request => request.StudentNumber).HasDatabaseName("account_requests_index_2");
                entity.Property(request => request.RequestId).ValueGeneratedOnAdd().HasColumnName("request_id");
                entity.Property(request => request.StudentNumber).IsRequired().HasMaxLength(50).HasColumnName("student_number");
                entity.Property(request => request.FullName).IsRequired().HasMaxLength(255).HasColumnName("full_name");
                entity.Property(request => request.Email).IsRequired().HasMaxLength(255).HasColumnName("email");
                entity.Property(request => request.PasswordHash).HasColumnType("varchar(255)").HasMaxLength(255).HasColumnName("password_hash");
                entity.Property(request => request.Course).IsRequired().HasMaxLength(100).HasColumnName("course");
                entity.Property(request => request.YearLevel).IsRequired().HasColumnName("year_level");
                entity.Property(request => request.Section).IsRequired().HasMaxLength(50).HasColumnName("section");
                entity.Property(request => request.MobileNumber).HasColumnType("varchar(30)").HasMaxLength(30).HasColumnName("mobile_number");
                entity.Property(request => request.AccountRequestStatusId).HasConversion<byte>().HasColumnType("tinyint").HasColumnName("account_request_status_id").HasDefaultValue(AccountRequestStatusCode.Pending);
                entity.Property(request => request.RequestedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("requested_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(request => request.ReviewedBy).HasColumnName("reviewed_by");
                entity.Property(request => request.ReviewedAt).HasColumnType("datetime2(3)").HasColumnName("reviewed_at");
                entity.Property(request => request.ReviewNotes).HasColumnType("nvarchar(max)").HasColumnName("review_notes");
                entity.Property(request => request.CreatedUserId).HasColumnName("created_user_id");
                entity.HasOne<AccountRequestStatusLookup>().WithMany().HasForeignKey(request => request.AccountRequestStatusId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(request => request.ReviewedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(request => request.CreatedUserId).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Member>(entity =>
            {
                entity.ToTable("members", table => table.HasCheckConstraint("chk_members_positive_year_level", "year_level > 0"));
                entity.HasKey(member => member.MemberId);
                entity.HasIndex(member => member.StudentNumber).IsUnique().HasDatabaseName("UQ__members__0E749A79D636F2B5");
                entity.HasIndex(member => member.UserId).IsUnique().HasDatabaseName("UX_members_user_id");
                entity.Property(member => member.MemberId).ValueGeneratedOnAdd().HasColumnName("member_id");
                entity.Property(member => member.UserId).IsRequired().HasColumnName("user_id");
                entity.Property(member => member.StudentNumber).IsRequired().HasMaxLength(50).HasColumnName("student_number");
                entity.Property(member => member.Course).IsRequired().HasMaxLength(100).HasColumnName("course");
                entity.Property(member => member.YearLevel).IsRequired().HasColumnName("year_level");
                entity.Property(member => member.Section).IsRequired().HasMaxLength(50).HasColumnName("section");
                entity.Property(member => member.MobileNumber).HasColumnType("varchar(30)").HasMaxLength(30).HasColumnName("mobile_number");
                entity.Property(member => member.MembershipType).HasMaxLength(100).HasColumnName("membership_type");
                entity.Property(member => member.MembershipStatusId).HasConversion<byte>().HasColumnType("tinyint").HasColumnName("membership_status_id").HasDefaultValue(MembershipStatusCode.Active);
                entity.Ignore(member => member.Status);
                entity.Property(member => member.CreatedBy).IsRequired().HasColumnName("created_by");
                entity.Property(member => member.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(member => member.UpdatedBy).HasColumnName("updated_by");
                entity.Property(member => member.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
                entity.HasOne<User>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<MembershipStatusLookup>().WithMany().HasForeignKey(member => member.MembershipStatusId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(member => member.CreatedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(member => member.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<EventType>(entity =>
            {
                entity.ToTable("event_types");
                entity.HasKey(eventType => eventType.TypeId);
                entity.HasIndex(eventType => eventType.TypeName).IsUnique().HasDatabaseName("UQ__event_ty__543C4FD9CB67C4B3");
                entity.Property(eventType => eventType.TypeId).ValueGeneratedOnAdd().HasColumnName("type_id");
                entity.Property(eventType => eventType.TypeName).IsRequired().HasMaxLength(100).HasColumnName("type_name");
                entity.Property(eventType => eventType.Description).IsRequired().HasColumnType("nvarchar(max)").HasColumnName("description");
                entity.Property(eventType => eventType.IsActive).IsRequired().HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(eventType => eventType.CreatedBy).IsRequired().HasColumnName("created_by");
                entity.Property(eventType => eventType.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(eventType => eventType.UpdatedBy).HasColumnName("updated_by");
                entity.Property(eventType => eventType.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
                entity.HasOne<User>().WithMany().HasForeignKey(eventType => eventType.CreatedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(eventType => eventType.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Venue>(entity =>
            {
                entity.ToTable("venues", table =>
                {
                    table.HasCheckConstraint("chk_venues_positive_capacity", "capacity > 0");
                    table.HasCheckConstraint("chk_venues_required_photo", "LEN(LTRIM(RTRIM(image_path))) > 0");
                });
                entity.HasKey(venue => venue.VenueId);
                entity.Property(venue => venue.VenueId).ValueGeneratedOnAdd().HasColumnName("venue_id");
                entity.Property(venue => venue.RoomName).IsRequired().HasMaxLength(150).HasColumnName("room_name");
                entity.Property(venue => venue.Capacity).IsRequired().HasColumnName("capacity");
                entity.Property(venue => venue.Location).HasMaxLength(255).HasColumnName("location");
                entity.Property(venue => venue.AvailabilityNotes).HasColumnType("nvarchar(max)").HasColumnName("availability_notes");
                entity.Property(venue => venue.ContactPerson).HasMaxLength(150).HasColumnName("contact_person");
                entity.Property(venue => venue.IsActive).IsRequired().HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(venue => venue.ImagePath).IsRequired().HasMaxLength(500).HasColumnName("image_path");
                entity.Property(venue => venue.CreatedBy).IsRequired().HasColumnName("created_by");
                entity.Property(venue => venue.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(venue => venue.UpdatedBy).HasColumnName("updated_by");
                entity.Property(venue => venue.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
                entity.HasOne<User>().WithMany().HasForeignKey(venue => venue.CreatedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(venue => venue.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("events", table =>
                {
                    table.HasCheckConstraint("chk_events_positive_participant_limit", "participant_limit > 0");
                    table.HasCheckConstraint("chk_events_registration_deadline", "registration_deadline <= starts_at");
                });
                entity.HasKey(eventItem => eventItem.EventId);
                entity.HasIndex(eventItem => new { eventItem.EventStatusId, eventItem.StartsAt, eventItem.EventId }).HasDatabaseName("events_index_3");
                entity.HasIndex(eventItem => eventItem.TypeId).HasDatabaseName("events_index_4");
                entity.HasIndex(eventItem => eventItem.VenueId).HasDatabaseName("events_index_5");
                entity.Property(eventItem => eventItem.EventId).ValueGeneratedOnAdd().HasColumnName("event_id");
                entity.Property(eventItem => eventItem.Title).IsRequired().HasMaxLength(255).HasColumnName("title");
                entity.Property(eventItem => eventItem.Description).HasColumnType("nvarchar(max)").HasColumnName("description");
                entity.Property(eventItem => eventItem.TypeId).IsRequired().HasColumnName("type_id");
                entity.Property(eventItem => eventItem.VenueId).IsRequired().HasColumnName("venue_id");
                entity.Property(eventItem => eventItem.StartsAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("starts_at");
                entity.Property(eventItem => eventItem.RegistrationDeadline).IsRequired().HasColumnType("datetime2(3)").HasColumnName("registration_deadline");
                entity.Property(eventItem => eventItem.ParticipantLimit).IsRequired().HasColumnName("participant_limit");
                entity.Property(eventItem => eventItem.EventStatusId).HasConversion<byte>().HasColumnType("tinyint").HasColumnName("event_status_id").HasDefaultValue(EventStatusCode.Draft);
                entity.Property(eventItem => eventItem.ImagePath).HasMaxLength(500).HasColumnName("image_path");
                entity.Property(eventItem => eventItem.PublishedBy).HasColumnName("published_by");
                entity.Property(eventItem => eventItem.PublishedAt).HasColumnType("datetime2(3)").HasColumnName("published_at");
                entity.Property(eventItem => eventItem.IsCheckInOpen).IsRequired().HasColumnName("is_check_in_open").HasDefaultValue(false);
                entity.Property(eventItem => eventItem.CheckInOpenedBy).HasColumnName("check_in_opened_by");
                entity.Property(eventItem => eventItem.CheckInOpenedAt).HasColumnType("datetime2(3)").HasColumnName("check_in_opened_at");
                entity.Property(eventItem => eventItem.CheckInClosedBy).HasColumnName("check_in_closed_by");
                entity.Property(eventItem => eventItem.CheckInClosedAt).HasColumnType("datetime2(3)").HasColumnName("check_in_closed_at");
                entity.Property(eventItem => eventItem.CompletedBy).HasColumnName("completed_by");
                entity.Property(eventItem => eventItem.CompletedAt).HasColumnType("datetime2(3)").HasColumnName("completed_at");
                entity.Property(eventItem => eventItem.CancelledBy).HasColumnName("cancelled_by");
                entity.Property(eventItem => eventItem.CancelledAt).HasColumnType("datetime2(3)").HasColumnName("cancelled_at");
                entity.Property(eventItem => eventItem.CancellationReason).HasColumnType("nvarchar(max)").HasColumnName("cancellation_reason");
                entity.Property(eventItem => eventItem.CreatedBy).IsRequired().HasColumnName("created_by");
                entity.Property(eventItem => eventItem.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(eventItem => eventItem.UpdatedBy).HasColumnName("updated_by");
                entity.Property(eventItem => eventItem.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
                entity.HasOne<EventType>().WithMany().HasForeignKey(eventItem => eventItem.TypeId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Venue>().WithMany().HasForeignKey(eventItem => eventItem.VenueId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<EventStatusLookup>().WithMany().HasForeignKey(eventItem => eventItem.EventStatusId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(eventItem => eventItem.PublishedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(eventItem => eventItem.CheckInOpenedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(eventItem => eventItem.CheckInClosedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(eventItem => eventItem.CompletedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(eventItem => eventItem.CancelledBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(eventItem => eventItem.CreatedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(eventItem => eventItem.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Registration>(entity =>
            {
                entity.ToTable("registrations", table => table.HasCheckConstraint("chk_registrations_positive_year_level", "year_level_snapshot > 0"));
                entity.HasKey(registration => registration.RegistrationId);
                entity.HasIndex(registration => new { registration.EventId, registration.MemberId }).IsUnique().HasDatabaseName("unique_event_member_registration");
                entity.HasIndex(registration => registration.QrToken).IsUnique().HasFilter("[qr_token] IS NOT NULL").HasDatabaseName("UX_registrations_qr_token_not_null");
                entity.HasIndex(registration => new { registration.EventId, registration.RegistrationStatusId, registration.ReviewedAt, registration.RegistrationId }).HasDatabaseName("registrations_index_7");
                entity.HasIndex(registration => new { registration.MemberId, registration.SubmittedAt, registration.RegistrationId }).HasDatabaseName("registrations_index_8");
                entity.Property(registration => registration.RegistrationId).ValueGeneratedOnAdd().HasColumnName("registration_id");
                entity.Property(registration => registration.MemberId).IsRequired().HasColumnName("member_id");
                entity.Property(registration => registration.EventId).IsRequired().HasColumnName("event_id");
                entity.Property(registration => registration.RegistrationStatusId).HasConversion<byte>().HasColumnType("tinyint").HasColumnName("registration_status_id").HasDefaultValue(RegistrationStatusCode.Pending);
                entity.Property(registration => registration.CourseSnapshot).IsRequired().HasMaxLength(100).HasColumnName("course_snapshot");
                entity.Property(registration => registration.YearLevelSnapshot).IsRequired().HasColumnName("year_level_snapshot");
                entity.Property(registration => registration.SectionSnapshot).IsRequired().HasMaxLength(50).HasColumnName("section_snapshot");
                entity.Property(registration => registration.SubmittedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("submitted_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(registration => registration.ReviewedBy).HasColumnName("reviewed_by");
                entity.Property(registration => registration.ReviewedAt).HasColumnType("datetime2(3)").HasColumnName("reviewed_at");
                entity.Property(registration => registration.ReviewNotes).HasColumnType("nvarchar(max)").HasColumnName("review_notes");
                entity.Property(registration => registration.ConfirmedBy).HasColumnName("confirmed_by");
                entity.Property(registration => registration.ConfirmedAt).HasColumnType("datetime2(3)").HasColumnName("confirmed_at");
                entity.Property(registration => registration.QrToken).HasColumnType("varchar(64)").HasMaxLength(64).HasColumnName("qr_token");
                entity.Property(registration => registration.CancelledBy).HasColumnName("cancelled_by");
                entity.Property(registration => registration.CancelledAt).HasColumnType("datetime2(3)").HasColumnName("cancelled_at");
                entity.Property(registration => registration.CancellationReason).HasColumnType("nvarchar(max)").HasColumnName("cancellation_reason");
                entity.Property(registration => registration.CreatedBy).IsRequired().HasColumnName("created_by");
                entity.Property(registration => registration.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(registration => registration.UpdatedBy).HasColumnName("updated_by");
                entity.Property(registration => registration.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
                entity.HasOne<Member>().WithMany().HasForeignKey(registration => registration.MemberId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<Event>().WithMany().HasForeignKey(registration => registration.EventId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<RegistrationStatusLookup>().WithMany().HasForeignKey(registration => registration.RegistrationStatusId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(registration => registration.ReviewedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(registration => registration.ConfirmedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(registration => registration.CancelledBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(registration => registration.CreatedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(registration => registration.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.ToTable("attendance");
                entity.HasKey(attendance => attendance.AttendanceId);
                entity.HasIndex(attendance => attendance.RegistrationId).IsUnique();
                entity.Property(attendance => attendance.AttendanceId).ValueGeneratedOnAdd().HasColumnName("attendance_id");
                entity.Property(attendance => attendance.RegistrationId).IsRequired().HasColumnName("registration_id");
                entity.Property(attendance => attendance.CheckInMethodId).HasConversion<byte>().HasColumnType("tinyint").HasColumnName("check_in_method_id");
                entity.Property(attendance => attendance.RecordedBy).IsRequired().HasColumnName("recorded_by");
                entity.Property(attendance => attendance.RecordedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("recorded_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(attendance => attendance.InvalidatedBy).HasColumnName("invalidated_by");
                entity.Property(attendance => attendance.InvalidatedAt).HasColumnType("datetime2(3)").HasColumnName("invalidated_at");
                entity.Property(attendance => attendance.InvalidationReason).HasColumnType("nvarchar(max)").HasColumnName("invalidation_reason");
                entity.HasOne<Registration>().WithMany().HasForeignKey(attendance => attendance.RegistrationId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<CheckInMethodLookup>().WithMany().HasForeignKey(attendance => attendance.CheckInMethodId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(attendance => attendance.RecordedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(attendance => attendance.InvalidatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Certificate>(entity =>
            {
                entity.ToTable("certificates");
                entity.HasKey(certificate => certificate.CertificateId);
                entity.HasIndex(certificate => certificate.RegistrationId).IsUnique();
                entity.HasIndex(certificate => certificate.CertificateNumber).IsUnique().HasFilter("[certificate_number] IS NOT NULL").HasDatabaseName("UX_certificates_certificate_number_not_null");
                entity.Property(certificate => certificate.CertificateId).ValueGeneratedOnAdd().HasColumnName("certificate_id");
                entity.Property(certificate => certificate.RegistrationId).IsRequired().HasColumnName("registration_id");
                entity.Property(certificate => certificate.CertificateStatusId).HasConversion<byte>().HasColumnType("tinyint").HasColumnName("certificate_status_id").HasDefaultValue(CertificateStatusCode.PendingApproval);
                entity.Property(certificate => certificate.ReviewedBy).HasColumnName("reviewed_by");
                entity.Property(certificate => certificate.ReviewedAt).HasColumnType("datetime2(3)").HasColumnName("reviewed_at");
                entity.Property(certificate => certificate.ReviewNotes).HasColumnType("nvarchar(max)").HasColumnName("review_notes");
                entity.Property(certificate => certificate.CertificateNumber).HasColumnType("varchar(100)").HasMaxLength(100).HasColumnName("certificate_number");
                entity.Property(certificate => certificate.RecipientNameSnapshot).HasMaxLength(255).HasColumnName("recipient_name_snapshot");
                entity.Property(certificate => certificate.EventTitleSnapshot).HasMaxLength(255).HasColumnName("event_title_snapshot");
                entity.Property(certificate => certificate.EventStartsAtSnapshot).HasColumnType("datetime2(3)").HasColumnName("event_starts_at_snapshot");
                entity.Property(certificate => certificate.IssuerNameSnapshot).HasMaxLength(255).HasColumnName("issuer_name_snapshot");
                entity.Property(certificate => certificate.FilePath).HasMaxLength(500).HasColumnName("file_path");
                entity.Property(certificate => certificate.IssuedBy).HasColumnName("issued_by");
                entity.Property(certificate => certificate.IssuedAt).HasColumnType("datetime2(3)").HasColumnName("issued_at");
                entity.Property(certificate => certificate.CreatedBy).HasColumnName("created_by");
                entity.Property(certificate => certificate.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(certificate => certificate.UpdatedBy).HasColumnName("updated_by");
                entity.Property(certificate => certificate.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
                entity.HasOne<Registration>().WithMany().HasForeignKey(certificate => certificate.RegistrationId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<CertificateStatusLookup>().WithMany().HasForeignKey(certificate => certificate.CertificateStatusId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(certificate => certificate.ReviewedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(certificate => certificate.IssuedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(certificate => certificate.CreatedBy).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(certificate => certificate.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.ToTable("activity_logs");
                entity.HasKey(log => log.LogId);
                entity.HasIndex(log => new { log.OccurredAt, log.LogId }).HasDatabaseName("activity_logs_index_9");
                entity.HasIndex(log => new { log.ActorUserId, log.OccurredAt, log.LogId }).HasDatabaseName("activity_logs_index_10");
                entity.HasIndex(log => new { log.EntityType, log.EntityId, log.OccurredAt, log.LogId }).HasDatabaseName("activity_logs_index_11");
                entity.Property(log => log.LogId).ValueGeneratedOnAdd().HasColumnName("log_id");
                entity.Property(log => log.ActorTypeId).HasConversion<byte>().HasColumnType("tinyint").HasColumnName("actor_type_id");
                entity.Property(log => log.ActorUserId).HasColumnName("actor_user_id");
                entity.Property(log => log.Action).IsRequired().HasColumnType("varchar(100)").HasMaxLength(100).HasColumnName("action");
                entity.Property(log => log.EntityType).IsRequired().HasColumnType("varchar(100)").HasMaxLength(100).HasColumnName("entity_type");
                entity.Property(log => log.EntityId).IsRequired().HasColumnName("entity_id");
                entity.Property(log => log.Description).IsRequired().HasColumnType("nvarchar(max)").HasColumnName("description");
                entity.Property(log => log.Reason).HasColumnType("nvarchar(max)").HasColumnName("reason");
                entity.Property(log => log.OldValues).HasColumnType("nvarchar(max)").HasColumnName("old_values");
                entity.Property(log => log.NewValues).HasColumnType("nvarchar(max)").HasColumnName("new_values");
                // SQL Server stores datetime2 without a kind. Restore the UTC marker on read so every
                // consumer (view, reader, test) sees the same instant the writer stored, and no view
                // has to re-specify it.
                entity.Property(log => log.OccurredAt)
                    .HasConversion(
                        value => value,
                        value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
                    .IsRequired()
                    .HasColumnType("datetime2(3)")
                    .HasColumnName("occurred_at")
                    .HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasOne<ActorTypeLookup>().WithMany().HasForeignKey(log => log.ActorTypeId).OnDelete(DeleteBehavior.NoAction);
                entity.HasOne<User>().WithMany().HasForeignKey(log => log.ActorUserId).OnDelete(DeleteBehavior.NoAction);
            });
        }

        private static void ConfigureLookup<TEntity, TEnum>(
            ModelBuilder modelBuilder,
            string tableName,
            string keyColumn,
            Expression<Func<TEntity, TEnum>> keyProperty)
            where TEntity : class
            where TEnum : struct, Enum
        {
            var entity = modelBuilder.Entity<TEntity>();
            var propertyName = ((MemberExpression)keyProperty.Body).Member.Name;
            entity.ToTable(tableName);
            entity.HasKey(propertyName);
            entity.Property<TEnum>(propertyName).HasConversion<byte>().HasColumnType("tinyint").HasColumnName(keyColumn).ValueGeneratedNever();
            entity.Property<string>("Code").IsRequired().HasMaxLength(50).IsUnicode(false).HasColumnName("code");
            entity.HasIndex("Code").IsUnique();
            entity.Property<string>("Label").IsRequired().HasMaxLength(100).HasColumnName("label");
        }
    }
}
