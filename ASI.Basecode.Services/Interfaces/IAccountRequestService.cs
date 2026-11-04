using ASI.Basecode.Services.ServiceModels;
namespace ASI.Basecode.Services.Interfaces
{
    public interface IAccountRequestService
    {
        void Submit(AccountRequestViewModel model);
        AccountRequestListViewModel GetPending(int page, string search = null);
        void Approve(int requestId, int actorUserId);
        void Reject(int requestId, int actorUserId, string reason);
    }
}
