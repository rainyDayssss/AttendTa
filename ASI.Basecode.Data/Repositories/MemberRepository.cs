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
        public MemberRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
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
                       Email = user.UserId,
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

        public MemberRecord GetMemberRecord(int memberId)
        {
            return this.GetMemberRecords().FirstOrDefault(member => member.MemberId == memberId);
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

        public Member GetMember(int memberId)
        {
            return this.GetDbSet<Member>().FirstOrDefault(m => m.MemberId == memberId);
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

        public bool UserHasMemberProfile(int userId)
        {
            return this.GetDbSet<Member>().Any(m => m.UserId == userId);
        }

        public bool StudentNumberExists(string studentNumber, int? excludeId = null)
        {
            return this.GetDbSet<Member>().Any(m => m.StudentNumber == studentNumber &&
                                                     (!excludeId.HasValue || m.MemberId != excludeId.Value));
        }
    }
}
