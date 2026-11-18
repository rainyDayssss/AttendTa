using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IMemberService
    {
        Task<PagedResult<MemberViewModel>> GetPageAsync(string search, string status, int? pageNumber, CancellationToken cancellationToken = default);

        /// <summary>Reads one member's editor model, or <c>null</c> when the member does not exist.</summary>
        Task<MemberViewModel> GetMemberAsync(int id, CancellationToken cancellationToken = default);
        Task<MemberViewModel> GetMemberEditorAsync(int id, CancellationToken cancellationToken = default);
        Task<MemberViewModel> GetSelfProfileAsync(CancellationToken cancellationToken = default);
        Task<bool> HasMemberProfileAsync(int userId, CancellationToken cancellationToken = default);
        /// <returns>True if the profile was changed; false if it was already equivalent.</returns>
        Task<bool> UpdateMemberAsync(MemberViewModel model, CancellationToken cancellationToken = default, MemberEditSnapshot original = null);
        Task CreateMemberFromApprovedAccountRequestAsync(int userId, MemberViewModel model, CancellationToken cancellationToken = default);
    }
}
