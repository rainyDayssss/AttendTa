using ASI.Basecode.Data.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IMemberRepository
    {
        IQueryable<MemberRecord> GetMemberRecords();
        Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default);
        MemberRecord GetMemberRecord(int memberId);
        Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Member GetMember(int memberId);
        Task AddMemberAsync(Member member, CancellationToken cancellationToken = default);
        Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default);
        bool UserHasMemberProfile(int userId);
        bool StudentNumberExists(string studentNumber, int? excludeId = null);
    }
}
