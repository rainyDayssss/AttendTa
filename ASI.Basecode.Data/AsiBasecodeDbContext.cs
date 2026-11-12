using Microsoft.EntityFrameworkCore;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;

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
        public virtual DbSet<EventType> EventTypes { get; set; }
        public virtual DbSet<Venue> Venues { get; set; }
        public virtual DbSet<Event> Events { get; set; }
        public virtual DbSet<Registration> Registrations { get; set; }
        public virtual DbSet<Attendance> AttendanceRecords { get; set; }
        public virtual DbSet<Certificate> Certificates { get; set; }
        public virtual DbSet<ActivityLog> ActivityLogs { get; set; }
        public virtual DbSet<RoleLookup> Roles { get; set; }
        public virtual DbSet<AccountRequestStatusLookup> AccountRequestStatuses { get; set; }
        public virtual DbSet<MembershipStatusLookup> MembershipStatuses { get; set; }
        public virtual DbSet<EventStatusLookup> EventStatuses { get; set; }
        public virtual DbSet<RegistrationStatusLookup> RegistrationStatuses { get; set; }
        public virtual DbSet<CheckInMethodLookup> CheckInMethods { get; set; }
        public virtual DbSet<CertificateStatusLookup> CertificateStatuses { get; set; }
        public virtual DbSet<ActorTypeLookup> ActorTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
