using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class MemberService : IMemberService
    {
        private readonly IMemberRepository _repository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ICurrentActor _currentActor;
        private readonly IActivityLogWriter _audit;

        public MemberService(
            IMemberRepository repository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ICurrentActor currentActor,
            IActivityLogWriter audit)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _currentActor = currentActor ?? throw new ArgumentNullException(nameof(currentActor));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public async Task<PagedResult<MemberViewModel>> GetPageAsync(string search, string status, int? pageNumber, CancellationToken cancellationToken = default)
        {
            var totalCount = await _repository.CountAsync(search, status, cancellationToken);
            var request = new PageRequest(pageNumber);
            var page = request.NormalizePageNumber(totalCount);
            var records = await _repository.GetPageAsync(search, status, request.GetOffset(page), PageRequest.FixedPageSize, cancellationToken);
            return new PagedResult<MemberViewModel>(records.Select(ToViewModel).ToList(), page, totalCount);
        }

        public async Task<MemberViewModel> GetMemberAsync(int id, CancellationToken cancellationToken = default)
        {
            var member = await _repository.GetMemberRecordAsync(id, cancellationToken);
            return member == null ? null : ToViewModel(member);
        }

        public async Task<MemberViewModel> GetSelfProfileAsync(CancellationToken cancellationToken = default)
        {
            if (!_currentActor.IsAuthenticated || !string.Equals(_currentActor.Role, RoleCodes.Member, StringComparison.Ordinal))
            {
                throw new DomainException(OutcomeCode.Forbidden, "Access denied.");
            }

            var member = await _repository.GetMemberRecordByUserIdAsync(_currentActor.UserId, cancellationToken);
            if (member == null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Member profile not found.");
            }

            return ToViewModel(member);
        }

        public async Task CreateMemberFromApprovedAccountRequestAsync(int userId, MemberViewModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            var verifiedActorId = RequireManagementActor();
            if (userId <= 0)
            {
                throw new DomainException(OutcomeCode.Invalid, "A valid linked user is required to create a member.");
            }

            Normalize(model);
            Validate(model);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var user = await _userRepository.LockUserAsync(userId, ct);
                if (user == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "The linked account was not found.");
                }

                if (await _repository.UserHasMemberProfileAsync(userId, ct))
                {
                    throw new DomainException(OutcomeCode.Duplicate, "This user already has a member profile.");
                }

                if (await _repository.StudentNumberExistsAsync(model.StudentNumber, cancellationToken: ct))
                {
                    throw new DomainException(OutcomeCode.Duplicate, "Student number already exists.", nameof(MemberViewModel.StudentNumber));
                }

                var member = new Member
                {
                    UserId = userId,
                    StudentNumber = model.StudentNumber,
                    Course = model.Course,
                    YearLevel = model.YearLevel,
                    Section = model.Section,
                    MobileNumber = model.MobileNumber,
                    MembershipType = model.MembershipType,
                    MembershipStatusId = MembershipStatusCode.Active,
                    CreatedBy = verifiedActorId,
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                };

                await _repository.AddMemberAsync(member, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                _audit.Append(new AuditEntry(
                    AuditActions.MemberCreated,
                    "members",
                    member.MemberId,
                    NewValues: Snapshot(member, user.Name)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public async Task UpdateMemberAsync(MemberViewModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            var verifiedActorId = RequireManagementActor();
            Normalize(model);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var memberRecord = await _repository.GetMemberRecordAsync(model.MemberId, ct);
                if (memberRecord == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "Member not found.");
                }

                var user = await _userRepository.LockUserAsync(memberRecord.UserId, ct);
                if (user == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "The linked account was not found.");
                }

                var member = await _repository.GetMemberAsync(model.MemberId, ct);
                if (member == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "Member not found.");
                }

                Validate(model);
                var correctingStudentNumber = !string.Equals(member.StudentNumber, model.StudentNumber, StringComparison.Ordinal);
                var reason = string.IsNullOrWhiteSpace(model.StudentNumberCorrectionReason)
                    ? null
                    : model.StudentNumberCorrectionReason.Trim();

                if (correctingStudentNumber && reason == null)
                {
                    throw new DomainException(OutcomeCode.Invalid, "A reason is required when correcting the student number.", nameof(MemberViewModel.StudentNumberCorrectionReason));
                }

                if (correctingStudentNumber && await _repository.StudentNumberExistsAsync(model.StudentNumber, model.MemberId, ct))
                {
                    throw new DomainException(OutcomeCode.Duplicate, "Student number already exists.", nameof(MemberViewModel.StudentNumber));
                }

                var oldValues = Snapshot(member, user.Name);
                user.Name = model.FullName;
                user.UpdatedById = verifiedActorId;
                user.UpdatedTime = _timeProvider.GetUtcNow().UtcDateTime;
                member.StudentNumber = model.StudentNumber;
                member.Course = model.Course;
                member.YearLevel = model.YearLevel;
                member.Section = model.Section;
                member.MobileNumber = model.MobileNumber;
                member.MembershipType = model.MembershipType;
                member.UpdatedBy = verifiedActorId;
                member.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _repository.UpdateMemberAsync(member, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                _audit.Append(new AuditEntry(
                    correctingStudentNumber ? AuditActions.MemberStudentNumberCorrected : AuditActions.MemberEdited,
                    "members",
                    member.MemberId,
                    Reason: correctingStudentNumber ? reason : null,
                    OldValues: oldValues,
                    NewValues: Snapshot(member, user.Name)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        private int RequireManagementActor()
        {
            if (!_currentActor.IsAuthenticated)
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated user is required to change a member.");
            }

            int actorId;
            try
            {
                actorId = _currentActor.UserId;
            }
            catch (InvalidOperationException)
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated user is required to change a member.");
            }

            if (actorId <= 0
                || (!string.Equals(_currentActor.Role, RoleCodes.Admin, StringComparison.Ordinal)
                    && !string.Equals(_currentActor.Role, RoleCodes.Officer, StringComparison.Ordinal)))
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated management user is required to change a member.");
            }

            return actorId;
        }

        private static void Normalize(MemberViewModel model)
        {
            model.FullName = model.FullName?.Trim();
            model.StudentNumber = model.StudentNumber?.Trim();
            model.Course = model.Course?.Trim();
            model.Section = model.Section?.Trim();
            model.MobileNumber = string.IsNullOrWhiteSpace(model.MobileNumber) ? null : model.MobileNumber.Trim();
            model.MembershipType = string.IsNullOrWhiteSpace(model.MembershipType) ? null : model.MembershipType.Trim();
            model.StudentNumberCorrectionReason = string.IsNullOrWhiteSpace(model.StudentNumberCorrectionReason)
                ? null
                : model.StudentNumberCorrectionReason.Trim();
        }

        private static void Validate(MemberViewModel model)
        {
            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
            {
                return;
            }

            var first = results[0];
            var field = first.MemberNames.FirstOrDefault();
            throw new DomainException(OutcomeCode.Invalid, first.ErrorMessage ?? "The member profile contains invalid details.", field);
        }

        private static MemberAuditValues Snapshot(Member member, string fullName) => new(
            fullName,
            member.StudentNumber,
            member.Course,
            member.YearLevel,
            member.Section,
            member.MobileNumber,
            member.MembershipType,
            member.Status);

        private sealed record MemberAuditValues(
            string FullName,
            string StudentNumber,
            string Course,
            int YearLevel,
            string Section,
            string MobileNumber,
            string MembershipType,
            string Status) : IAuditValues;

        private static MemberViewModel ToViewModel(MemberRecord member)
        {
            return new MemberViewModel
            {
                MemberId = member.MemberId,
                UserId = member.UserId,
                FullName = member.FullName,
                Email = member.Email,
                StudentNumber = member.StudentNumber,
                Course = member.Course,
                YearLevel = member.YearLevel,
                Section = member.Section,
                MobileNumber = member.MobileNumber,
                MembershipType = member.MembershipType,
                Status = member.Status
            };
        }
    }
}
