using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Published event browsing, registration submission and member self-cancellation. Registration
    /// and cancellation lock the owner's <c>users</c> row and then the event, recheck the canonical
    /// facts against the locked rows, and let the database uniqueness key remain the final duplicate
    /// guard (AD-4, AD-5, AD-6).
    /// </summary>
    public sealed class MemberEventService : IMemberEventService
    {
        private readonly IEventRepository _events;
        private readonly IRegistrationRepository _registrations;
        private readonly IMemberRepository _members;
        private readonly IUserRepository _users;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ICurrentActor _actor;
        private readonly IRegistrationCancellationService _cancellation;
        private readonly IMapper _mapper;

        public MemberEventService(
            IEventRepository events,
            IRegistrationRepository registrations,
            IMemberRepository members,
            IUserRepository users,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ICurrentActor actor,
            IRegistrationCancellationService cancellation,
            IMapper mapper)
        {
            _events = events;
            _registrations = registrations;
            _members = members;
            _users = users;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _actor = actor;
            _cancellation = cancellation;
            _mapper = mapper;
        }

        public async Task<PagedResult<MemberEventListItemViewModel>> GetFeedAsync(int? pageNumber, CancellationToken cancellationToken = default)
        {
            RequireAuthenticatedActor();

            var totalCount = await _events.CountPublishedAsync(cancellationToken);
            var pageRequest = new PageRequest(pageNumber);
            var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
            var rows = await _events.GetPublishedPageAsync(
                pageRequest.GetOffset(normalizedPage),
                PageRequest.FixedPageSize,
                cancellationToken);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var signupOpen = ParticipationFacts.SignupOpen(now).Compile();
            var items = new List<MemberEventListItemViewModel>(rows.Count);
            foreach (var row in rows)
            {
                var item = _mapper.Map<MemberEventListItemViewModel>(row);
                item.IsSignupOpen = SignupOpen(row.EventStatusId, row.RegistrationDeadline, signupOpen);
                items.Add(item);
            }

            return new PagedResult<MemberEventListItemViewModel>(items, normalizedPage, totalCount);
        }

        public async Task<MemberEventDetailViewModel> GetDetailAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireAuthenticatedActor();

            var row = await _events.GetPublishedEventAsync(eventId, cancellationToken);
            if (row == null)
            {
                return null;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var model = _mapper.Map<MemberEventDetailViewModel>(row);
            model.IsSignupOpen = SignupOpen(row.EventStatusId, row.RegistrationDeadline, ParticipationFacts.SignupOpen(now).Compile());
            model.IsFull = row.ConfirmedCount >= row.ParticipantLimit;
            model.IsStartCutoffPassed = StartCutoffPassed(row.StartsAt, now);

            var member = await _members.GetMemberByUserIdAsync(actorUserId, cancellationToken);
            var user = await _users.GetByIdAsync(actorUserId, cancellationToken);
            var eligible = user != null
                           && member != null
                           && ParticipationFacts.EligibleParticipant.Compile()(user, member);

            // Ownership is resolved through members.user_id, so the actor can only ever see their own row.
            var registration = await _registrations.GetMemberRegistrationAsync(eventId, actorUserId, cancellationToken);
            model.HasRegistration = registration != null;
            model.RegistrationId = registration?.RegistrationId;
            model.RegistrationStatus = registration?.Status;
            model.CanCancel = registration != null
                              && (registration.Status is RegistrationStatusCode.Pending
                                  or RegistrationStatusCode.Confirmed
                                  or RegistrationStatusCode.Waitlisted)
                              && row.EventStatusId == EventStatusCode.Published
                              && !model.IsStartCutoffPassed
                              && !await _registrations.HasValidAttendanceAsync(registration.RegistrationId, cancellationToken);
            model.CanRegister = eligible && model.IsSignupOpen && !model.HasRegistration;
            return model;
        }

        public async Task<MemberRegistrationResult> RegisterAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireAuthenticatedActor();

            return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // AD-4: the owner's users row is locked first; that lock also protects the member row.
                var user = await _users.LockUserAsync(actorUserId, ct);
                if (user == null || !user.IsActive)
                {
                    throw new DomainException(OutcomeCode.Forbidden, "An active member account is required to register.");
                }

                var member = await _members.GetMemberByUserIdAsync(actorUserId, ct);
                if (member == null || !ParticipationFacts.EligibleParticipant.Compile()(user, member))
                {
                    throw new DomainException(OutcomeCode.Forbidden, "An active membership is required to register.");
                }

                var eventItem = await _events.LockEventAsync(eventId, ct);
                if (eventItem == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "Event not found.");
                }

                // Capture time after both locks so the cutoff guard and the stored snapshot agree.
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (!ParticipationFacts.SignupOpen(now).Compile()(eventItem))
                {
                    throw new DomainException(OutcomeCode.CutoffPassed, "Signup for this event is closed.");
                }

                if (await _registrations.ExistsAsync(eventId, member.MemberId, ct))
                {
                    throw new DomainException(OutcomeCode.Duplicate, "You already have a registration for this event.");
                }

                var confirmedCount = await _registrations.CountConfirmedAsync(eventId, ct);

                var registration = new Registration
                {
                    MemberId = member.MemberId,
                    EventId = eventItem.EventId,
                    RegistrationStatusId = RegistrationStatusCode.Pending,
                    CourseSnapshot = member.Course,
                    YearLevelSnapshot = member.YearLevel,
                    SectionSnapshot = member.Section,
                    SubmittedAt = now,
                    CreatedBy = actorUserId,
                    CreatedAt = now
                };

                await _registrations.AddAsync(registration, ct);
                try
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                catch (DomainException exception) when (exception.Outcome == OutcomeCode.Duplicate)
                {
                    // The unique (event_id, member_id) key is the final guard for a concurrent submission.
                    throw new DomainException(OutcomeCode.Duplicate, "You already have a registration for this event.");
                }

                return new MemberRegistrationResult(registration.RegistrationId, confirmedCount >= eventItem.ParticipantLimit);
            }, cancellationToken);
        }

        public async Task CancelRegistrationAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireAuthenticatedActor();

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // Discover the actor's own row before locking; recheck it after both locks (AD-4).
                var discovered = await _registrations.GetMemberRegistrationAsync(eventId, actorUserId, ct);
                if (discovered == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "You have no registration for this event.");
                }

                // AD-4: the owner's users row is locked first; that lock also protects the member row.
                var user = await _users.LockUserAsync(actorUserId, ct);
                var member = user == null ? null : await _members.GetMemberByUserIdAsync(actorUserId, ct);
                if (member == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "You have no registration for this event.");
                }

                var eventItem = await _events.LockEventAsync(eventId, ct);
                if (eventItem == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "Event not found.");
                }

                // Capture time after both locks so the cutoff guard and the stored snapshot agree.
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (eventItem.EventStatusId != EventStatusCode.Published)
                {
                    throw new DomainException(OutcomeCode.Stale, "This event is no longer published, so the registration can no longer be cancelled.");
                }

                if (StartCutoffPassed(eventItem.StartsAt, now))
                {
                    throw new DomainException(OutcomeCode.CutoffPassed, "This event has already started, so the registration can no longer be cancelled.");
                }

                var row = await _registrations.GetMemberRegistrationAsync(eventId, actorUserId, ct);
                if (row == null || row.RegistrationId != discovered.RegistrationId)
                {
                    throw new DomainException(OutcomeCode.Stale, "Your registration changed. Refresh the page and try again.");
                }

                if (row.Status is not (RegistrationStatusCode.Pending or RegistrationStatusCode.Confirmed or RegistrationStatusCode.Waitlisted))
                {
                    throw new DomainException(OutcomeCode.Stale, "This registration can no longer be cancelled.");
                }

                if (await _registrations.HasValidAttendanceAsync(row.RegistrationId, ct))
                {
                    throw new DomainException(OutcomeCode.Invalid, "Attendance has already been recorded for this registration.");
                }

                // IRegistrationCancellationService is the only writer that cancels a registration.
                await _cancellation.CancelRegistrationAsync(row.RegistrationId, CancellationCause.MemberRequested, null, ct);
            }, cancellationToken);
        }

        private static bool StartCutoffPassed(DateTime startsAt, DateTime now) =>
            ParticipationFacts.StartCutoffPassed(now).Compile()(new Event { StartsAt = startsAt });

        private static bool SignupOpen(EventStatusCode status, DateTime registrationDeadline, Func<Event, bool> signupOpen) =>
            signupOpen(new Event
            {
                EventStatusId = status,
                RegistrationDeadline = registrationDeadline
            });

        private int RequireAuthenticatedActor()
        {
            if (!_actor.IsAuthenticated)
            {
                throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to view events.");
            }

            int userId;
            try
            {
                userId = _actor.UserId;
            }
            catch (InvalidOperationException)
            {
                throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to view events.");
            }

            if (userId <= 0)
            {
                throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to view events.");
            }

            return userId;
        }
    }
}
