using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

// These controller-level cases deliberately need neither SQL nor a running web host.
public sealed class VenueEditorControllerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CreateFailureWithSelectedPhotoRequiresReselection(bool dialog, bool domainError)
    {
        var service = new VenueServiceStub { FailSave = domainError };
        var controller = CreateController(service, dialog);
        if (!domainError) controller.ModelState.AddModelError("Capacity", "Invalid capacity");
        var model = new VenueViewModel { RoomName = "Example" };

        var result = await controller.Create(model, Photo());

        AssertEditor(result, dialog, model);
        Assert.Equal(true, controller.ViewData["PhotoRequired"]);
        Assert.Equal(true, controller.ViewData["PhotoReselect"]);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(domainError ? "photo" : "Capacity"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task EditFailurePreservesOnlyServablePhotoAndAlwaysRequestsReselection(bool dialog, bool domainError, bool servable)
    {
        var service = new VenueServiceStub { FailSave = domainError, Servable = servable };
        var controller = CreateController(service, dialog);
        if (!domainError) controller.ModelState.AddModelError("Capacity", "Invalid capacity");
        var model = new VenueViewModel { VenueId = 17, RoomName = "Example" };

        var result = await controller.Edit(model, Photo());

        AssertEditor(result, dialog, model);
        Assert.Equal(servable ? VenueServiceStub.PhotoPath : null, model.ImagePath);
        Assert.Equal(false, controller.ViewData["PhotoRequired"]);
        Assert.Equal(true, controller.ViewData["PhotoReselect"]);
        Assert.True(controller.ModelState.ContainsKey(domainError ? "photo" : "Capacity"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidEditWithoutNewPhotoDoesNotAskForReselection(bool dialog)
    {
        var controller = CreateController(new VenueServiceStub { Servable = true }, dialog);
        controller.ModelState.AddModelError("Capacity", "Invalid capacity");
        var model = new VenueViewModel { VenueId = 17 };
        AssertEditor(await controller.Edit(model, null!), dialog, model);
        Assert.Null(controller.ViewData["PhotoReselect"]);
        Assert.Equal(VenueServiceStub.PhotoPath, model.ImagePath);
    }

    private static VenueController CreateController(IVenueService service, bool dialog)
    {
        var context = new DefaultHttpContext();
        if (dialog) context.Request.Headers["X-Record-Dialog"] = "true";
        return new VenueController(new HttpContextAccessor { HttpContext = context }, NullLoggerFactory.Instance,
            new ConfigurationBuilder().Build(), null!, service)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private static IFormFile Photo() => new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "photo", "venue.jpg");

    private static void AssertEditor(IActionResult result, bool dialog, VenueViewModel model)
    {
        if (dialog)
        {
            var view = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_Editor", view.ViewName);
            Assert.Same(model, view.Model);
        }
        else Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
    }

    private sealed class VenueServiceStub : IVenueService
    {
        public const string PhotoPath = "uploads/venues/current.jpg";
        public bool FailSave { get; init; }
        public bool Servable { get; init; }
        public VenueViewModel GetVenue(int id) => new() { VenueId = id, ImagePath = PhotoPath };
        public bool IsServablePhoto(string path) => Servable;
        public Task AddVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default) => Save();
        public Task UpdateVenueAsync(VenueViewModel model, Stream photo, string extension, CancellationToken cancellationToken = default) => Save();
        private Task Save() => FailSave ? Task.FromException(new DomainException(OutcomeCode.Invalid, "Invalid photo", "photo")) : Task.CompletedTask;
        public Task<PagedResult<VenueViewModel>> GetPageAsync(string status, int? pageNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VenuePhotoStream> GetPhotoAsync(int venueId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
