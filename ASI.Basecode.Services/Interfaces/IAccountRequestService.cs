using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;
namespace ASI.Basecode.Services.Interfaces
{
    public interface IAccountRequestService
    {
        void Submit(AccountRequestViewModel model);
        (IReadOnlyList<AccountRequest> Items, int TotalPages) GetPending(int page);
        void Approve(int requestId, int actorUserId);
        void Reject(int requestId, int actorUserId, string reason);
    }
}
