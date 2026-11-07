using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = Policies.Management)]
    [AutoValidateAntiforgeryToken]
    public class VenueController : ControllerBase<VenueController>
    {
        private const string VenueIndexRoute = "VenueIndex";

        private readonly IVenueService _venueService;
        private readonly IPrivateFileStore _privateFileStore;

        private bool IsRecordDialog => Request.Headers["X-Record-Dialog"] == "true";

        private IActionResult Editor(VenueViewModel model)
        {
            return IsRecordDialog ? PartialView("_Editor", model) : View(model);
        }

        private IActionResult EditorError(DomainException exception, VenueViewModel model)
        {
            if (!IsRecordDialog) return HandleDomainException(exception, model);
            ModelState.AddModelError(exception.Field ?? string.Empty, exception.Message);
            return Editor(model);
        }

        public VenueController(IHttpContextAccessor httpContextAccessor, ILoggerFactory loggerFactory,
            IConfiguration configuration, IMapper mapper, IVenueService venueService, IPrivateFileStore privateFileStore)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _venueService = venueService;
            _privateFileStore = privateFileStore;
        }

        [HttpGet]
        [Route("Venue", Name = VenueIndexRoute)]
        [Route("Venue/Index")]
        public async Task<IActionResult> Index(string status, int? page, CancellationToken cancellationToken)
        {
            ViewBag.Status = status;

            try
            {
                return View(await _venueService.GetPageAsync(status, page, cancellationToken));
            }
            catch (DomainException exception)
            {
                ViewData["ListError"] = exception.Message;
                return View(EmptyPage());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Venue list read failed ({ExceptionType}).", exception.GetType().Name);
                ViewData["ListError"] = "The venue list could not be loaded. Please try again.";
                return View(EmptyPage());
            }
        }

        private static PagedResult<VenueViewModel> EmptyPage() =>
            new PagedResult<VenueViewModel>(Array.Empty<VenueViewModel>(), 1, 0);

        [HttpGet]
        public async Task<IActionResult> Photo(int id, CancellationToken cancellationToken)
        {
            var venue = _venueService.GetVenue(id);
            if (venue?.ImagePath == null || venue.ImagePath.Contains('\0') || !venue.ImagePath.StartsWith("uploads/venues/", StringComparison.Ordinal)) return NotFound();
            var contentType = Path.GetExtension(venue.ImagePath ?? string.Empty).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => null
            };
            if (contentType == null) return NotFound();
            try { return File(await _privateFileStore.OpenSafelyAsync(venue.ImagePath, cancellationToken), contentType); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DomainException { Outcome: OutcomeCode.NotFound }) { return NotFound(); }
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewData["PhotoRequired"] = true;
            return Editor(new VenueViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(VenueViewModel model, IFormFile photo)
        {
            if (!ModelState.IsValid)
            {
                ViewData["PhotoRequired"] = true;
                return Editor(model);
            }

            try
            {
                await using var content = photo?.OpenReadStream();
                await _venueService.AddVenueAsync(model, content, Path.GetExtension(photo?.FileName), HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Venue added successfully.";
                if (IsRecordDialog) return Json(new { saved = true });
                return RedirectToAction("Index");
            }
            catch (DomainException ex) { return EditorError(ex, model); }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var model = _venueService.GetVenue(id);
            if (model == null)
            {
                if (IsRecordDialog) return NotFound();
                return RedirectToAction("Index");
            }

            ViewData["PhotoRequired"] = false;
            return Editor(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(VenueViewModel model, IFormFile photo)
        {
            if (!ModelState.IsValid)
            {
                ViewData["PhotoRequired"] = false;
                return Editor(model);
            }

            try
            {
                await using var content = photo?.OpenReadStream();
                await _venueService.UpdateVenueAsync(model, content, Path.GetExtension(photo?.FileName), HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Venue updated successfully.";
                if (IsRecordDialog) return Json(new { saved = true });
                return RedirectToAction("Index");
            }
            catch (DomainException ex) { return EditorError(ex, model); }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id, int? page)
        {
            try
            {
                await _venueService.ToggleActiveAsync(id, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Venue status updated.";
            }
            catch (DomainException ex) { TempData["ErrorMessage"] = ex.Message; }
            return page is > 0 ? RedirectToRoute(VenueIndexRoute, new { page }) : RedirectToRoute(VenueIndexRoute);
        }
    }
}
