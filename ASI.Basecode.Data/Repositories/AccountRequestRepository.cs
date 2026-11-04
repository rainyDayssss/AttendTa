using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public class AccountRequestRepository : BaseRepository, IAccountRequestRepository
    {
        public AccountRequestRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

        public bool HasPendingIdentity(string email, string studentNumber) =>
            GetDbSet<AccountRequest>().Any(r => r.Status == "pending" && (r.Email == email || r.StudentNumber == studentNumber));

        public void Add(AccountRequest request)
        {
            GetDbSet<AccountRequest>().Add(request);
            UnitOfWork.SaveChanges();
        }

        public IReadOnlyList<AccountRequest> GetPending(string search, int page, int pageSize, out int totalCount)
        {
            var query = GetDbSet<AccountRequest>().AsNoTracking().Where(r => r.Status == "pending");
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(r => r.FullName.Contains(term) || r.Email.Contains(term) || r.StudentNumber.Contains(term));
            }
            totalCount = query.Count();
            return query.OrderBy(r => r.RequestedAt).ThenBy(r => r.RequestId)
                .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        public AccountRequest GetPending(int requestId) =>
            GetDbSet<AccountRequest>().AsNoTracking().SingleOrDefault(r => r.RequestId == requestId && r.Status == "pending");

        public int TryFinalize(int requestId, string status, int reviewerId, string reviewNotes, int? createdUserId)
        {
            return GetDbSet<AccountRequest>().Where(r => r.RequestId == requestId && r.Status == "pending")
                .ExecuteUpdate(setters => setters
                    .SetProperty(r => r.Status, status)
                    .SetProperty(r => r.ReviewedBy, reviewerId)
                    .SetProperty(r => r.ReviewedAt, DateTime.UtcNow)
                    .SetProperty(r => r.ReviewNotes, reviewNotes)
                    .SetProperty(r => r.CreatedUserId, createdUserId)
                    .SetProperty(r => r.PasswordHash, (string)null));
        }

        public void WriteAudit(string actorType, int? actorUserId, string action, int requestId, string reason = null)
        {
            UnitOfWork.Database.Database.ExecuteSqlInterpolated($"INSERT INTO dbo.activity_logs (actor_type, actor_user_id, action, entity_type, entity_id, description, reason, occurred_at) VALUES ({actorType}, {actorUserId}, {action}, 'account_request', {requestId}, {action}, {reason}, SYSUTCDATETIME())");
        }
    }
}
