using ASI.Basecode.Data.Queries;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class EventTypeService : IEventTypeService
    {
        private readonly ListReadOptions _readOptions;
        private readonly IEventTypeRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly IMapper _mapper;
        private readonly IActivityLogWriter _audit;
        private readonly ICurrentActor _actor;

        public EventTypeService(IEventTypeRepository repository, IUnitOfWork unitOfWork, IMapper mapper, TimeProvider timeProvider, IActivityLogWriter audit, ICurrentActor actor, ListReadOptions readOptions = null)
        {
            _readOptions = readOptions;
            _repository = repository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _mapper = mapper;
            _audit = audit;
            _actor = actor;
        }

        public async Task<PagedResult<EventTypeViewModel>> GetPageAsync(string status, int? page, CancellationToken cancellationToken = default)
        {
            var totalCount = await _repository.CountEventTypesAsync(status, cancellationToken);
            var pageRequest = new PageRequest(page, _readOptions?.PageSize);
            var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
            var eventTypes = await _repository.GetEventTypesPageAsync(
                status,
                pageRequest.GetOffset(normalizedPage),
                PageRequest.NormalizeSize(_readOptions?.PageSize),
                cancellationToken);
            var items = _mapper.Map<List<EventTypeViewModel>>(eventTypes);
            return new PagedResult<EventTypeViewModel>(items, normalizedPage, totalCount, PageRequest.NormalizeSize(_readOptions?.PageSize));
        }

        public EventTypeViewModel GetEventType(int id)
        {
            var eventType = _repository.GetEventType(id);
            return eventType == null ? null : _mapper.Map<EventTypeViewModel>(eventType);
        }

        private sealed record EventTypeAuditValues(string TypeName, string Description, bool IsActive) : IAuditValues;

        private sealed record EventTypeStatusAuditValues(bool IsActive, int LinkedEventCount) : IAuditValues;

        private static EventTypeAuditValues Snapshot(EventType eventType) =>
            new(eventType.TypeName, eventType.Description, eventType.IsActive);

        private int RequireActorId()
        {
            if (!_actor.IsAuthenticated)
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated user is required to change an event type.");
            }

            int userId;
            try
            {
                userId = _actor.UserId;
            }
            catch (InvalidOperationException)
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated user is required to change an event type.");
            }

            if (userId <= 0)
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated user is required to change an event type.");
            }

            return userId;
        }

        private static string RequiredTypeName(EventTypeViewModel model)
        {
            var typeName = model.TypeName?.Trim();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new DomainException(OutcomeCode.Invalid, "Type name is required.", nameof(EventTypeViewModel.TypeName));
            }

            return typeName;
        }

        private static string RequiredDescription(EventTypeViewModel model)
        {
            var description = model.Description?.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new DomainException(OutcomeCode.Invalid, "Description is required.", nameof(EventTypeViewModel.Description));
            }

            return description;
        }

        public async Task AddEventTypeAsync(EventTypeViewModel model, CancellationToken cancellationToken = default)
        {
            var actorId = RequireActorId();
            var typeName = RequiredTypeName(model);
            var description = RequiredDescription(model);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (_repository.TypeNameExists(typeName))
                {
                    throw new DomainException(OutcomeCode.Duplicate, "Event type name already exists.", nameof(EventTypeViewModel.TypeName));
                }

                var eventType = new EventType
                {
                    TypeName = typeName,
                    Description = description,
                    IsActive = true,
                    CreatedBy = actorId,
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                };
                await _repository.AddEventTypeAsync(eventType, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                _audit.Append(new AuditEntry(AuditActions.EventTypeCreated, "event_types", eventType.TypeId, NewValues: Snapshot(eventType)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public async Task UpdateEventTypeAsync(EventTypeViewModel model, CancellationToken cancellationToken = default)
        {
            var actorId = RequireActorId();
            var typeName = RequiredTypeName(model);
            var description = RequiredDescription(model);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var eventType = await _repository.LockEventTypeAsync(model.TypeId, ct);
                if (eventType == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "Event type not found.");
                }

                if (_repository.TypeNameExists(typeName, model.TypeId))
                {
                    throw new DomainException(OutcomeCode.Duplicate, "Event type name already exists.", nameof(EventTypeViewModel.TypeName));
                }

                var oldValues = Snapshot(eventType);
                eventType.TypeName = typeName;
                eventType.Description = description;
                eventType.UpdatedBy = actorId;
                eventType.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _repository.UpdateEventTypeAsync(eventType, ct);
                _audit.Append(new AuditEntry(AuditActions.EventTypeEdited, "event_types", eventType.TypeId,
                    OldValues: oldValues, NewValues: Snapshot(eventType)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public async Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default)
        {
            var actorId = RequireActorId();

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var eventType = await _repository.LockEventTypeAsync(id, ct);
                if (eventType == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "Event type not found.");
                }

                var linkedEventCount = await _repository.CountLinkedEventsAsync(id, ct);
                var oldValues = new EventTypeStatusAuditValues(eventType.IsActive, linkedEventCount);
                eventType.IsActive = !eventType.IsActive;
                eventType.UpdatedBy = actorId;
                eventType.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _repository.UpdateEventTypeAsync(eventType, ct);
                _audit.Append(new AuditEntry(AuditActions.EventTypeStatusChanged, "event_types", eventType.TypeId,
                    OldValues: oldValues, NewValues: new EventTypeStatusAuditValues(eventType.IsActive, linkedEventCount)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
    }
}
