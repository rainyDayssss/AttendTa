using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class MemberRepository : BaseRepository, IMemberRepository
    {
        public MemberRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<Member> GetMembers()
        {
            return this.GetDbSet<Member>();
        }

        public Member GetMember(int memberId)
        {
            return this.GetDbSet<Member>().FirstOrDefault(m => m.MemberId == memberId);
        }

        public void AddMember(Member member)
        {
            this.GetDbSet<Member>().Add(member);
            UnitOfWork.SaveChanges();
        }

        public void UpdateMember(Member member)
        {
            this.SetEntityState(member, EntityState.Modified);
            UnitOfWork.SaveChanges();
        }

        public bool MemberExists(int memberId)
        {
            return this.GetDbSet<Member>().Any(m => m.MemberId == memberId);
        }

        public bool StudentNumberExists(string studentNumber, int? excludeId = null)
        {
            return this.GetDbSet<Member>().Any(m => m.StudentNumber == studentNumber &&
                                                     (!excludeId.HasValue || m.MemberId != excludeId.Value));
        }
    }
}
