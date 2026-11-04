using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class AccountRequestService : IAccountRequestService
    {
        private const int DefaultPageSize = 25;
        private readonly IAccountRequestRepository _requests;
        private readonly IUserRepository _users;
        private readonly IMemberRepository _membersRepository;
        private readonly IMemberService _members;
        private readonly IUnitOfWork _unitOfWork;

        public AccountRequestService(IAccountRequestRepository requests, IUserRepository users,
            IMemberRepository membersRepository, IMemberService members, IUnitOfWork unitOfWork)
        {
            _requests = requests; _users = users; _membersRepository = membersRepository;
            _members = members; _unitOfWork = unitOfWork;
        }

        public void Submit(AccountRequestViewModel model)
        {
            var email = model.Email.Trim().ToLowerInvariant();
            var studentNumber = model.StudentNumber.Trim();
            if (_users.UserExists(email) || _membersRepository.StudentNumberExists(studentNumber) ||
                _requests.HasPendingIdentity(email, studentNumber))
                throw new InvalidDataException("An account request with those details cannot be submitted.");

            using var tx = _unitOfWork.Database.Database.BeginTransaction();
            var request = new AccountRequest
            {
                StudentNumber = studentNumber, FullName = model.FullName.Trim(), Email = email,
                PasswordHash = PasswordManager.HashPassword(model.Password), Course = model.Course.Trim(),
                YearLevel = model.YearLevel, Section = model.Section.Trim(), MobileNumber = model.MobileNumber?.Trim(),
                Status = "pending", RequestedAt = DateTime.UtcNow
            };
            try
            {
                _requests.Add(request);
                _requests.WriteAudit("system", null, "account_request_submitted", request.RequestId);
                tx.Commit();
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex))
            {
                tx.Rollback();
                throw new InvalidDataException("An account request with those details cannot be submitted.");
            }
        }

        public AccountRequestListViewModel GetPending(int page, string search = null)
        {
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var countQueryPage = Math.Max(1, page);
            var items = _requests.GetPending(normalizedSearch, countQueryPage, DefaultPageSize, out var totalCount);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)DefaultPageSize));
            var effectivePage = Math.Min(countQueryPage, totalPages);
            if (effectivePage != countQueryPage)
                items = _requests.GetPending(normalizedSearch, effectivePage, DefaultPageSize, out totalCount);
            return new AccountRequestListViewModel
            {
                Items = items.Select(r => new AccountRequestListItemViewModel
                {
                    RequestId = r.RequestId, StudentNumber = r.StudentNumber, FullName = r.FullName,
                    Email = r.Email, Course = r.Course, YearLevel = r.YearLevel, Section = r.Section,
                    MobileNumber = r.MobileNumber, RequestedAt = r.RequestedAt
                }).ToList(),
                Page = effectivePage, PageSize = DefaultPageSize, TotalCount = totalCount,
                TotalPages = totalPages, Search = normalizedSearch
            };
        }

        public void Approve(int requestId, int actorUserId)
        {
            if (actorUserId <= 0) throw new InvalidDataException("A valid reviewer is required.");
            using var tx = _unitOfWork.Database.Database.BeginTransaction();
            var request = _requests.GetPending(requestId) ?? throw new InvalidDataException("This request has already been reviewed.");
            if (_users.UserExists(request.Email) || _membersRepository.StudentNumberExists(request.StudentNumber))
                throw new InvalidDataException("The requested identity is already in use.");

            var user = new User
            {
                FullName = request.FullName, Email = request.Email, PasswordHash = request.PasswordHash,
                Role = "member", IsActive = true, CreatedBy = actorUserId, CreatedAt = DateTime.UtcNow
            };
            try
            {
                _users.AddUser(user);
                if (_requests.TryFinalize(requestId, "approved", actorUserId, null, user.UserId) != 1)
                    throw new InvalidDataException("This request has already been reviewed.");
                _members.CreateMemberFromApprovedAccountRequest(user.UserId, new MemberViewModel
                {
                    StudentNumber = request.StudentNumber, Course = request.Course, YearLevel = request.YearLevel,
                    Section = request.Section, MobileNumber = request.MobileNumber, Status = "active"
                }, actorUserId);
                _requests.WriteAudit("user", actorUserId, "account_request_approved", requestId);
                tx.Commit();
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex))
            {
                tx.Rollback();
                throw new InvalidDataException("This request has already been reviewed or its identity is in use.");
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void Reject(int requestId, int actorUserId, string reason)
        {
            if (actorUserId <= 0 || string.IsNullOrWhiteSpace(reason)) throw new InvalidDataException("A valid reviewer and rejection reason are required.");
            using var tx = _unitOfWork.Database.Database.BeginTransaction();
            try
            {
                if (_requests.TryFinalize(requestId, "rejected", actorUserId, reason.Trim(), null) != 1)
                    throw new InvalidDataException("This request has already been reviewed.");
                _requests.WriteAudit("user", actorUserId, "account_request_rejected", requestId, reason.Trim());
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        private static bool IsUniqueConflict(DbUpdateException exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
                if (current is SqlException sql && (sql.Number == 2601 || sql.Number == 2627)) return true;
            return false;
        }
    }
}
