using ASI.Basecode.Data.Models;
using System.Linq;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IMemberRepository
    {
        IQueryable<Member> GetMembers();
        Member GetMember(int memberId);
        void AddMember(Member member);
        void UpdateMember(Member member);
        bool MemberExists(int memberId);
        bool StudentNumberExists(string studentNumber, int? excludeId = null);
    }
}
