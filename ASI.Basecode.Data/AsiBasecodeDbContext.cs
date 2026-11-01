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
        public virtual DbSet<Member> Members { get; set; }
        public virtual DbSet<EventType> EventTypes { get; set; }
        public virtual DbSet<Venue> Venues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.UserId, "UQ__Users__1788CC4D5F4A160F")
                    .IsUnique();

                entity.Property(e => e.CreatedBy)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.CreatedTime).HasColumnType("datetime");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Password)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.UpdatedBy)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.UpdatedTime).HasColumnType("datetime");

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Member>(entity =>
            {
                entity.HasKey(e => e.MemberId);

                entity.ToTable("members");

                entity.HasIndex(e => e.StudentNumber, "UQ__members__0E749A79D636F2B5").IsUnique();

                entity.Property(e => e.MemberId).ValueGeneratedOnAdd().HasColumnName("member_id");
                entity.Property(e => e.ContactDetails).HasMaxLength(255).HasColumnName("contact_details");
                entity.Property(e => e.CourseYear).IsRequired().HasMaxLength(255).HasColumnName("course_year");
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(255).HasColumnName("full_name");
                entity.Property(e => e.MembershipType).HasMaxLength(255).HasColumnName("membership_type");
                entity.Property(e => e.Section).IsRequired().HasMaxLength(255).HasColumnName("section");
                entity.Property(e => e.Status).HasMaxLength(255).HasColumnName("status");
                entity.Property(e => e.StudentNumber).IsRequired().HasMaxLength(255).HasColumnName("student_number");
                entity.Property(e => e.UserId).HasColumnName("user_id");
            });

            modelBuilder.Entity<EventType>(entity =>
            {
                entity.HasKey(e => e.TypeId);

                entity.ToTable("event_types");

                entity.HasIndex(e => e.TypeName, "UQ__event_ty__543C4FD9CB67C4B3").IsUnique();

                entity.Property(e => e.TypeId).ValueGeneratedOnAdd().HasColumnName("type_id");
                entity.Property(e => e.Description).HasColumnType("text").HasColumnName("description");
                entity.Property(e => e.TypeName).IsRequired().HasMaxLength(255).HasColumnName("type_name");
            });

            modelBuilder.Entity<Venue>(entity =>
            {
                entity.HasKey(e => e.VenueId);

                entity.ToTable("venues");

                entity.Property(e => e.VenueId).ValueGeneratedOnAdd().HasColumnName("venue_id");
                entity.Property(e => e.AvailabilityNotes).HasColumnType("text").HasColumnName("availability_notes");
                entity.Property(e => e.Capacity).HasColumnName("capacity");
                entity.Property(e => e.ContactPerson).HasMaxLength(255).HasColumnName("contact_person");
                entity.Property(e => e.Location).HasMaxLength(255).HasColumnName("location");
                entity.Property(e => e.RoomName).IsRequired().HasMaxLength(255).HasColumnName("room_name");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
