using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Controllers;
using ASI.Basecode.WebApp.Storage;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public class MasterDataTransactionTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly IMapper Mapper = new MapperConfiguration(config =>
    {
        config.CreateMap<EventTypeViewModel, EventType>();
        config.CreateMap<EventType, EventTypeViewModel>();
        config.CreateMap<VenueViewModel, Venue>();
        config.CreateMap<Venue, VenueViewModel>();
    }).CreateMapper();

    private AsiBasecodeDBContext Context() => new(new DbContextOptionsBuilder<AsiBasecodeDBContext>().UseSqlServer(fixture.ConnectionString).Options);

    private static VenueService VenueWriter(IVenueRepository repository, UnitOfWork work, int actorId, RecordingVenueImageStorage? storage = null)
    {
        var actor = new TestActor(actorId);
        return new VenueService(repository, work, Mapper, TimeProvider.System, storage ?? new RecordingVenueImageStorage("uploads/venues/test.jpg"),
            new ActivityLogWriter((AsiBasecodeDBContext)work.Database, actor, TimeProvider.System), actor, NullLogger<VenueService>.Instance);
    }

    private static EventTypeService EventTypeWriter(IEventTypeRepository repository, UnitOfWork work, int actorId)
    {
        var actor = new TestActor(actorId);
        return new EventTypeService(repository, work, Mapper, TimeProvider.System,
            new ActivityLogWriter((AsiBasecodeDBContext)work.Database, actor, TimeProvider.System), actor);
    }

    private static MemberService MemberWriter(IMemberRepository repository, UnitOfWork work, int actorId)
    {
        return MemberWriter(repository, work, new TestActor(actorId));
    }

    private static MemberService MemberWriter(IMemberRepository repository, UnitOfWork work, ICurrentActor actor)
    {
        return new MemberService(repository, new UserRepository(work), work, TimeProvider.System, actor,
            new ActivityLogWriter((AsiBasecodeDBContext)work.Database, actor, TimeProvider.System));
    }

    private sealed class TestActor(int id, bool authenticated = true, string role = "admin") : ICurrentActor
    {
        public bool IsAuthenticated => authenticated;
        public int UserId => id;
        public string Role => role;
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static User NewUser() => new()
    {
        Name = "Master Data Actor",
        UserId = $"{Guid.NewGuid():N}@example.invalid",
        Password = "test",
        IsActive = true,
        RoleId = RoleCode.Admin
    };

    private static EventTypeViewModel EventTypeModel(string name) => new() { TypeName = name, Description = "Description" };

    private static VenueViewModel VenueModel(string roomName, string imagePath = "venues/test.jpg") => new()
    {
        RoomName = roomName,
        Capacity = 20,
        Location = "Main Building",
        ImagePath = imagePath
    };

    private static MemberViewModel MemberModel(string studentNumber) => new()
    {
        FullName = "Linked Member",
        StudentNumber = studentNumber,
        Course = "Computing",
        YearLevel = 1,
        Section = "A",
        MobileNumber = "555-0100",
        MembershipType = "Regular",
        Status = "active"
    };

    private async Task<int> SeedUserAsync(CancellationToken cancellationToken)
    {
        await using var context = Context();
        var user = NewUser();
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    private async Task<(int ActorId, int LinkedUserId)> SeedUsersAsync(CancellationToken cancellationToken)
    {
        await using var context = Context();
        var actor = NewUser();
        var linked = NewUser();
        context.Users.AddRange(actor, linked);
        await context.SaveChangesAsync(cancellationToken);
        return (actor.Id, linked.Id);
    }

    [Fact]
    public async Task EventTypeWriteRollsBackWhenTheRepositorySavesAndThenFails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var actorId = await SeedUserAsync(ct);
        var service = EventTypeWriter(new EventTypeRepositoryThatSavesThenFails(new EventTypeRepository(unitOfWork), unitOfWork), unitOfWork, actorId);
        var name = UniqueName("Type");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddEventTypeAsync(EventTypeModel(name), ct));

        Assert.Empty(writer.ChangeTracker.Entries());
        Assert.False(await observer.EventTypes.AnyAsync(t => t.TypeName == name, ct));
    }

    [Fact]
    public async Task EventTypeWriteCommitsWithoutAnOuterTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var actorId = await SeedUserAsync(ct);
        var service = EventTypeWriter(new EventTypeRepository(unitOfWork), unitOfWork, actorId);
        var name = UniqueName("Type");

        await service.AddEventTypeAsync(EventTypeModel(name), ct);

        Assert.True(await observer.EventTypes.AnyAsync(t => t.TypeName == name, ct));
    }

    [Fact]
    public async Task EventTypeWriteJoinsAnOuterTransactionThatRollsBack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var actorId = await SeedUserAsync(ct);
        var service = EventTypeWriter(new EventTypeRepository(unitOfWork), unitOfWork, actorId);
        var name = UniqueName("Type");

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            await service.AddEventTypeAsync(EventTypeModel(name), inner);
            throw new InvalidOperationException("The outer work failed.");
        }, ct));

        Assert.False(await observer.EventTypes.AnyAsync(t => t.TypeName == name, ct));
    }

    [Fact]
    public async Task VenueWriteRollsBackWhenTheRepositorySavesAndThenFails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var actorId = await SeedUserAsync(ct);
        var service = VenueWriter(new VenueRepositoryThatSavesThenFails(new VenueRepository(unitOfWork), unitOfWork), unitOfWork, actorId);
        var roomName = UniqueName("Room");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddVenueAsync(VenueModel(roomName), new MemoryStream(), ".jpg", ct));

        Assert.Empty(writer.ChangeTracker.Entries());
        Assert.False(await observer.Venues.AnyAsync(v => v.RoomName == roomName, ct));
    }

    [Fact]
    public async Task VenueWriteCommitsWithoutAnOuterTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var actorId = await SeedUserAsync(ct);
        var service = VenueWriter(new VenueRepository(unitOfWork), unitOfWork, actorId);
        var roomName = UniqueName("Room");

        await service.AddVenueAsync(VenueModel(roomName), new MemoryStream(), ".jpg", ct);

        Assert.True(await observer.Venues.AnyAsync(v => v.RoomName == roomName, ct));
        var venue = await observer.Venues.SingleAsync(v => v.RoomName == roomName, ct);
        Assert.Equal(actorId, venue.CreatedBy);
        var audit = await observer.ActivityLogs.SingleAsync(a => a.EntityType == "venues" && a.EntityId == venue.VenueId, ct);
        Assert.DoesNotContain("uploads/", audit.NewValues, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VenueCapacityChecksOnlyActiveEventLimitsAndPreservesPhoto()
    {
        var ct = TestContext.Current.CancellationToken;
        var actorId = await SeedUserAsync(ct);
        await using var writer = Context();
        var unit = new UnitOfWork(writer);
        var service = VenueWriter(new VenueRepository(unit), unit, actorId);
        var type = new EventType { TypeName = UniqueName("Event"), Description = "test", IsActive = true, CreatedBy = actorId, CreatedAt = DateTime.UtcNow };
        var venue = new Venue { RoomName = UniqueName("Room"), Capacity = 40, ImagePath = "uploads/venues/old.jpg", IsActive = true, CreatedBy = actorId, CreatedAt = DateTime.UtcNow };
        writer.EventTypes.Add(type);
        writer.Venues.Add(venue);
        await writer.SaveChangesAsync(ct);
        writer.Set<Event>().Add(new Event { Title = "Published", TypeId = type.TypeId, VenueId = venue.VenueId, StartsAt = DateTime.UtcNow.AddDays(2), RegistrationDeadline = DateTime.UtcNow.AddDays(1), ParticipantLimit = 30, EventStatusId = EventStatusCode.Published, CreatedBy = actorId, CreatedAt = DateTime.UtcNow });
        writer.Set<Event>().Add(new Event { Title = "Cancelled", TypeId = type.TypeId, VenueId = venue.VenueId, StartsAt = DateTime.UtcNow.AddDays(2), RegistrationDeadline = DateTime.UtcNow.AddDays(1), ParticipantLimit = 50, EventStatusId = EventStatusCode.Cancelled, CreatedBy = actorId, CreatedAt = DateTime.UtcNow });
        writer.Set<Event>().Add(new Event { Title = "Completed", TypeId = type.TypeId, VenueId = venue.VenueId, StartsAt = DateTime.UtcNow.AddDays(2), RegistrationDeadline = DateTime.UtcNow.AddDays(1), ParticipantLimit = 60, EventStatusId = EventStatusCode.Completed, CreatedBy = actorId, CreatedAt = DateTime.UtcNow });
        await writer.SaveChangesAsync(ct);
        var model = new VenueViewModel { VenueId = venue.VenueId, RoomName = venue.RoomName, Capacity = 29 };
        var error = await Assert.ThrowsAsync<DomainException>(() => service.UpdateVenueAsync(model, null, null, ct));
        Assert.Equal(OutcomeCode.Invalid, error.Outcome);
        Assert.Equal(nameof(VenueViewModel.Capacity), error.Field);
        await using var observer = Context();
        Assert.Equal(40, (await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venue.VenueId, ct)).Capacity);
        Assert.False(await observer.ActivityLogs.AnyAsync(a => a.EntityType == "venues" && a.EntityId == venue.VenueId, ct));
        model.Capacity = 30;
        await service.UpdateVenueAsync(model, null, null, ct);
        var updated = await observer.Venues.AsNoTracking().SingleAsync(v => v.VenueId == venue.VenueId, ct);
        Assert.Equal(30, updated.Capacity);
        Assert.Equal("uploads/venues/old.jpg", updated.ImagePath);
        var log = await observer.ActivityLogs.SingleAsync(a => a.EntityType == "venues" && a.EntityId == venue.VenueId, ct);
        Assert.DoesNotContain("uploads/", log.NewValues, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("uploads/", log.OldValues, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VenueWriteRejectsAnOuterTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var actorId = await SeedUserAsync(ct);
        var storage = new RecordingVenueImageStorage("uploads/venues/test.jpg");
        var service = VenueWriter(new VenueRepository(unitOfWork), unitOfWork, actorId, storage);
        var roomName = UniqueName("Room");

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            await service.AddVenueAsync(VenueModel(roomName), new MemoryStream(), ".jpg", inner);
            throw new InvalidOperationException("The outer work failed.");
        }, ct));

        Assert.False(await observer.Venues.AnyAsync(v => v.RoomName == roomName, ct));
        Assert.Equal(0, storage.Saves);
    }

    [Fact]
    public async Task MemberWriteRollsBackWhenTheRepositorySavesAndThenFails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var (actorId, linkedUserId) = await SeedUsersAsync(ct);
        var service = MemberWriter(new MemberRepositoryThatSavesThenFails(new MemberRepository(unitOfWork), unitOfWork), unitOfWork, actorId);
        var studentNumber = UniqueName("SN");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMemberFromApprovedAccountRequestAsync(linkedUserId, MemberModel(studentNumber), ct));

        Assert.Empty(writer.ChangeTracker.Entries());
        Assert.False(await observer.Members.AnyAsync(m => m.StudentNumber == studentNumber, ct));
    }

    [Fact]
    public async Task MemberWriteCommitsWithoutAnOuterTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var (actorId, linkedUserId) = await SeedUsersAsync(ct);
        var service = MemberWriter(new MemberRepository(unitOfWork), unitOfWork, actorId);
        var studentNumber = UniqueName("SN");

        await service.CreateMemberFromApprovedAccountRequestAsync(linkedUserId, MemberModel(studentNumber), ct);

        Assert.True(await observer.Members.AnyAsync(m => m.StudentNumber == studentNumber, ct));
    }

    [Fact]
    public async Task MemberWriteJoinsAnOuterTransactionThatRollsBack()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var (actorId, linkedUserId) = await SeedUsersAsync(ct);
        var service = MemberWriter(new MemberRepository(unitOfWork), unitOfWork, actorId);
        var studentNumber = UniqueName("SN");

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(async inner =>
        {
            await service.CreateMemberFromApprovedAccountRequestAsync(linkedUserId, MemberModel(studentNumber), inner);
            throw new InvalidOperationException("The outer work failed.");
        }, ct));

        Assert.False(await observer.Members.AnyAsync(m => m.StudentNumber == studentNumber, ct));
    }

    [Fact]
    public async Task EventTypeWritesUseTheSharedOutcomeContract()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var actorId = await SeedUserAsync(ct);
        var service = EventTypeWriter(new EventTypeRepository(unitOfWork), unitOfWork, actorId);
        var anonymous = EventTypeWriter(new EventTypeRepository(unitOfWork), unitOfWork, 0);
        var name = UniqueName("Type");

        Assert.Equal(OutcomeCode.Forbidden, (await Assert.ThrowsAsync<DomainException>(() => anonymous.AddEventTypeAsync(EventTypeModel(name), ct))).Outcome);
        Assert.Equal(OutcomeCode.Invalid, (await Assert.ThrowsAsync<DomainException>(() => service.AddEventTypeAsync(EventTypeModel("   "), ct))).Outcome);
        Assert.Equal(OutcomeCode.Invalid, (await Assert.ThrowsAsync<DomainException>(() => service.AddEventTypeAsync(new EventTypeViewModel { TypeName = name, Description = "  " }, ct))).Outcome);
        Assert.Equal(OutcomeCode.NotFound, (await Assert.ThrowsAsync<DomainException>(() => service.UpdateEventTypeAsync(new EventTypeViewModel { TypeId = int.MaxValue, TypeName = name, Description = "Description" }, ct))).Outcome);
        Assert.Equal(OutcomeCode.NotFound, (await Assert.ThrowsAsync<DomainException>(() => service.ToggleActiveAsync(int.MaxValue, ct))).Outcome);
        Assert.False(await observer.EventTypes.AnyAsync(t => t.TypeName == name, ct));

        await service.AddEventTypeAsync(EventTypeModel(name), ct);
        var duplicate = await Assert.ThrowsAsync<DomainException>(() => service.AddEventTypeAsync(EventTypeModel(name), ct));
        Assert.Equal(OutcomeCode.Duplicate, duplicate.Outcome);
        Assert.Equal(1, await observer.EventTypes.CountAsync(t => t.TypeName == name, ct));
    }

    [Fact]
    public async Task VenueWritesUseTheSharedOutcomeContract()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var actorId = await SeedUserAsync(ct);
        var service = VenueWriter(new VenueRepository(unitOfWork), unitOfWork, actorId);
        var roomName = UniqueName("Room");

        var invalidActorService = VenueWriter(new VenueRepository(unitOfWork), unitOfWork, 0);
        Assert.Equal(OutcomeCode.Forbidden, (await Assert.ThrowsAsync<DomainException>(() => invalidActorService.AddVenueAsync(VenueModel(roomName), null, null, ct))).Outcome);
        var missingPhoto = await Assert.ThrowsAsync<DomainException>(() => service.AddVenueAsync(VenueModel(roomName, "  "), null, null, ct));
        Assert.Equal(OutcomeCode.Invalid, missingPhoto.Outcome);
        Assert.Equal("photo", missingPhoto.Field);
        Assert.Equal(OutcomeCode.NotFound, (await Assert.ThrowsAsync<DomainException>(() => service.UpdateVenueAsync(new VenueViewModel { VenueId = int.MaxValue, RoomName = roomName, Capacity = 20, ImagePath = "venues/test.jpg" }, null, null, ct))).Outcome);
        Assert.Equal(OutcomeCode.NotFound, (await Assert.ThrowsAsync<DomainException>(() => service.ToggleActiveAsync(int.MaxValue, ct))).Outcome);
        Assert.False(await observer.Venues.AnyAsync(v => v.RoomName == roomName, ct));
    }

    [Fact]
    public async Task MemberWritesUseTheSharedOutcomeContract()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var writer = Context();
        await using var observer = Context();
        var unitOfWork = new UnitOfWork(writer);
        var (actorId, linkedUserId) = await SeedUsersAsync(ct);
        var service = MemberWriter(new MemberRepository(unitOfWork), unitOfWork, actorId);
        var studentNumber = UniqueName("SN");

        var unauthenticated = MemberWriter(new MemberRepository(unitOfWork), unitOfWork, new TestActor(actorId, authenticated: false));
        Assert.Equal(OutcomeCode.Forbidden, (await Assert.ThrowsAsync<DomainException>(() => unauthenticated.CreateMemberFromApprovedAccountRequestAsync(linkedUserId, MemberModel(studentNumber), ct))).Outcome);
        Assert.Equal(OutcomeCode.Invalid, (await Assert.ThrowsAsync<DomainException>(() => service.CreateMemberFromApprovedAccountRequestAsync(0, MemberModel(studentNumber), ct))).Outcome);
        Assert.Equal(OutcomeCode.NotFound, (await Assert.ThrowsAsync<DomainException>(() => service.UpdateMemberAsync(new MemberViewModel { MemberId = int.MaxValue, StudentNumber = studentNumber, Course = "Computing", YearLevel = 1, Section = "A", Status = "active" }, ct))).Outcome);
        Assert.False(await observer.Members.AnyAsync(m => m.StudentNumber == studentNumber, ct));

        await service.CreateMemberFromApprovedAccountRequestAsync(linkedUserId, MemberModel(studentNumber), ct);
        var duplicateProfile = await Assert.ThrowsAsync<DomainException>(() => service.CreateMemberFromApprovedAccountRequestAsync(linkedUserId, MemberModel(UniqueName("SN")), ct));
        Assert.Equal(OutcomeCode.Duplicate, duplicateProfile.Outcome);

        var otherUserId = await SeedUserAsync(ct);
        var duplicateStudentNumber = await Assert.ThrowsAsync<DomainException>(() => service.CreateMemberFromApprovedAccountRequestAsync(otherUserId, MemberModel(studentNumber), ct));
        Assert.Equal(OutcomeCode.Duplicate, duplicateStudentNumber.Outcome);
        Assert.Equal(1, await observer.Members.CountAsync(m => m.StudentNumber == studentNumber, ct));
    }

    [Fact]
    public async Task VenueCreateLeavesPhotoHandlingToTheService()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "test"))
        };
        var storage = new RecordingVenueImageStorage("venues/saved.jpg");
        var controller = new VenueController(
            new HttpContextAccessor { HttpContext = httpContext },
            NullLoggerFactory.Instance,
            new ConfigurationBuilder().Build(),
            Mapper,
            new RefusingVenueService(),
            storage)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };

        var result = await controller.Create(VenueModel("Room"), Photo("photo.png", [0x89, 0x50, 0x4E, 0x47]));

        Assert.IsType<ViewResult>(result);
        Assert.Empty(storage.Deleted);
        Assert.Equal(0, storage.Saves);
        Assert.Equal("Refused.", controller.TempData["ErrorMessage"]);
    }

    private static IFormFile Photo(string fileName, byte[] content) => new FormFile(new MemoryStream(content), 0, content.Length, "photo", fileName);

    private sealed class RecordingVenueImageStorage(string savedPath) : IPrivateFileStore
    {
        public List<string> Deleted { get; } = [];
        public int Saves { get; private set; }

        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default) { Saves++; return Task.FromResult(savedPath); }

        public string CreateTemporaryPath(string extension) => throw new NotSupportedException();

        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Delete(string relativePath) => Deleted.Add(relativePath);
    }

    private sealed class RefusingVenueService : IVenueService
    {
        public Task<PagedResult<VenueViewModel>> GetPageAsync(string status, int? pageNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<VenueViewModel>(Array.Empty<VenueViewModel>(), 1, 0));

        public VenueViewModel GetVenue(int id) => null!;

        public Task AddVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default) =>
            throw new DomainException(OutcomeCode.Invalid, "Refused.");

        public Task UpdateVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class TestCurrentActor(int userId) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => userId;
        public string Role => "admin";
    }
    private sealed class EventTypeRepositoryThatSavesThenFails(IEventTypeRepository inner, IUnitOfWork unitOfWork) : IEventTypeRepository
    {
        public Task<IReadOnlyList<EventType>> GetActiveEventTypesAsync(CancellationToken cancellationToken = default) => inner.GetActiveEventTypesAsync(cancellationToken);
        public Task<int> CountEventTypesAsync(string status, CancellationToken cancellationToken = default) => inner.CountEventTypesAsync(status, cancellationToken);
        public Task<IReadOnlyList<EventType>> GetEventTypesPageAsync(string status, int offset, int pageSize, CancellationToken cancellationToken = default) => inner.GetEventTypesPageAsync(status, offset, pageSize, cancellationToken);
        public EventType GetEventType(int typeId) => inner.GetEventType(typeId);
        public Task<EventType> LockEventTypeAsync(int typeId, CancellationToken cancellationToken = default) => inner.LockEventTypeAsync(typeId, cancellationToken);
        public Task<int> CountLinkedEventsAsync(int typeId, CancellationToken cancellationToken = default) => inner.CountLinkedEventsAsync(typeId, cancellationToken);
        public bool EventTypeExists(int typeId) => inner.EventTypeExists(typeId);
        public bool TypeNameExists(string typeName, int? excludeId = null) => inner.TypeNameExists(typeName, excludeId);
        public Task UpdateEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default) => inner.UpdateEventTypeAsync(eventType, cancellationToken);

        public async Task AddEventTypeAsync(EventType eventType, CancellationToken cancellationToken = default)
        {
            await inner.AddEventTypeAsync(eventType, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The repository failed after saving.");
        }
    }

    private sealed class VenueRepositoryThatSavesThenFails(IVenueRepository inner, IUnitOfWork unitOfWork) : IVenueRepository
    {
        public Task<IReadOnlyList<Venue>> GetActiveVenuesAsync(CancellationToken cancellationToken = default) => inner.GetActiveVenuesAsync(cancellationToken);
        public Task<int> CountAsync(string status, CancellationToken cancellationToken = default) => inner.CountAsync(status, cancellationToken);
        public Task<IReadOnlyList<Venue>> GetPageAsync(string status, int skip, int take, CancellationToken cancellationToken = default) => inner.GetPageAsync(status, skip, take, cancellationToken);
        public Venue GetVenue(int venueId) => inner.GetVenue(venueId);
        public Task<Venue> LockVenueAsync(int venueId, CancellationToken cancellationToken = default) => inner.LockVenueAsync(venueId, cancellationToken);
        public Task<int> CountLinkedEventsAsync(int venueId, CancellationToken cancellationToken = default) => inner.CountLinkedEventsAsync(venueId, cancellationToken);
        public Task<bool> HasEventAboveCapacityAsync(int venueId, int capacity, CancellationToken cancellationToken = default) => inner.HasEventAboveCapacityAsync(venueId, capacity, cancellationToken);
        public bool VenueExists(int venueId) => inner.VenueExists(venueId);
        public Task UpdateVenueAsync(Venue venue, CancellationToken cancellationToken = default) => inner.UpdateVenueAsync(venue, cancellationToken);

        public async Task AddVenueAsync(Venue venue, CancellationToken cancellationToken = default)
        {
            await inner.AddVenueAsync(venue, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The repository failed after saving.");
        }
    }

    private sealed class MemberRepositoryThatSavesThenFails(IMemberRepository inner, IUnitOfWork unitOfWork) : IMemberRepository
    {
        public IQueryable<MemberRecord> GetMemberRecords() => inner.GetMemberRecords();
        public Task<int> CountAsync(string search, string status, CancellationToken cancellationToken = default) => inner.CountAsync(search, status, cancellationToken);
        public Task<IReadOnlyList<MemberRecord>> GetPageAsync(string search, string status, int skip, int take, CancellationToken cancellationToken = default) => inner.GetPageAsync(search, status, skip, take, cancellationToken);
        public Task<MemberRecord> GetMemberRecordAsync(int memberId, CancellationToken cancellationToken = default) => inner.GetMemberRecordAsync(memberId, cancellationToken);
        public Task<MemberRecord> GetMemberRecordByUserIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetMemberRecordByUserIdAsync(userId, cancellationToken);
        public Task<Member> GetMemberByUserIdAsync(int userId, CancellationToken cancellationToken = default) => inner.GetMemberByUserIdAsync(userId, cancellationToken);
        public Task<Member> GetMemberAsync(int memberId, CancellationToken cancellationToken = default) => inner.GetMemberAsync(memberId, cancellationToken);
        public Task<bool> UserHasMemberProfileAsync(int userId, CancellationToken cancellationToken = default) => inner.UserHasMemberProfileAsync(userId, cancellationToken);
        public Task<bool> StudentNumberExistsAsync(string studentNumber, int? excludeId = null, CancellationToken cancellationToken = default) => inner.StudentNumberExistsAsync(studentNumber, excludeId, cancellationToken);
        public Task UpdateMemberAsync(Member member, CancellationToken cancellationToken = default) => inner.UpdateMemberAsync(member, cancellationToken);

        public async Task AddMemberAsync(Member member, CancellationToken cancellationToken = default)
        {
            await inner.AddMemberAsync(member, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("The repository failed after saving.");
        }
    }
}
