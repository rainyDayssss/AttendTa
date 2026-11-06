using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.Services.Services
{
    public class VenueService : IVenueService
    {
        private readonly IVenueRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly IMapper _mapper;
        private readonly IPrivateFileStore _files;
        private readonly IActivityLogWriter _audit;
        private readonly ICurrentActor _actor;
        private readonly ILogger<VenueService> _logger;

        public VenueService(IVenueRepository repository, IUnitOfWork unitOfWork, IMapper mapper, TimeProvider timeProvider, IPrivateFileStore files, IActivityLogWriter audit, ICurrentActor actor, ILogger<VenueService> logger)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _mapper = mapper;
            _files = files;
            _audit = audit;
            _actor = actor;
            _logger = logger;
        }

        public async Task<PagedResult<VenueViewModel>> GetPageAsync(string status, int? pageNumber, CancellationToken cancellationToken = default)
        {
            var totalCount = await _repository.CountAsync(status, cancellationToken);
            var pageRequest = new PageRequest(pageNumber);
            var normalizedPage = pageRequest.NormalizePageNumber(totalCount);
            var venues = await _repository.GetPageAsync(
                status,
                pageRequest.GetOffset(normalizedPage),
                PageRequest.FixedPageSize,
                cancellationToken);
            var items = _mapper.Map<List<VenueViewModel>>(venues);
            return new PagedResult<VenueViewModel>(items, normalizedPage, totalCount);
        }

        public VenueViewModel GetVenue(int id)
        {
            var venue = _repository.GetVenue(id);
            return venue == null ? null : _mapper.Map<VenueViewModel>(venue);
        }

        private sealed record VenueAuditValues(string RoomName, int Capacity, string Location, string AvailabilityNotes, string ContactPerson, bool PhotoChanged) : IAuditValues;

        private sealed record VenueStatusAuditValues(bool IsActive, int LinkedEventCount) : IAuditValues;

        private static VenueAuditValues Snapshot(Venue venue, bool photoChanged) =>
            new(venue.RoomName, venue.Capacity, venue.Location, venue.AvailabilityNotes, venue.ContactPerson, photoChanged);

        private static bool IsVenuePhoto(string key) =>
            !string.IsNullOrWhiteSpace(key) && key.StartsWith("uploads/venues/", StringComparison.Ordinal) &&
            key.Length > "uploads/venues/".Length && key.IndexOfAny(new[] { '/', '\\' }, "uploads/venues/".Length) < 0 &&
            key != "uploads/venues/..";

        private int RequireManagementActor(string message = "A valid authenticated user is required.")
        {
            if (!_actor.IsAuthenticated || (_actor.Role != RoleCodes.Admin && _actor.Role != RoleCodes.Officer))
                throw new DomainException(OutcomeCode.Forbidden, message);
            int id;
            try { id = _actor.UserId; }
            catch (InvalidOperationException) { throw new DomainException(OutcomeCode.Forbidden, message); }
            if (id <= 0) throw new DomainException(OutcomeCode.Forbidden, message);
            return id;
        }

        private void ValidateWrite(VenueViewModel model)
        {
            if (model.Capacity <= 0)
                throw new DomainException(OutcomeCode.Invalid, "Capacity must be greater than 0.", nameof(VenueViewModel.Capacity));
            if (_unitOfWork.Database.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null)
                throw new InvalidOperationException("Venue writes require ownership of the transaction.");
        }

        private void DeleteBestEffort(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            try { _files.Delete(key); }
            catch (Exception) { _logger.LogWarning("Could not clean up a venue photo."); }
        }

        public async Task AddVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default)
        {
            var actorId = RequireManagementActor();
            ValidateWrite(model);
            if (photo == null) throw new DomainException(OutcomeCode.Invalid, "A venue photo is required.", "photo");
            string newPath = null;
            try
            {
                newPath = await _files.SaveImageAsync(PrivateFileArea.VenueImages, photo, extension, "photo", cancellationToken);
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var venue = new Venue
                    {
                        RoomName = model.RoomName?.Trim(),
                        Capacity = model.Capacity,
                        Location = model.Location,
                        AvailabilityNotes = model.AvailabilityNotes,
                        ContactPerson = model.ContactPerson,
                        ImagePath = newPath,
                        IsActive = true,
                        CreatedBy = actorId,
                        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                    };
                    await _repository.AddVenueAsync(venue, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    _audit.Append(new AuditEntry(AuditActions.VenueCreated, "venues", venue.VenueId, NewValues: Snapshot(venue, true)));
                    await _unitOfWork.SaveChangesAsync(ct);
                }, cancellationToken);
            }
            catch { DeleteBestEffort(newPath); throw; }
        }

        public async Task UpdateVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default)
        {
            var actorId = RequireManagementActor();
            ValidateWrite(model);
            string newPath = null;
            string oldPath = null;
            try
            {
                if (photo != null) newPath = await _files.SaveImageAsync(PrivateFileArea.VenueImages, photo, extension, "photo", cancellationToken);
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var venue = await _repository.LockVenueAsync(model.VenueId, ct);
                    if (venue == null) throw new DomainException(OutcomeCode.NotFound, "Venue not found.");
                    if (await _repository.HasEventAboveCapacityAsync(venue.VenueId, model.Capacity, ct))
                        throw new DomainException(OutcomeCode.Invalid, "Capacity cannot be below the participant limit of a draft or published event.", nameof(VenueViewModel.Capacity));
                    oldPath = venue.ImagePath;
                    if (!IsVenuePhoto(oldPath) && newPath == null)
                        throw new DomainException(OutcomeCode.Invalid, "A venue photo is required.", "photo");
                    var oldValues = Snapshot(venue, newPath != null);
                    venue.RoomName = model.RoomName?.Trim();
                    venue.Capacity = model.Capacity;
                    venue.Location = model.Location;
                    venue.AvailabilityNotes = model.AvailabilityNotes;
                    venue.ContactPerson = model.ContactPerson;
                    if (newPath != null) venue.ImagePath = newPath;
                    venue.UpdatedBy = actorId;
                    venue.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await _repository.UpdateVenueAsync(venue, ct);
                    _audit.Append(new AuditEntry(AuditActions.VenueEdited, "venues", venue.VenueId, OldValues: oldValues, NewValues: Snapshot(venue, newPath != null)));
                    await _unitOfWork.SaveChangesAsync(ct);
                }, cancellationToken);
            }
            catch { DeleteBestEffort(newPath); throw; }
            if (newPath != null && IsVenuePhoto(oldPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) DeleteBestEffort(oldPath);
        }

        public async Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default)
        {
            var actorId = RequireManagementActor("A valid authenticated user is required to update a venue.");

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var venue = await _repository.LockVenueAsync(id, ct);
                if (venue == null)
                {
                    throw new DomainException(OutcomeCode.NotFound, "Venue not found.");
                }

                var linkedEventCount = await _repository.CountLinkedEventsAsync(venue.VenueId, ct);
                var oldValues = new VenueStatusAuditValues(venue.IsActive, linkedEventCount);
                venue.IsActive = !venue.IsActive;
                venue.UpdatedBy = actorId;
                venue.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await _repository.UpdateVenueAsync(venue, ct);
                _audit.Append(new AuditEntry(AuditActions.VenueStatusChanged, "venues", venue.VenueId,
                    OldValues: oldValues, NewValues: new VenueStatusAuditValues(venue.IsActive, linkedEventCount)));
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);
        }
    }
}
