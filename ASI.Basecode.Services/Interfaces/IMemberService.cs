using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IMemberService
    {
        Task<PagedResult<MemberViewModel>> GetPageAsync(string search, string status, int? pageNumber, CancellationToken cancellationToken = default);
        MemberViewModel GetMember(int id);
        Task<MemberViewModel> GetSelfProfileAsync(CancellationToken cancellationToken = default);
        Task UpdateMemberAsync(MemberViewModel model, CancellationToken cancellationToken = default);
        Task CreateMemberFromApprovedAccountRequestAsync(int userId, MemberViewModel model, CancellationToken cancellationToken = default);
    }
}
