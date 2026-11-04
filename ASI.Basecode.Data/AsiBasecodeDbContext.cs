using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data
{
    public partial class AsiBasecodeDBContext : DbContext
    {
        public AsiBasecodeDBContext()
        {
        }

        public AsiBasecodeDBContext(DbContextOptions<AsiBasecodeDBContext> options)
            : base(options)
        {
        }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<AccountRequest> AccountRequests { get; set; }
        public virtual DbSet<Member> Members { get; set; }
        public virtual DbSet<MemberAccountSummary> MemberAccountSummaries { get; set; }
        public virtual DbSet<EventType> EventTypes { get; set; }
        public virtual DbSet<Venue> Venues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).HasColumnName("user_id").ValueGeneratedOnAdd();
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(255).HasColumnName("full_name");
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255).HasColumnName("email");
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255).IsUnicode(false).HasColumnName("password_hash");
                entity.Property(e => e.Role).IsRequired().HasMaxLength(255).HasColumnName("role");
                entity.Property(e => e.IsActive).IsRequired().HasColumnName("is_active").HasDefaultValue(false);
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
            });

            modelBuilder.Entity<AccountRequest>(entity =>
            {
                entity.ToTable("account_requests", table =>
                {
                    table.HasCheckConstraint("chk_account_requests_positive_year_level", "year_level > 0");
                    table.HasCheckConstraint("chk_account_requests_password_by_status", "([status] = 'pending' AND [password_hash] IS NOT NULL) OR ([status] IN ('approved', 'rejected') AND [password_hash] IS NULL)");
                });
                entity.HasKey(e => e.RequestId);
                entity.Property(e => e.RequestId).HasColumnName("request_id").ValueGeneratedOnAdd();
                entity.Property(e => e.StudentNumber).IsRequired().HasMaxLength(50).HasColumnName("student_number");
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(255).HasColumnName("full_name");
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasMaxLength(255).IsUnicode(false).HasColumnName("password_hash");
                entity.Property(e => e.Course).IsRequired().HasMaxLength(100).HasColumnName("course");
                entity.Property(e => e.YearLevel).IsRequired().HasColumnName("year_level");
                entity.Property(e => e.Section).IsRequired().HasMaxLength(50).HasColumnName("section");
                entity.Property(e => e.MobileNumber).HasMaxLength(30).IsUnicode(false).HasColumnName("mobile_number");
                entity.Property(e => e.Status).IsRequired().HasMaxLength(255).HasColumnName("status").HasDefaultValue("pending");
                entity.Property(e => e.RequestedAt).HasColumnType("datetime2(3)").HasColumnName("requested_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
                entity.Property(e => e.ReviewedAt).HasColumnType("datetime2(3)").HasColumnName("reviewed_at");
                entity.Property(e => e.ReviewNotes).HasColumnName("review_notes");
                entity.Property(e => e.CreatedUserId).HasColumnName("created_user_id");
                entity.HasIndex(e => e.Email).HasDatabaseName("account_requests_index_1");
                entity.HasIndex(e => e.StudentNumber).HasDatabaseName("account_requests_index_2");
                entity.HasIndex(e => e.Email).IsUnique().HasFilter("[status] = 'pending'").HasDatabaseName("UX_account_requests_pending_email");
                entity.HasIndex(e => e.StudentNumber).IsUnique().HasFilter("[status] = 'pending'").HasDatabaseName("UX_account_requests_pending_student_number");
            });

            modelBuilder.Entity<Member>(entity =>
            {
                entity.HasKey(e => e.MemberId);

                entity.ToTable("members", table => table.HasCheckConstraint("chk_members_positive_year_level", "year_level > 0"));

                entity.HasIndex(e => e.StudentNumber, "UQ__members__0E749A79D636F2B5").IsUnique();
                entity.HasIndex(e => e.UserId, "UX_members_user_id").IsUnique();

                entity.Property(e => e.MemberId).ValueGeneratedOnAdd().HasColumnName("member_id");
                entity.Property(e => e.UserId).IsRequired().HasColumnName("user_id");
                entity.Property(e => e.StudentNumber).IsRequired().HasMaxLength(50).HasColumnName("student_number");
                entity.Property(e => e.Course).IsRequired().HasMaxLength(100).HasColumnName("course");
                entity.Property(e => e.YearLevel).IsRequired().HasColumnName("year_level");
                entity.Property(e => e.Section).IsRequired().HasMaxLength(50).HasColumnName("section");
                entity.Property(e => e.MobileNumber).HasColumnType("varchar(30)").HasColumnName("mobile_number");
                entity.Property(e => e.MembershipType).HasMaxLength(100).HasColumnName("membership_type");
                entity.Property(e => e.Status).IsRequired().HasMaxLength(255).HasColumnName("status").HasDefaultValue("active");
                entity.Property(e => e.CreatedBy).IsRequired().HasColumnName("created_by");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
            });

            modelBuilder.Entity<MemberAccountSummary>(entity =>
            {
                entity.HasNoKey();
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.ToSqlQuery("SELECT [user_id], [full_name], [email] FROM [dbo].[users]");
            });

            modelBuilder.Entity<EventType>(entity =>
            {
                entity.HasKey(e => e.TypeId);

                entity.ToTable("event_types");

                entity.HasIndex(e => e.TypeName, "UQ__event_ty__543C4FD9CB67C4B3").IsUnique();

                entity.Property(e => e.TypeId).ValueGeneratedOnAdd().HasColumnName("type_id");
                entity.Property(e => e.TypeName).IsRequired().HasMaxLength(100).HasColumnName("type_name");
                entity.Property(e => e.Description).HasColumnType("nvarchar(max)").HasColumnName("description");
                entity.Property(e => e.IsActive).IsRequired().HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.CreatedBy).IsRequired().HasColumnName("created_by");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
            });

            modelBuilder.Entity<Venue>(entity =>
            {
                entity.HasKey(e => e.VenueId);

                entity.ToTable("venues", table =>
                {
                    table.HasCheckConstraint("chk_venues_positive_capacity", "capacity > 0");
                    table.HasCheckConstraint("chk_venues_required_photo", "LEN(LTRIM(RTRIM(image_path))) > 0");
                });

                entity.Property(e => e.VenueId).ValueGeneratedOnAdd().HasColumnName("venue_id");
                entity.Property(e => e.RoomName).IsRequired().HasMaxLength(150).HasColumnName("room_name");
                entity.Property(e => e.Capacity).HasColumnName("capacity");
                entity.Property(e => e.Location).HasMaxLength(255).HasColumnName("location");
                entity.Property(e => e.AvailabilityNotes).HasColumnType("nvarchar(max)").HasColumnName("availability_notes");
                entity.Property(e => e.ContactPerson).HasMaxLength(150).HasColumnName("contact_person");
                entity.Property(e => e.IsActive).IsRequired().HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.ImagePath).IsRequired().HasMaxLength(500).HasColumnName("image_path");
                entity.Property(e => e.CreatedBy).IsRequired().HasColumnName("created_by");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("datetime2(3)").HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(3)").HasColumnName("updated_at");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
