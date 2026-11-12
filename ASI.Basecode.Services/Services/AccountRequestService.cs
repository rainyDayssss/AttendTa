using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Validates and stores anonymous membership requests. Only the Argon2id hash is persisted;
    /// the plaintext password and its confirmation never leave this call.
    /// </summary>
    public class AccountRequestService : IAccountRequestService
    {
        private readonly IAccountRequestRepository _accountRequestRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMemberRepository _memberRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly TimeProvider _timeProvider;
        private readonly ICurrentActor _currentActor;
        private readonly IMemberService _memberService;
        private readonly IActivityLogWriter _activityLogWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountRequestService"/> class.
        /// </summary>
        /// <param name="accountRequestRepository">The account request repository.</param>
        /// <param name="userRepository">The user repository (email uniqueness, including inactive accounts).</param>
        /// <param name="memberRepository">The member repository (student-number uniqueness, including inactive members).</param>
        /// <param name="unitOfWork">The unit of work.</param>
        /// <param name="passwordHasher">The Argon2id password hasher.</param>
        /// <param name="timeProvider">The clock used for <c>requested_at</c>.</param>
        /// <param name="currentActor">The authenticated actor for decisions and audit.</param>
        /// <param name="memberService">The owning service that creates linked member profiles.</param>
        /// <param name="activityLogWriter">The transactional audit writer.</param>
        public AccountRequestService(
            IAccountRequestRepository accountRequestRepository,
            IUserRepository userRepository,
            IMemberRepository memberRepository,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            TimeProvider timeProvider,
            ICurrentActor currentActor,
            IMemberService memberService,
            IActivityLogWriter activityLogWriter)
        {
            _accountRequestRepository = accountRequestRepository ?? throw new ArgumentNullException(nameof(accountRequestRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _currentActor = currentActor ?? throw new ArgumentNullException(nameof(currentActor));
            _memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));
            _activityLogWriter = activityLogWriter ?? throw new ArgumentNullException(nameof(activityLogWriter));
        }

        /// <inheritdoc />
        public async Task SubmitAsync(AccountRequestViewModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);

            Normalize(model);
            Validate(model);

            var email = model.Email;
            var studentNumber = model.StudentNumber;

            // Reuse the login lookup: it returns inactive accounts too, so a deactivated account
            // still owns its email address.
            if (await _userRepository.GetByEmailAsync(email, cancellationToken) is not null)
            {
                throw new DomainException(OutcomeCode.Duplicate, "This email is already in use.", nameof(AccountRequestViewModel.Email));
            }

            // StudentNumberExists includes inactive member profiles.
            if (await _memberRepository.StudentNumberExistsAsync(studentNumber, cancellationToken: cancellationToken))
            {
                throw new DomainException(OutcomeCode.Duplicate, "This student number is already in use.", nameof(AccountRequestViewModel.StudentNumber));
            }

            if (await _accountRequestRepository.PendingEmailExistsAsync(email, cancellationToken))
            {
                throw new DomainException(OutcomeCode.Duplicate, "An account request with this email is already pending.", nameof(AccountRequestViewModel.Email));
            }

            if (await _accountRequestRepository.PendingStudentNumberExistsAsync(studentNumber, cancellationToken))
            {
                throw new DomainException(OutcomeCode.Duplicate, "An account request with this student number is already pending.", nameof(AccountRequestViewModel.StudentNumber));
            }

            var request = new AccountRequest
            {
                StudentNumber = studentNumber,
                FullName = model.FullName,
                Email = email,
                PasswordHash = _passwordHasher.Hash(model.Password),
                Course = model.Course,
                YearLevel = model.YearLevel.Value,
                Section = model.Section,
                MobileNumber = model.MobileNumber,
                AccountRequestStatusId = AccountRequestStatusCode.Pending,
                RequestedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            // One insert, no explicit transaction. The pending-only filtered unique indexes are
            // the final guard; the unit of work translates a race loser's SQL 2601/2627 into
            // DomainException(Duplicate) without a field.
            await _accountRequestRepository.AddAsync(request, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task ApproveAsync(int requestId, int actorUserId, CancellationToken cancellationToken = default)
        {
            var reviewerId = RequireManagementActor(actorUserId);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var request = await GetPendingRequestAsync(requestId, ct);
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                var existingUser = await _userRepository.GetByEmailAsync(request.Email, ct);
                if (existingUser is not null)
                {
                    // Keep the lock order account_requests -> users and refresh under the lock.
                    var lockedUser = await _userRepository.LockUserAsync(existingUser.Id, ct);
                    if (lockedUser is not null && string.Equals(lockedUser.UserId, request.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new DomainException(OutcomeCode.Duplicate, "This email is already in use.");
                    }
                }

                if (await _memberRepository.StudentNumberExistsAsync(request.StudentNumber, cancellationToken: ct))
                {
                    throw new DomainException(OutcomeCode.Duplicate, "This student number is already in use.");
                }

                if (string.IsNullOrWhiteSpace(request.PasswordHash))
                {
                    throw new DomainException(OutcomeCode.Stale, "This account request can no longer be approved.");
                }

                var user = new User
                {
                    UserId = request.Email,
                    Name = request.FullName,
                    Password = request.PasswordHash,
                    RoleId = RoleCode.Member,
                    IsActive = true,
                    CreatedById = reviewerId,
                    CreatedTime = now
                };

                await _userRepository.AddAsync(user, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                await _memberService.CreateMemberFromApprovedAccountRequestAsync(user.Id, new MemberViewModel
                {
                    FullName = request.FullName,
                    StudentNumber = request.StudentNumber,
                    Course = request.Course,
                    YearLevel = request.YearLevel,
                    Section = request.Section,
                    MobileNumber = request.MobileNumber
                }, ct);

                request.AccountRequestStatusId = AccountRequestStatusCode.Approved;
                request.ReviewedBy = reviewerId;
                request.ReviewedAt = now;
                request.ReviewNotes = null;
                request.CreatedUserId = user.Id;
                request.PasswordHash = null;

                _activityLogWriter.Append(new AuditEntry(
                    AuditActions.AccountRequestApproved,
                    "account_requests",
                    request.RequestId,
                    OldValues: new RequestDecisionValues("pending"),
                    NewValues: new RequestDecisionValues("approved")));

                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task RejectAsync(int requestId, string reason, int actorUserId, CancellationToken cancellationToken = default)
        {
            var reviewerId = RequireManagementActor(actorUserId);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var normalizedReason = reason?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedReason))
                {
                    throw new DomainException(OutcomeCode.Invalid, "A rejection reason is required.");
                }

                var request = await GetPendingRequestAsync(requestId, ct);
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                request.AccountRequestStatusId = AccountRequestStatusCode.Rejected;
                request.ReviewedBy = reviewerId;
                request.ReviewedAt = now;
                request.ReviewNotes = normalizedReason;
                request.PasswordHash = null;

                _activityLogWriter.Append(new AuditEntry(
                    AuditActions.AccountRequestRejected,
                    "account_requests",
                    request.RequestId,
                    Reason: normalizedReason,
                    OldValues: new RequestDecisionValues("pending"),
                    NewValues: new RequestDecisionValues("rejected")));

                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        private int RequireManagementActor(int actorUserId)
        {
            if (!_currentActor.IsAuthenticated)
            {
                throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
            }

            int currentActorId;
            try
            {
                currentActorId = _currentActor.UserId;
            }
            catch (InvalidOperationException)
            {
                throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
            }

            if (currentActorId <= 0 || actorUserId <= 0 || currentActorId != actorUserId
                || (!string.Equals(_currentActor.Role, RoleCodes.Admin, StringComparison.Ordinal)
                    && !string.Equals(_currentActor.Role, RoleCodes.Officer, StringComparison.Ordinal)))
            {
                throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
            }

            return currentActorId;
        }

        private async Task<AccountRequest> GetPendingRequestAsync(int requestId, CancellationToken cancellationToken)
        {
            var request = requestId > 0
                ? await _accountRequestRepository.LockAccountRequestAsync(requestId, cancellationToken)
                : null;

            if (request is null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Account request not found.");
            }

            if (request.AccountRequestStatusId != AccountRequestStatusCode.Pending)
            {
                throw new DomainException(OutcomeCode.Stale, "This account request has already been decided.");
            }

            return request;
        }

        private sealed record RequestDecisionValues(string Status) : IAuditValues;

        private static void Normalize(AccountRequestViewModel model)
        {
            model.StudentNumber = model.StudentNumber?.Trim();
            model.FullName = model.FullName?.Trim();
            model.Email = model.Email?.Trim();
            model.Course = model.Course?.Trim();
            model.Section = model.Section?.Trim();
            model.MobileNumber = string.IsNullOrWhiteSpace(model.MobileNumber) ? null : model.MobileNumber.Trim();
        }

        private static void Validate(AccountRequestViewModel model)
        {
            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
            {
                return;
            }

            var first = results[0];
            var field = first.MemberNames.FirstOrDefault();
            throw new DomainException(OutcomeCode.Invalid, first.ErrorMessage ?? "The request contains invalid details.", field);
        }
    }
}
