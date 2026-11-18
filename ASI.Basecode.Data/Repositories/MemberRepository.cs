using ASI.Basecode.Data.Queries;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Repositories
{
    public class MemberRepository : BaseRepository, IMemberRepository
    {
        private readonly ListReadOptions _readOptions;
        public MemberRepository(IUnitOfWork unitOfWork, ListReadOptions readOptions = null) : base(unitOfWork)
        {
            _readOptions = readOptions;
        }

        public IQueryable<MemberRecord> GetMemberRecords()
        {
            var members = this.GetDbSet<Member>().AsNoTracking();
            var users = this.GetDbSet<User>().AsNoTracking();

            return from member in members
                   join user in users on member.UserId equals user.Id
                   select new MemberRecord
                   {
                       MemberId = member.MemberId,
                       UserId = member.UserId,
                       FullName = user.Name,
                       Email = user.Email,
                       StudentNumber = member.StudentNumber,
                       Course = member.Course,
                       YearLevel = member.YearLevel,
                       Section = member.Section,
                       MobileNumber = member.MobileNumber,
                       MembershipType = member.MembershipType,
                       Status = member.MembershipStatusId == MembershipStatusCode.Active ? "active" : "inactive",
                       CreatedBy = member.CreatedBy,
                       CreatedAt = member.CreatedAt,
                       UpdatedBy = member.UpdatedBy,
                       UpdatedAt = member.UpdatedAt
                   };
        }

        public Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default)
        {
            return this.GetMemberRecords()
                .FirstOrDefaultAsync(member => member.MemberId == memberId, cancellationToken);
        }

        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) =>
            FilterMembers(search, status).CountAsync(cancellationToken);

        public async Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default)
        {
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
            if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take));

            return await FilterMembers(search, status)
                .OrderBy(member => member.FullName)
                .ThenBy(member => member.MemberId)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        private IQueryable<MemberRecord> FilterMembers(string search, string status)
        {
            var query = GetMemberRecords();
            if (!string.IsNullOrWhiteSpace(_readOptions?.Course)) {
                var course = _readOptions.Course;
                var legacy = course == "BSIT" ? "BS Information Technology" : course == "BSCS" ? "BS Computer Science" : course;
                query = query.Where(row => row.Course == course || row.Course == legacy);
            }
            if (_readOptions?.Year is int year) query = query.Where(row => row.YearLevel == year);
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(member => member.FullName.Contains(search) || member.Email.Contains(search) || member.StudentNumber.Contains(search));
            }

            return status switch
            {
                "active" => query.Where(member => member.Status == "active"),
                "inactive" => query.Where(member => member.Status == "inactive"),
                _ => query
            };
        }

        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return this.GetMemberRecords()
                .FirstOrDefaultAsync(member => member.UserId == userId, cancellationToken);
        }

        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return this.GetDbSet<Member>()
                .AsNoTracking()
                .SingleOrDefaultAsync(member => member.UserId == userId, cancellationToken);
        }

        public Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) =>
            this.GetDbSet<Member>().FirstOrDefaultAsync(member => member.MemberId == memberId, cancellationToken);

        public void EnsureMemberCanRefresh(int memberId)
        {
            var entry = Context.ChangeTracker.Entries<Member>().FirstOrDefault(item => item.Entity.MemberId == memberId);
            if (entry != null && entry.State != EntityState.Unchanged)
                throw new InvalidOperationException("Cannot refresh a member with unsaved tracked changes.");
        }

        public async Task<Member> GetMemberForEditAsync(int memberId, CancellationToken cancellationToken = default)
        {
            EnsureMemberCanRefresh(memberId);
            var entry = Context.ChangeTracker.Entries<Member>().FirstOrDefault(item => item.Entity.MemberId == memberId);
            if (entry == null) return await GetMemberAsync(memberId, cancellationToken);
            await entry.ReloadAsync(cancellationToken);
            return entry.State == EntityState.Detached ? null : entry.Entity;
        }

        public Task AddMemberAsync(Member member, CancellationToken cancellationToken = default)
        {
            this.GetDbSet<Member>().Add(member);
            return Task.CompletedTask;
        }

        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default)
        {
            if (Context.Entry(member).State == EntityState.Detached)
            {
                throw new System.InvalidOperationException("Only a tracked member can be updated.");
            }

            return Task.CompletedTask;
        }

        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default)
        {
            return this.GetDbSet<Member>().AnyAsync(member => member.UserId == userId, cancellationToken);
        }

        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            return this.GetDbSet<Member>().AnyAsync(
                member => member.StudentNumber == studentNumber
                          && (!excludeId.HasValue || member.MemberId != excludeId.Value),
                cancellationToken);
        }
    }
}
