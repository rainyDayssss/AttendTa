using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class AccountRequestService : IAccountRequestService
    {
        private readonly AsiBasecodeDBContext _db;
        private readonly IMemberService _members;
        public AccountRequestService(AsiBasecodeDBContext db, IMemberService members) { _db = db; _members = members; }

        public void Submit(AccountRequestViewModel model)
        {
            var email = model.Email.Trim().ToLowerInvariant();
            var studentNumber = model.StudentNumber.Trim();
            if (_db.Users.Any(u => u.Email == email) || _db.Members.Any(m => m.StudentNumber == studentNumber) ||
                _db.AccountRequests.Any(r => r.Status == "pending" && (r.Email == email || r.StudentNumber == studentNumber)))
                throw new InvalidDataException("Email or student number is already in use or has a pending request.");
            using var tx = _db.Database.BeginTransaction();
            var request = new AccountRequest
            {
                StudentNumber = studentNumber, FullName = model.FullName.Trim(), Email = email,
                PasswordHash = PasswordManager.HashPassword(model.Password), Course = model.Course.Trim(),
                YearLevel = model.YearLevel, Section = model.Section.Trim(), MobileNumber = model.MobileNumber?.Trim(),
                Status = "pending", RequestedAt = DateTime.UtcNow
            };
            _db.AccountRequests.Add(request);
            _db.SaveChanges();
            _db.Database.ExecuteSqlInterpolated($"INSERT INTO dbo.activity_logs (actor_type, actor_user_id, action, entity_type, entity_id, description, occurred_at) VALUES ('system', NULL, 'account_request_submitted', 'account_request', {request.RequestId}, N'Account request submitted.', SYSUTCDATETIME())");
            tx.Commit();
        }

        public (IReadOnlyList<AccountRequest> Items, int TotalPages) GetPending(int page)
        {
            page = Math.Max(1, page);
            var query = _db.AccountRequests.AsNoTracking().Where(r => r.Status == "pending").OrderBy(r => r.RequestedAt).ThenBy(r => r.RequestId);
            var count = query.Count();
            var pages = Math.Max(1, (int)Math.Ceiling(count / 25d));
            page = Math.Min(page, pages);
            return (query.Skip((page - 1) * 25).Take(25).ToList(), pages);
        }

        public void Approve(int requestId, int actorUserId)
        {
            if (actorUserId <= 0) throw new InvalidDataException("A valid reviewer is required.");
            using var tx = _db.Database.BeginTransaction();
            var request = _db.AccountRequests.SingleOrDefault(r => r.RequestId == requestId && r.Status == "pending")
                ?? throw new InvalidDataException("Pending request not found.");
            if (_db.Users.Any(u => u.Email == request.Email) || _db.Members.Any(m => m.StudentNumber == request.StudentNumber))
                throw new InvalidDataException("The requested identity is already in use.");
            var user = new User { FullName = request.FullName, Email = request.Email, PasswordHash = request.PasswordHash, Role = "member", IsActive = true, CreatedBy = actorUserId, CreatedAt = DateTime.UtcNow };
            _db.Users.Add(user);
            _db.SaveChanges();
            _members.CreateMemberFromApprovedAccountRequest(user.UserId, new MemberViewModel
            {
                StudentNumber = request.StudentNumber, Course = request.Course, YearLevel = request.YearLevel,
                Section = request.Section, MobileNumber = request.MobileNumber, Status = "active"
            }, actorUserId);
            request.CreatedUserId = user.UserId; request.ReviewedBy = actorUserId; request.ReviewedAt = DateTime.UtcNow;
            request.Status = "approved"; request.PasswordHash = null;
            _db.SaveChanges();
            _db.Database.ExecuteSqlInterpolated($"INSERT INTO dbo.activity_logs (actor_type, actor_user_id, action, entity_type, entity_id, description, occurred_at) VALUES ('user', {actorUserId}, 'account_request_approved', 'account_request', {requestId}, N'Account request approved.', SYSUTCDATETIME())");
            tx.Commit();
        }

        public void Reject(int requestId, int actorUserId, string reason)
        {
            if (actorUserId <= 0 || string.IsNullOrWhiteSpace(reason)) throw new InvalidDataException("A valid reviewer and rejection reason are required.");
            using var tx = _db.Database.BeginTransaction();
            var request = _db.AccountRequests.SingleOrDefault(r => r.RequestId == requestId && r.Status == "pending")
                ?? throw new InvalidDataException("Pending request not found.");
            request.ReviewedBy = actorUserId; request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNotes = reason.Trim(); request.Status = "rejected"; request.PasswordHash = null;
            _db.SaveChanges();
            _db.Database.ExecuteSqlInterpolated($"INSERT INTO dbo.activity_logs (actor_type, actor_user_id, action, entity_type, entity_id, description, reason, occurred_at) VALUES ('user', {actorUserId}, 'account_request_rejected', 'account_request', {requestId}, N'Account request rejected.', {request.ReviewNotes}, SYSUTCDATETIME())");
            tx.Commit();
        }
    }
}
