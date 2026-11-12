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

        /// <summary>Reads one member's list record, or <c>null</c> when the member does not exist.</summary>
        Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default);

        Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>Reads the member row linked to a user, or <c>null</c> when the user has no profile.</summary>
        Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>Reads one member row by primary key, or <c>null</c> when it does not exist.</summary>
        Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default);

        Task AddMemberAsync(Member member, CancellationToken cancellationToken = default);
        Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default);

        /// <summary>Reports whether the user already has a member profile.</summary>
        Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reports whether any member, active or inactive, already uses the student number. The
        /// optional <paramref name="excludeId"/> skips the member being edited.
        /// </summary>
        Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default);
    }
}
