using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public sealed class EventCoverServiceTests
{
    private const string OldCover = "uploads/events/ffffffffffffffffffffffffffffffff.jpg";

    [Fact]
    public async Task UploadLocksTypeVenueEventCommitsAuditThenRemovesPreviousCover()
    {
        var harness = new Harness(EventStatusCode.Draft, OldCover);
        harness.EventType.IsActive = false;
        harness.Venue.IsActive = false;

        await harness.Service.UploadCoverAsync(15, new MemoryStream(ImageBytes()), ".jpg", TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "type", "venue", "event" }, harness.Locks);
        Assert.StartsWith("uploads/events/", harness.Events.Item.ImagePath, StringComparison.Ordinal);
        Assert.Equal(12, harness.Events.Item.UpdatedBy);
        Assert.Equal(harness.Clock.Now.UtcDateTime, harness.Events.Item.UpdatedAt);
        Assert.Contains(OldCover, harness.Files.Deleted);
        var save = Assert.Single(harness.Files.Saves);
        Assert.False(save.InTransaction);
        Assert.Contains(harness.Events.Item.ImagePath, harness.Files.StoredPaths);
        var oldFileDeletion = Assert.Single(harness.Files.DeleteAttempts, attempt => attempt.Path == OldCover);
        Assert.False(oldFileDeletion.InTransaction);
        Assert.Equal(1, oldFileDeletion.CommitCount);
        Assert.DoesNotContain(OldCover, harness.Files.StoredPaths);
        var audit = Assert.Single(harness.Audit.Entries);
        Assert.Equal(AuditActions.EventUpdated, audit.Action);
        Assert.Equal(15, audit.EntityId);
        Assert.DoesNotContain("uploads/", JsonSerializer.Serialize(audit.OldValues, audit.OldValues.GetType()), StringComparison.Ordinal);
        Assert.DoesNotContain("uploads/", JsonSerializer.Serialize(audit.NewValues, audit.NewValues.GetType()), StringComparison.Ordinal);
        Assert.Contains("\"HasCover\":true", JsonSerializer.Serialize(audit.NewValues, audit.NewValues.GetType()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftRemovalDeletesTheOldFileOnlyAfterTheRowAndAuditCommit()
    {
        var harness = new Harness(EventStatusCode.Draft, OldCover);

        await harness.Service.RemoveDraftCoverAsync(15, TestContext.Current.CancellationToken);

        Assert.Null(harness.Events.Item.ImagePath);
        Assert.Equal(12, harness.Events.Item.UpdatedBy);
        Assert.Single(harness.Audit.Entries);
        var deletion = Assert.Single(harness.Files.DeleteAttempts);
        Assert.Equal(OldCover, deletion.Path);
        Assert.False(deletion.InTransaction);
        Assert.Equal(1, deletion.CommitCount);
        Assert.DoesNotContain(OldCover, harness.Files.StoredPaths);
    }

    [Fact]
    public async Task DraftRemovalRollbackRetainsTheRowFileAndAuditState()
    {
        var harness = new Harness(EventStatusCode.Draft, OldCover) { UnitOfWork = { FailSave = true } };

        await Assert.ThrowsAsync<IOException>(() =>
            harness.Service.RemoveDraftCoverAsync(15, TestContext.Current.CancellationToken));

        Assert.Equal(OldCover, harness.Events.Item.ImagePath);
        Assert.Null(harness.Events.Item.UpdatedBy);
        Assert.Empty(harness.Audit.Entries);
        Assert.Empty(harness.Files.DeleteAttempts);
        Assert.Contains(OldCover, harness.Files.StoredPaths);
        Assert.Equal(0, harness.UnitOfWork.CommitCount);
    }

    [Theory]
    [InlineData(EventStatusCode.Published)]
    [InlineData(EventStatusCode.Completed)]
    [InlineData(EventStatusCode.Cancelled)]
    public async Task DraftCoverRemovalRejectsNonDraftEventsWithoutChangingRow(EventStatusCode status)
    {
        var harness = new Harness(status, OldCover);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.RemoveDraftCoverAsync(15, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(OldCover, harness.Events.Item.ImagePath);
        Assert.Empty(harness.Audit.Entries);
        Assert.Empty(harness.Files.Deleted);
    }

    [Theory]
    [InlineData(EventStatusCode.Completed)]
    [InlineData(EventStatusCode.Cancelled)]
    public async Task UploadToReadOnlyEventCleansStagedFileAndDoesNotChangeRow(EventStatusCode status)
    {
        var harness = new Harness(status, OldCover);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.UploadCoverAsync(15, new MemoryStream(ImageBytes()), ".jpg", TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(OldCover, harness.Events.Item.ImagePath);
        Assert.Empty(harness.Audit.Entries);
        Assert.Single(harness.Files.Deleted);
        Assert.StartsWith("uploads/events/", harness.Files.Deleted[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task PersistenceFailureRestoresOldRowAndCleansOnlyTheNewFile()
    {
        var harness = new Harness(EventStatusCode.Published, OldCover) { UnitOfWork = { FailSave = true } };

        await Assert.ThrowsAsync<IOException>(() =>
            harness.Service.UploadCoverAsync(15, new MemoryStream(ImageBytes()), ".png", TestContext.Current.CancellationToken));

        Assert.Equal(OldCover, harness.Events.Item.ImagePath);
        Assert.Null(harness.Events.Item.UpdatedBy);
        Assert.Empty(harness.Audit.Entries);
        Assert.Single(harness.Files.Deleted);
        Assert.NotEqual(OldCover, harness.Files.Deleted[0]);
        Assert.Contains(OldCover, harness.Files.StoredPaths);
        Assert.DoesNotContain(harness.Files.Deleted[0], harness.Files.StoredPaths);
        var cleanup = Assert.Single(harness.Files.DeleteAttempts);
        Assert.False(cleanup.InTransaction);
        Assert.Equal(0, cleanup.CommitCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaleCoverStatusOrReferenceCleansStagedFileWithoutAudit(bool statusChange)
    {
        var harness = new Harness(EventStatusCode.Published, OldCover);
        harness.Events.BeforeLock = item =>
        {
            if (statusChange) item.EventStatusId = EventStatusCode.Completed;
            else item.VenueId++;
        };
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.UploadCoverAsync(15, new MemoryStream(ImageBytes()), ".jpg", TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.Stale, error.Outcome);
        Assert.Equal(OldCover, harness.Events.Item.ImagePath);
        Assert.Empty(harness.Audit.Entries);
        Assert.Single(harness.Files.Deleted);
        Assert.Contains(OldCover, harness.Files.StoredPaths);
    }

    [Fact]
    public async Task PostCommitOldFileCleanupFailureDoesNotUndoSuccessfulReplacement()
    {
        var harness = new Harness(EventStatusCode.Published, OldCover);
        harness.Files.FailDeletePath = OldCover;

        await harness.Service.UploadCoverAsync(15, new MemoryStream(ImageBytes()), ".png", TestContext.Current.CancellationToken);

        Assert.NotEqual(OldCover, harness.Events.Item.ImagePath);
        Assert.Single(harness.Audit.Entries);
        Assert.Empty(harness.Files.Deleted);
        var failedCleanup = Assert.Single(harness.Files.DeleteAttempts);
        Assert.Equal(OldCover, failedCleanup.Path);
        Assert.False(failedCleanup.InTransaction);
        Assert.Equal(1, failedCleanup.CommitCount);
        Assert.Contains(OldCover, harness.Files.StoredPaths);
    }

    [Fact]
    public async Task MemberCanReadOnlyPublishedCoversAndMissingOrDraftCoversAreNotFound()
    {
        var harness = new Harness(EventStatusCode.Published, "uploads/events/0123456789abcdef0123456789abcdef.jpg", role: RoleCodes.Member);
        harness.Files.ReadBytes = ImageBytes();

        var cover = await harness.Service.OpenCoverAsync(15, TestContext.Current.CancellationToken);
        Assert.Equal("image/jpeg", cover.ContentType);
        Assert.Equal(ImageBytes(), await ReadAllAsync(cover.Content));

        harness.Events.Item.EventStatusId = EventStatusCode.Draft;
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.OpenCoverAsync(15, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
    }

    [Fact]
    public async Task MemberCanOpenACompletedEventCoverAndStillCannotOpenADraftCover()
    {
        var harness = new Harness(EventStatusCode.Completed, "uploads/events/0123456789abcdef0123456789abcdef.jpg", role: RoleCodes.Member);
        harness.Files.ReadBytes = ImageBytes();

        var cover = await harness.Service.OpenCoverAsync(15, TestContext.Current.CancellationToken);
        Assert.Equal("image/jpeg", cover.ContentType);
        Assert.Equal(ImageBytes(), await ReadAllAsync(cover.Content));

        harness.Events.Item.EventStatusId = EventStatusCode.Draft;
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.OpenCoverAsync(15, TestContext.Current.CancellationToken));
        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
    }

    [Fact]
    public async Task UnsupportedRoleCannotOpenAnEventCover()
    {
        var harness = new Harness(EventStatusCode.Published, OldCover, role: "auditor");

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.OpenCoverAsync(15, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.Forbidden, error.Outcome);
    }

    [Fact]
    public async Task MissingStoredFileIsReturnedAsNotFound()
    {
        var harness = new Harness(EventStatusCode.Published, OldCover);
        harness.Files.FailRead = true;

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.OpenCoverAsync(15, TestContext.Current.CancellationToken));

        Assert.Equal(OutcomeCode.NotFound, error.Outcome);
    }

    private static byte[] ImageBytes() => [0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0];

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using (stream)
        using (var output = new MemoryStream())
        {
            await stream.CopyToAsync(output);
            return output.ToArray();
        }
    }

    private sealed class Harness
    {
        public Harness(EventStatusCode status, string path, string role = RoleCodes.Officer)
        {
            EventType = new EventType { TypeId = 2, TypeName = "Inactive type", IsActive = true };
            Venue = new Venue { VenueId = 3, RoomName = "Inactive venue", Capacity = 20, IsActive = true };
            Events = new MemoryEvents(this)
            {
                Item = new Event
                {
                    EventId = 15,
                    Title = "Cover event",
                    TypeId = EventType.TypeId,
                    VenueId = Venue.VenueId,
                    EventStatusId = status,
                    ImagePath = path
                }
            };
            Audit = new RecordingAudit();
            Files = new MemoryFiles(path);
            Clock = new FixedClock();
            UnitOfWork = new TransactionUnitOfWork(Events, Audit);
            Files.UnitOfWork = UnitOfWork;
            var actor = new FixedActor(role);
            Service = new EventLifecycleService(
                Events,
                new MemoryEventTypes(this),
                new MemoryVenues(this),
                new UnusedRegistrations(),
                new UnusedCancellations(),
                Array.Empty<IEventCompletionHandler>(),
                UnitOfWork,
                new MapperConfiguration(_ => { }).CreateMapper(),
                Clock,
                Audit,
                actor,
                Files,
                NullLogger<EventLifecycleService>.Instance);
        }

        public EventType EventType { get; }
        public Venue Venue { get; }
        public MemoryEvents Events { get; }
        public RecordingAudit Audit { get; }
        public MemoryFiles Files { get; }
        public FixedClock Clock { get; }
        public TransactionUnitOfWork UnitOfWork { get; }
        public EventLifecycleService Service { get; }
        public List<string> Locks { get; } = [];
    }

    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; } = new(2026, 11, 6, 12, 30, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FixedActor(string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => 12;
        public string Role => role;
    }

    private sealed class RecordingAudit : IActivityLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class MemoryFiles : IPrivateFileStore
    {
        public MemoryFiles(string existingPath) => StoredPaths.Add(existingPath);

        public List<string> Deleted { get; } = [];
        public List<(string Path, bool InTransaction, int CommitCount)> DeleteAttempts { get; } = [];
        public List<(string Path, bool InTransaction)> Saves { get; } = [];
        public HashSet<string> StoredPaths { get; } = [];
        public TransactionUnitOfWork UnitOfWork { get; set; } = null!;
        public string? FailDeletePath { get; set; }
        public bool FailRead { get; set; }
        public byte[] ReadBytes { get; set; } = [];

        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default)
        {
            Assert.Equal(PrivateFileArea.EventImages, area);
            Assert.False(UnitOfWork.IsInTransaction, "New cover files must be saved before database locks are taken.");
            var path = $"uploads/events/{Guid.NewGuid():N}{extension}";
            Saves.Add((path, UnitOfWork.IsInTransaction));
            StoredPaths.Add(path);
            return Task.FromResult(path);
        }

        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();
        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) =>
            FailRead || !StoredPaths.Contains(relativePath)
                ? Task.FromException<Stream>(new FileNotFoundException())
                : Task.FromResult<Stream>(new MemoryStream(ReadBytes));

        public void Delete(string relativePath)
        {
            DeleteAttempts.Add((relativePath, UnitOfWork.IsInTransaction, UnitOfWork.CommitCount));
            Assert.False(UnitOfWork.IsInTransaction, "Cover files must not be deleted while the database transaction is open.");
            if (relativePath == FailDeletePath)
            {
                throw new IOException("simulated cleanup failure");
            }

            Deleted.Add(relativePath);
            StoredPaths.Remove(relativePath);
        }
    }

    private sealed class MemoryEvents(Harness harness) : IEventRepository
    {
        public Event Item { get; set; } = null!;
        public Action<Event>? BeforeLock { get; set; }
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EventListItem>>([]);
        public Task<Event> GetEventAsync(int eventId, CancellationToken cancellationToken = default) => Task.FromResult(eventId == Item.EventId ? new Event
        {
            EventId = Item.EventId, Title = Item.Title, TypeId = Item.TypeId, VenueId = Item.VenueId,
            EventStatusId = Item.EventStatusId, ImagePath = Item.ImagePath
        } : null!);
        public Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            harness.Locks.Add("event");
            BeforeLock?.Invoke(Item);
            return Task.FromResult(eventId == Item.EventId ? Item : null!);
        }
        public Task AddEventAsync(Event eventItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateEventAsync(Event eventItem, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublishedEventListItem>> GetPublishedPageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublishedEventDetail> GetPublishedEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMemberVisibleAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublishedEventListItem>> GetMemberVisiblePageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublishedEventDetail> GetMemberVisibleEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MemoryEventTypes(Harness harness) : IEventTypeRepository
    {
        public Task<IReadOnlyList<EventType>> GetActiveEventTypesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountEventTypesAsync(string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<EventType>> GetEventTypesPageAsync(string status, int offset, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public EventType GetEventType(int typeId) => harness.EventType;
        public Task<EventType> LockEventTypeAsync(int typeId, CancellationToken cancellationToken = default)
        {
            harness.Locks.Add("type");
            return Task.FromResult(typeId == harness.EventType.TypeId ? harness.EventType : null!);
        }
        public Task<int> CountLinkedEventsAsync(int typeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public bool EventTypeExists(int typeId) => typeId == harness.EventType.TypeId;
        public bool TypeNameExists(string typeName, int? excludeId = null) => false;
    }

    private sealed class MemoryVenues(Harness harness) : IVenueRepository
    {
        public IQueryable<Venue> GetVenues() => new[] { harness.Venue }.AsQueryable();
        public Task<IReadOnlyList<Venue>> GetActiveVenuesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountAsync(string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Venue>> GetPageAsync(string status, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Venue GetVenue(int venueId) => harness.Venue;
        public Task<Venue> LockVenueAsync(int venueId, CancellationToken cancellationToken = default)
        {
            harness.Locks.Add("venue");
            return Task.FromResult(venueId == harness.Venue.VenueId ? harness.Venue : null!);
        }
        public Task<int> CountLinkedEventsAsync(int venueId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasEventAboveCapacityAsync(int venueId, int capacity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddVenueAsync(Venue venue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateVenueAsync(Venue venue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public bool VenueExists(int venueId) => venueId == harness.Venue.VenueId;
    }

    private sealed class UnusedRegistrations : IRegistrationRepository
    {
        public Task<RegistrationReviewIdentity> DiscoverReviewAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasEligibleWaitlistAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Registration> GetForCancellationAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int id, DateTime now, int? eventId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsConfirmedAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountConfirmedAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountConfirmedLookupAsync(int eventId, string query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(int eventId, string query, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> GetConfirmedStudentNumberAsync(int eventId, int registrationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Registration registration, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UnusedCancellations : IRegistrationCancellationService
    {
        public Task CancelRegistrationAsync(int registrationId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelAllForEventAsync(int eventId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelUpcomingForUserAsync(int userId, CancellationCause cause, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationPreview>> PreviewUpcomingForUserAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TransactionUnitOfWork(MemoryEvents events, RecordingAudit audit) : IUnitOfWork
    {
        public bool FailSave { get; set; }
        public bool IsInTransaction { get; private set; }
        public int CommitCount { get; private set; }
        public DbContext Database => throw new NotSupportedException();
        public void SaveChanges() => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            FailSave ? Task.FromException<int>(new IOException("simulated database failure")) : Task.FromResult(1);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async ct => { await work(ct); return true; }, cancellationToken);

        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default) =>
            await ExecuteAsync(work, cancellationToken);

        private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
        {
            var oldPath = events.Item.ImagePath;
            var oldUpdatedBy = events.Item.UpdatedBy;
            var oldUpdatedAt = events.Item.UpdatedAt;
            var auditCount = audit.Entries.Count;
            IsInTransaction = true;
            try
            {
                var result = await work(cancellationToken);
                CommitCount++;
                return result;
            }
            catch
            {
                events.Item.ImagePath = oldPath;
                events.Item.UpdatedBy = oldUpdatedBy;
                events.Item.UpdatedAt = oldUpdatedAt;
                audit.Entries.RemoveRange(auditCount, audit.Entries.Count - auditCount);
                throw;
            }
            finally
            {
                IsInTransaction = false;
            }
        }
    }
}
