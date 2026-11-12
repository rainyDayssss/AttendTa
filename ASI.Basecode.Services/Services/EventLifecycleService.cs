using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Event lifecycle use cases: draft creation and editing, publish, cancel, complete, and cover
    /// management. Every write locks the Event Type, then the Venue, then the Event, in the order
    /// AD-4 requires, and never converts time zones (AD-11).
    /// </summary>
    public class EventLifecycleService : IEventLifecycleService
    {
        private readonly IEventRepository _events;
        private readonly IEventTypeRepository _eventTypes;
        private readonly IVenueRepository _venues;
        private readonly IRegistrationRepository _registrations;
        private readonly IRegistrationCancellationService _cancellations;
        private readonly IEnumerable<IEventCompletionHandler> _completionHandlers;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly IMapper _mapper;
        private readonly IActivityLogWriter _audit;
        private readonly ICurrentActor _actor;
        private readonly IPrivateFileStore _files;
        private readonly ILogger<EventLifecycleService> _logger;

        private const string EventCoverPrefix = "uploads/events/";

        public EventLifecycleService(
            IEventRepository events,
            IEventTypeRepository eventTypes,
            IVenueRepository venues,
            IRegistrationRepository registrations,
            IRegistrationCancellationService cancellations,
            IEnumerable<IEventCompletionHandler> completionHandlers,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            TimeProvider timeProvider,
            IActivityLogWriter audit,
            ICurrentActor actor,
            IPrivateFileStore files,
            ILogger<EventLifecycleService> logger)
        {
            _events = events;
            _eventTypes = eventTypes;
            _venues = venues;
            _registrations = registrations;
            _cancellations = cancellations;
            _completionHandlers = completionHandlers ?? Array.Empty<IEventCompletionHandler>();
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _timeProvider = timeProvider;
            _audit = audit;
            _actor = actor;
            _files = files;
            _logger = logger;
        }

        public async Task<PagedResult<EventViewModel>> GetPageAsync(int? pageNumber, CancellationToken cancellationToken = default)
        {
            var totalCount = await _events.CountAsync(cancellationToken);
            var pageRequest = new PageRequest(pageNumber);
            var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
            var rows = await _events.GetPageAsync(
                pageRequest.GetOffset(normalizedPage),
                PageRequest.FixedPageSize,
                cancellationToken);
            var items = _mapper.Map<List<EventViewModel>>(rows);
            return new PagedResult<EventViewModel>(items, normalizedPage, totalCount);
        }

        public async Task<EventViewModel> GetEventAsync(int id, CancellationToken cancellationToken = default)
        {
            var eventItem = await _events.GetEventAsync(id, cancellationToken);
            if (eventItem == null) return null;
            var model = _mapper.Map<EventViewModel>(eventItem);
            model.TypeName = _eventTypes.GetEventType(eventItem.TypeId)?.TypeName;
            model.VenueName = _venues.GetVenue(eventItem.VenueId)?.RoomName;
            return model;
        }

        public async Task<EventFormOptions> GetFormOptionsAsync(CancellationToken cancellationToken = default)
        {
            var eventTypes = await _eventTypes.GetActiveEventTypesAsync(cancellationToken);
            var venues = await _venues.GetActiveVenuesAsync(cancellationToken);
            return new EventFormOptions
            {
                EventTypes = eventTypes.Select(eventType => new EventTypeOption(eventType.TypeId, eventType.TypeName)).ToList(),
                Venues = venues.Select(venue => new VenueOption(venue.VenueId, venue.RoomName, venue.Capacity)).ToList()
            };
        }

        public async Task<int> CreateDraftAsync(EventViewModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            var actorUserId = RequireManagementActor();
            var title = RequiredTitle(model);
            ValidateTimes(model);
            ValidateLimit(model);

            return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var venue = await LockActiveReferencesAsync(model.TypeId, model.VenueId, ct);
                ValidateCapacity(model, venue);

                var eventItem = new Event
                {
                    Title = title,
                    Description = NormalizedDescription(model),
                    TypeId = model.TypeId,
                    VenueId = model.VenueId,
                    StartsAt = model.StartsAt,
                    RegistrationDeadline = model.RegistrationDeadline,
                    ParticipantLimit = model.ParticipantLimit,
                    EventStatusId = EventStatusCode.Draft,
                    CreatedBy = actorUserId,
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                };
                await _events.AddEventAsync(eventItem, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return eventItem.EventId;
            }, cancellationToken);
        }

        public async Task UpdateDraftAsync(EventViewModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            var actorUserId = RequireManagementActor();
            var title = RequiredTitle(model);
            ValidateTimes(model);
            ValidateLimit(model);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var discovered = await RequireEventAsync(model.EventId, ct);

                // The controller replaces the posted status with its own read before invoking us.
                if (discovered.EventStatusId != model.EventStatusId)
                {
                    throw new DomainException(OutcomeCode.Stale, "The event changed while it was being edited.");
                }

                if (discovered.EventStatusId == EventStatusCode.Draft)
                {
                    var venue = await LockActiveReferencesAsync(model.TypeId, model.VenueId, ct);
                    ValidateCapacity(model, venue);
                }
                else
                {
                    await LockCoverReferencesAsync(discovered, ct);
                }

                var eventItem = await LockEventAsync(
                    model.EventId,
                    locked => locked.EventStatusId == discovered.EventStatusId
                              && locked.TypeId == discovered.TypeId
                              && locked.VenueId == discovered.VenueId,
                    "The event changed while it was being edited.",
                    ct);

                if (eventItem.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
                {
                    throw new DomainException(OutcomeCode.Invalid, "Only draft or published events can be edited.");
                }

                if (eventItem.EventStatusId == EventStatusCode.Published &&
                    (model.TypeId != eventItem.TypeId || model.VenueId != eventItem.VenueId ||
                     model.StartsAt != eventItem.StartsAt || model.RegistrationDeadline != eventItem.RegistrationDeadline ||
                     model.ParticipantLimit != eventItem.ParticipantLimit))
                {
                    throw new DomainException(OutcomeCode.Invalid, "Published event schedule, type, venue and limit cannot be changed.");
                }

                var oldValues = Snapshot(eventItem);
                eventItem.Title = title;
                eventItem.Description = NormalizedDescription(model);
                eventItem.TypeId = model.TypeId;
                eventItem.VenueId = model.VenueId;
                eventItem.StartsAt = model.StartsAt;
                eventItem.RegistrationDeadline = model.RegistrationDeadline;
                eventItem.ParticipantLimit = model.ParticipantLimit;
                eventItem.UpdatedBy = actorUserId;
                eventItem.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _events.UpdateEventAsync(eventItem, ct);
                _audit.Append(new AuditEntry(AuditActions.EventUpdated, "events", eventItem.EventId,
                    OldValues: oldValues, NewValues: Snapshot(eventItem)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public async Task PublishAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireManagementActor();
            var discovered = await RequireEventAsync(eventId, cancellationToken);

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var eventType = await _eventTypes.LockEventTypeAsync(discovered.TypeId, ct);
                var venue = await _venues.LockVenueAsync(discovered.VenueId, ct);
                var eventItem = await LockEventAsync(
                    eventId,
                    locked => locked.TypeId == discovered.TypeId && locked.VenueId == discovered.VenueId,
                    "The event changed while it was being published.",
                    ct);

                // Capture request time after all rows are locked so the deadline guard and persisted audit agree.
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (eventType == null || venue == null)
                {
                    throw new DomainException(OutcomeCode.Stale, "The event changed while it was being published.");
                }

                if (!ParticipationFacts.Publishable(now).Compile()(eventItem, eventType, venue))
                {
                    throw new DomainException(OutcomeCode.Invalid, "This event no longer meets the publication requirements.");
                }

                var oldValues = new EventPublicationAuditValues(eventItem.EventStatusId, eventItem.PublishedBy, eventItem.PublishedAt);
                eventItem.EventStatusId = EventStatusCode.Published;
                eventItem.PublishedBy = actorUserId;
                eventItem.PublishedAt = now;
                eventItem.UpdatedBy = actorUserId;
                eventItem.UpdatedAt = now;
                await _events.UpdateEventAsync(eventItem, ct);
                _audit.Append(new AuditEntry(AuditActions.EventPublished, "events", eventItem.EventId,
                    OldValues: oldValues,
                    NewValues: new EventPublicationAuditValues(eventItem.EventStatusId, eventItem.PublishedBy, eventItem.PublishedAt)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public async Task CancelAsync(int eventId, string reason, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireManagementActor();
            var discovered = await RequireEventAsync(eventId, cancellationToken);

            if (discovered.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
            {
                throw new DomainException(OutcomeCode.Invalid, "Only draft or published events can be cancelled.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // AD-4: event cancellation takes only the event lock.
                var eventItem = await LockEventAsync(
                    eventId,
                    locked => locked.EventStatusId == discovered.EventStatusId,
                    "The event changed while it was being cancelled.",
                    ct);

                if (eventItem.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
                {
                    throw new DomainException(OutcomeCode.Invalid, "Only draft or published events can be cancelled.");
                }

                var cancellationReason = reason?.Trim();
                if (string.IsNullOrWhiteSpace(cancellationReason))
                {
                    throw new DomainException(OutcomeCode.Invalid, "A cancellation reason is required.", nameof(EventCancelViewModel.Reason));
                }

                // Canonical facts are read under the event lock; attendance and certificates are
                // written only while holding it.
                if (await _registrations.HasValidAttendanceForEventAsync(eventId, ct))
                {
                    throw new DomainException(OutcomeCode.Invalid, "This event has recorded attendance and cannot be cancelled.");
                }

                if (await _registrations.HasIssuedCertificateForEventAsync(eventId, ct))
                {
                    throw new DomainException(OutcomeCode.Invalid, "This event has an issued certificate and cannot be cancelled.");
                }

                // Capture request time after the row is locked so the metadata and audit agree.
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var oldValues = new EventCancellationAuditValues(
                    eventItem.EventStatusId, eventItem.IsCheckInOpen, eventItem.CancelledBy, eventItem.CancelledAt, eventItem.CancellationReason);

                // The cancellation service is the only code that cancels registrations and clears QR tokens.
                await _cancellations.CancelAllForEventAsync(eventId, CancellationCause.EventCancelled, cancellationReason, ct);

                eventItem.EventStatusId = EventStatusCode.Cancelled;
                eventItem.CancelledBy = actorUserId;
                eventItem.CancelledAt = now;
                eventItem.CancellationReason = cancellationReason;
                CloseCheckIn(eventItem, actorUserId, now);
                eventItem.UpdatedBy = actorUserId;
                eventItem.UpdatedAt = now;
                await _events.UpdateEventAsync(eventItem, ct);
                _audit.Append(new AuditEntry(AuditActions.EventCancelled, "events", eventItem.EventId, cancellationReason,
                    OldValues: oldValues,
                    NewValues: new EventCancellationAuditValues(
                        eventItem.EventStatusId, eventItem.IsCheckInOpen, eventItem.CancelledBy, eventItem.CancelledAt, eventItem.CancellationReason)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public async Task CompleteAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireManagementActor();
            var discovered = await RequireEventAsync(eventId, cancellationToken);

            if (discovered.EventStatusId != EventStatusCode.Published)
            {
                throw new DomainException(OutcomeCode.Invalid, "Only a published event can be completed.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // AD-4: completion takes only the event lock.
                var eventItem = await LockEventAsync(
                    eventId,
                    locked => locked.EventStatusId == discovered.EventStatusId,
                    "The event changed while it was being completed.",
                    ct);

                if (eventItem.EventStatusId != EventStatusCode.Published)
                {
                    throw new DomainException(OutcomeCode.Invalid, "Only a published event can be completed.");
                }

                // Capture request time after the row is locked so the cutoff guard, the stored
                // metadata and the audit agree.
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                if (!ParticipationFacts.StartCutoffPassed(now).Compile()(eventItem))
                {
                    throw new DomainException(OutcomeCode.Invalid, "An event cannot be completed before it starts.");
                }

                var oldValues = new EventCompletionAuditValues(
                    eventItem.EventStatusId, eventItem.IsCheckInOpen, eventItem.CompletedBy, eventItem.CompletedAt);

                eventItem.EventStatusId = EventStatusCode.Completed;
                eventItem.CompletedBy = actorUserId;
                eventItem.CompletedAt = now;
                CloseCheckIn(eventItem, actorUserId, now);
                eventItem.UpdatedBy = actorUserId;
                eventItem.UpdatedAt = now;
                await _events.UpdateEventAsync(eventItem, ct);
                _audit.Append(new AuditEntry(AuditActions.EventCompleted, "events", eventItem.EventId,
                    OldValues: oldValues,
                    NewValues: new EventCompletionAuditValues(
                        eventItem.EventStatusId, eventItem.IsCheckInOpen, eventItem.CompletedBy, eventItem.CompletedAt)));
                await _unitOfWork.SaveChangesAsync(ct);

                // Hooks run after the completion row is saved but inside the caller's transaction, so
                // they observe Completed and a throw rolls the whole completion back (AD-2, AD-3).
                var context = new EventCompletionContext(eventItem.EventId, actorUserId, now);
                foreach (var handler in _completionHandlers)
                {
                    await handler.HandleAsync(context, ct);
                }
            }, cancellationToken);
        }

        public async Task SetCheckInWindowAsync(int eventId, bool open, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireManagementActor();
            var discovered = await RequireEventAsync(eventId, cancellationToken);

            if (discovered.EventStatusId != EventStatusCode.Published)
            {
                throw new DomainException(OutcomeCode.Invalid, "Check-in can only be changed for a published event.");
            }

            if (discovered.IsCheckInOpen == open)
            {
                throw new DomainException(OutcomeCode.Invalid, open
                    ? "Check-in is already open for this event."
                    : "Check-in is already closed for this event.");
            }

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // AD-4: the check-in window takes only the event lock.
                var eventItem = await LockEventAsync(
                    eventId,
                    locked => locked.EventStatusId == discovered.EventStatusId,
                    "The event changed while its check-in window was being updated.",
                    ct);

                if (eventItem.IsCheckInOpen != discovered.IsCheckInOpen)
                {
                    throw new DomainException(OutcomeCode.Invalid, "The check-in window changed. Refresh the page and try again.");
                }

                if (eventItem.EventStatusId != EventStatusCode.Published)
                {
                    throw new DomainException(OutcomeCode.Invalid, "Check-in can only be changed for a published event.");
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var oldValues = new EventCheckInWindowAuditValues(
                    eventItem.EventStatusId,
                    eventItem.IsCheckInOpen,
                    eventItem.CheckInOpenedBy,
                    eventItem.CheckInOpenedAt,
                    eventItem.CheckInClosedBy,
                    eventItem.CheckInClosedAt);

                eventItem.IsCheckInOpen = open;
                if (open)
                {
                    eventItem.CheckInOpenedBy = actorUserId;
                    eventItem.CheckInOpenedAt = now;
                }
                else
                {
                    eventItem.CheckInClosedBy = actorUserId;
                    eventItem.CheckInClosedAt = now;
                }

                eventItem.UpdatedBy = actorUserId;
                eventItem.UpdatedAt = now;
                await _events.UpdateEventAsync(eventItem, ct);
                _audit.Append(new AuditEntry(
                    open ? AuditActions.EventCheckInOpened : AuditActions.EventCheckInClosed,
                    "events",
                    eventItem.EventId,
                    OldValues: oldValues,
                    NewValues: new EventCheckInWindowAuditValues(
                        eventItem.EventStatusId,
                        eventItem.IsCheckInOpen,
                        eventItem.CheckInOpenedBy,
                        eventItem.CheckInOpenedAt,
                        eventItem.CheckInClosedBy,
                        eventItem.CheckInClosedAt)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        public async Task<EventCoverViewModel> GetCoverManagementAsync(int eventId, CancellationToken cancellationToken = default)
        {
            RequireManagementActor();
            var eventItem = await RequireEventAsync(eventId, cancellationToken);

            return new EventCoverViewModel
            {
                EventId = eventItem.EventId,
                Title = eventItem.Title,
                EventStatusId = eventItem.EventStatusId,
                HasCover = IsEventCover(eventItem.ImagePath)
            };
        }

        public async Task<EventCoverStream> OpenCoverAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var role = AuthenticatedRole();
            var isManagement = role is RoleCodes.Admin or RoleCodes.Officer;
            if (!isManagement && role != RoleCodes.Member)
            {
                throw new DomainException(OutcomeCode.Forbidden, "You are not allowed to view this event cover.");
            }

            var eventItem = await _events.GetEventAsync(eventId, cancellationToken);
            if (eventItem == null || (!isManagement && eventItem.EventStatusId is not (EventStatusCode.Published or EventStatusCode.Completed)))
            {
                throw new DomainException(OutcomeCode.NotFound, "Event cover not found.");
            }

            if (!IsEventCover(eventItem.ImagePath))
            {
                throw new DomainException(OutcomeCode.NotFound, "Event cover not found.");
            }

            try
            {
                var stream = await _files.OpenReadAsync(eventItem.ImagePath, cancellationToken);
                var contentType = Path.GetExtension(eventItem.ImagePath).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    _ => throw new DomainException(OutcomeCode.NotFound, "Event cover not found.")
                };
                return new EventCoverStream(stream, contentType);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                throw new DomainException(OutcomeCode.NotFound, "Event cover not found.");
            }
        }

        public async Task UploadCoverAsync(int eventId, Stream image, string extension, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireManagementActor();
            if (image == null)
            {
                throw new DomainException(OutcomeCode.Invalid, "Choose a JPEG or PNG cover image.", "cover");
            }

            var discovered = await RequireEventAsync(eventId, cancellationToken);

            string newPath = null;
            string oldPath = null;
            try
            {
                // File I/O happens before taking any SQL row locks.
                newPath = await _files.SaveAsync(PrivateFileArea.EventImages, image, extension, cancellationToken);
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await LockCoverReferencesAsync(discovered, ct);
                    var eventItem = await LockEventAsync(eventId, ct);

                    EnsureSameCoverReferences(discovered, eventItem);
                    if (eventItem.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
                    {
                        throw new DomainException(OutcomeCode.Invalid, "Event covers can only be changed for draft or published events.", "cover");
                    }

                    oldPath = eventItem.ImagePath;
                    var oldValues = CoverAuditValues(oldPath);
                    eventItem.ImagePath = newPath;
                    eventItem.UpdatedBy = actorUserId;
                    eventItem.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await _events.UpdateEventAsync(eventItem, ct);
                    _audit.Append(new AuditEntry(AuditActions.EventUpdated, "events", eventItem.EventId,
                        OldValues: oldValues, NewValues: CoverAuditValues(newPath)));
                    await _unitOfWork.SaveChangesAsync(ct);
                }, cancellationToken);
            }
            catch
            {
                DeleteBestEffort(newPath);
                throw;
            }

            if (IsEventCover(oldPath) && !string.Equals(oldPath, newPath, StringComparison.Ordinal))
            {
                DeleteBestEffort(oldPath);
            }
        }

        public async Task RemoveDraftCoverAsync(int eventId, CancellationToken cancellationToken = default)
        {
            var actorUserId = RequireManagementActor();
            var discovered = await RequireEventAsync(eventId, cancellationToken);

            string oldPath = null;
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await LockCoverReferencesAsync(discovered, ct);
                var eventItem = await LockEventAsync(eventId, ct);

                EnsureSameCoverReferences(discovered, eventItem);
                if (eventItem.EventStatusId != EventStatusCode.Draft)
                {
                    throw new DomainException(OutcomeCode.Invalid, "Only draft event covers can be removed.", "cover");
                }

                oldPath = eventItem.ImagePath;
                if (!IsEventCover(oldPath))
                {
                    throw new DomainException(OutcomeCode.Invalid, "This draft does not have a removable cover.", "cover");
                }

                var oldValues = CoverAuditValues(oldPath);
                eventItem.ImagePath = null;
                eventItem.UpdatedBy = actorUserId;
                eventItem.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _events.UpdateEventAsync(eventItem, ct);
                _audit.Append(new AuditEntry(AuditActions.EventUpdated, "events", eventItem.EventId,
                    OldValues: oldValues, NewValues: CoverAuditValues(null)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);

            DeleteBestEffort(oldPath);
        }

        private sealed record EventCoverAuditValues(bool HasCover, bool CoverChanged) : IAuditValues;

        private static EventCoverAuditValues CoverAuditValues(string path) => new(IsEventCover(path), true);

        private static bool IsEventCover(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(EventCoverPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var fileName = path[EventCoverPrefix.Length..];
            if (fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                return false;
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var fileId = Path.GetFileNameWithoutExtension(fileName);
            return extension is ".jpg" or ".jpeg" or ".png" && Guid.TryParseExact(fileId, "N", out _);
        }

        private string AuthenticatedRole()
        {
            if (!_actor.IsAuthenticated)
            {
                throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to manage events.");
            }

            try
            {
                return _actor.Role;
            }
            catch (InvalidOperationException)
            {
                throw new DomainException(OutcomeCode.Forbidden, "Authentication is required to manage events.");
            }
        }

        private int RequireManagementActor()
        {
            var role = AuthenticatedRole();
            if (role is not (RoleCodes.Admin or RoleCodes.Officer))
            {
                throw new DomainException(OutcomeCode.Forbidden, "Management access is required to manage events.");
            }

            return ActorUserId();
        }

        private async Task<Event> RequireEventAsync(int eventId, CancellationToken cancellationToken)
        {
            var eventItem = await _events.GetEventAsync(eventId, cancellationToken);
            if (eventItem == null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Event not found.");
            }

            return eventItem;
        }

        /// <summary>
        /// Locks the event row inside the caller's transaction and refuses a missing event.
        /// </summary>
        private async Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken)
        {
            var eventItem = await _events.LockEventAsync(eventId, cancellationToken);
            if (eventItem == null)
            {
                throw new DomainException(OutcomeCode.NotFound, "Event not found.");
            }

            return eventItem;
        }

        /// <summary>
        /// Locks the event row and reports <c>Stale</c> when <paramref name="stillCurrent"/>
        /// rejects the freshly locked row, so a concurrent change is refused instead of applied.
        /// Must run inside the caller's transaction, which is what makes the lock meaningful.
        /// </summary>
        private async Task<Event> LockEventAsync(
            int eventId,
            Func<Event, bool> stillCurrent,
            string staleMessage,
            CancellationToken cancellationToken)
        {
            var eventItem = await LockEventAsync(eventId, cancellationToken);
            if (!stillCurrent(eventItem))
            {
                throw new DomainException(OutcomeCode.Stale, staleMessage);
            }

            return eventItem;
        }

        private static void CloseCheckIn(Event eventItem, int actorUserId, DateTime now)
        {
            if (eventItem.IsCheckInOpen)
            {
                eventItem.CheckInClosedBy = actorUserId;
                eventItem.CheckInClosedAt = now;
            }

            eventItem.IsCheckInOpen = false;
        }

        private async Task LockCoverReferencesAsync(Event discovered, CancellationToken cancellationToken)
        {
            // Cover-only commands preserve the global type -> venue -> event lock order without
            // requiring references to remain active after an event has been created.
            if (await _eventTypes.LockEventTypeAsync(discovered.TypeId, cancellationToken) == null
                || await _venues.LockVenueAsync(discovered.VenueId, cancellationToken) == null)
            {
                throw new DomainException(OutcomeCode.Stale, "The event changed while its cover was being updated.");
            }
        }

        private static void EnsureSameCoverReferences(Event discovered, Event locked)
        {
            if (discovered.TypeId != locked.TypeId || discovered.VenueId != locked.VenueId || discovered.EventStatusId != locked.EventStatusId)
            {
                throw new DomainException(OutcomeCode.Stale, "The event changed while its cover was being updated.");
            }
        }

        private void DeleteBestEffort(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                _files.Delete(path);
            }
            catch (Exception)
            {
                // Do not include private storage keys in diagnostics; a committed row remains authoritative.
                _logger.LogWarning("Could not clean up an event cover file.");
            }
        }

        private sealed record EventAuditValues(
            string Title,
            string Description,
            int TypeId,
            string TypeName,
            int VenueId,
            string VenueName,
            DateTime StartsAt,
            DateTime RegistrationDeadline,
            int ParticipantLimit,
            EventStatusCode Status) : IAuditValues;

        // Names come from the read repositories so the audit records the old reference names too.
        private EventAuditValues Snapshot(Event eventItem) =>
            new(eventItem.Title,
                eventItem.Description,
                eventItem.TypeId,
                _eventTypes.GetEventType(eventItem.TypeId)?.TypeName,
                eventItem.VenueId,
                _venues.GetVenue(eventItem.VenueId)?.RoomName,
                eventItem.StartsAt,
                eventItem.RegistrationDeadline,
                eventItem.ParticipantLimit,
                eventItem.EventStatusId);

        private sealed record EventPublicationAuditValues(EventStatusCode Status, int? PublishedBy, DateTime? PublishedAt) : IAuditValues;

        private sealed record EventCancellationAuditValues(
            EventStatusCode Status,
            bool IsCheckInOpen,
            int? CancelledBy,
            DateTime? CancelledAt,
            string CancellationReason) : IAuditValues;

        private sealed record EventCompletionAuditValues(
            EventStatusCode Status,
            bool IsCheckInOpen,
            int? CompletedBy,
            DateTime? CompletedAt) : IAuditValues;

        private sealed record EventCheckInWindowAuditValues(
            EventStatusCode Status,
            bool IsCheckInOpen,
            int? CheckInOpenedBy,
            DateTime? CheckInOpenedAt,
            int? CheckInClosedBy,
            DateTime? CheckInClosedAt) : IAuditValues;

        private int ActorUserId()
        {
            if (!_actor.IsAuthenticated)
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated user is required to change an event.");
            }

            int userId;
            try
            {
                userId = _actor.UserId;
            }
            catch (InvalidOperationException)
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated user is required to change an event.");
            }

            if (userId <= 0)
            {
                throw new DomainException(OutcomeCode.Forbidden, "A valid authenticated user is required to change an event.");
            }

            return userId;
        }

        private static string RequiredTitle(EventViewModel model)
        {
            var title = model.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new DomainException(OutcomeCode.Invalid, "Title is required.", nameof(EventViewModel.Title));
            }

            if (title.Length > 255)
            {
                throw new DomainException(OutcomeCode.Invalid, "Title cannot exceed 255 characters.", nameof(EventViewModel.Title));
            }

            return title;
        }

        private static string NormalizedDescription(EventViewModel model)
        {
            var description = model.Description?.Trim();
            return string.IsNullOrWhiteSpace(description) ? null : description;
        }

        private static void ValidateTimes(EventViewModel model)
        {
            if (model.StartsAt == default)
            {
                throw new DomainException(OutcomeCode.Invalid, "Start date and time is required.", nameof(EventViewModel.StartsAt));
            }

            if (model.RegistrationDeadline == default)
            {
                throw new DomainException(OutcomeCode.Invalid, "Signup deadline is required.", nameof(EventViewModel.RegistrationDeadline));
            }

            if (model.RegistrationDeadline > model.StartsAt)
            {
                throw new DomainException(OutcomeCode.Invalid, "The signup deadline cannot be after the event start.", nameof(EventViewModel.RegistrationDeadline));
            }
        }

        private static void ValidateLimit(EventViewModel model)
        {
            if (model.ParticipantLimit <= 0)
            {
                throw new DomainException(OutcomeCode.Invalid, "Participant limit must be greater than 0.", nameof(EventViewModel.ParticipantLimit));
            }
        }

        private static void ValidateCapacity(EventViewModel model, Venue venue)
        {
            if (model.ParticipantLimit > venue.Capacity)
            {
                throw new DomainException(OutcomeCode.Invalid, "Participant limit cannot exceed the venue capacity.", nameof(EventViewModel.ParticipantLimit));
            }
        }

        // AD-4: lock the Event Type, then the Venue, before the Event row is written.
        private async Task<Venue> LockActiveReferencesAsync(int typeId, int venueId, CancellationToken cancellationToken)
        {
            if (typeId <= 0)
            {
                throw new DomainException(OutcomeCode.Invalid, "Select an event type.", nameof(EventViewModel.TypeId));
            }

            if (venueId <= 0)
            {
                throw new DomainException(OutcomeCode.Invalid, "Select a venue.", nameof(EventViewModel.VenueId));
            }

            var eventType = await _eventTypes.LockEventTypeAsync(typeId, cancellationToken);
            if (eventType == null || !eventType.IsActive)
            {
                throw new DomainException(OutcomeCode.Invalid, "Select an active event type.", nameof(EventViewModel.TypeId));
            }

            var venue = await _venues.LockVenueAsync(venueId, cancellationToken);
            if (venue == null || !venue.IsActive)
            {
                throw new DomainException(OutcomeCode.Invalid, "Select an active venue.", nameof(EventViewModel.VenueId));
            }

            return venue;
        }
    }
}
