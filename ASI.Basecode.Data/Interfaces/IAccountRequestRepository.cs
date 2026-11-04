using ASI.Basecode.Data.Models;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IAccountRequestRepository
    {
        bool HasPendingIdentity(string email, string studentNumber);
        void Add(AccountRequest request);
        int CountPending(string search);
        IReadOnlyList<AccountRequest> GetPending(string search, int page, int pageSize);
        AccountRequest GetPending(int requestId);
        int TryFinalize(int requestId, string status, int reviewerId, string reviewNotes, int? createdUserId);
        void WriteAudit(string actorType, int? actorUserId, string action, int requestId, string reason = null);
    }
}
