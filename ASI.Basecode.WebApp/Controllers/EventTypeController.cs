using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = Policies.Management)]
    [AutoValidateAntiforgeryToken]
    public class EventTypeController : ControllerBase<EventTypeController>
    {
        private const string EventTypeIndexRoute = "EventTypeIndex";

        private readonly IEventTypeService _eventTypeService;

        private bool IsRecordDialog => Request.Headers["X-Record-Dialog"] == "true";

        private IActionResult Editor(EventTypeViewModel model)
        {
            return IsRecordDialog ? PartialView("_Editor", model) : View(model);
        }

        private IActionResult EditorError(DomainException exception, EventTypeViewModel model)
        {
            if (!IsRecordDialog) return HandleDomainException(exception, model);
            ModelState.AddModelError(exception.Field ?? string.Empty, exception.Message);
            return Editor(model);
        }

        public EventTypeController(IHttpContextAccessor httpContextAccessor,
                                   ILoggerFactory loggerFactory,
                                   IConfiguration configuration,
                                   IMapper mapper,
                                   IEventTypeService eventTypeService) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _eventTypeService = eventTypeService;
        }

        [HttpGet]
        [Route("EventType", Name = EventTypeIndexRoute)]
        [Route("EventType/Index")]
        public async Task<IActionResult> Index(string status, int? page = null)
        {
            ViewBag.Status = status;

            try
            {
                var eventTypes = await _eventTypeService.GetPageAsync(status, page, HttpContext.RequestAborted);
                return View(eventTypes);
            }
            catch (DomainException exception)
            {
                ViewData["ListError"] = exception.Message;
                return View(EmptyPage());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Event type list read failed ({ExceptionType}).", exception.GetType().Name);
                ViewData["ListError"] = "The event type list could not be loaded. Please try again.";
                return View(EmptyPage());
            }
        }

        private static PagedResult<EventTypeViewModel> EmptyPage() =>
            new PagedResult<EventTypeViewModel>(Array.Empty<EventTypeViewModel>(), 1, 0);

        [HttpGet]
        public IActionResult Create()
        {
            return Editor(new EventTypeViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Create(EventTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Editor(model);
            }

            try
            {
                await _eventTypeService.AddEventTypeAsync(model, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Event type added successfully.";
                if (IsRecordDialog) return Json(new { saved = true });
                return RedirectToAction("Index");
            }
            catch (DomainException ex)
            {
                return EditorError(ex, model);
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var model = _eventTypeService.GetEventType(id);
            if (model == null)
            {
                if (IsRecordDialog) return NotFound();
                return RedirectToAction("Index");
            }

            return Editor(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EventTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Editor(model);
            }

            try
            {
                await _eventTypeService.UpdateEventTypeAsync(model, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Event type updated successfully.";
                if (IsRecordDialog) return Json(new { saved = true });
                return RedirectToAction("Index");
            }
            catch (DomainException ex)
            {
                return EditorError(ex, model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id, int? page)
        {
            try
            {
                await _eventTypeService.ToggleActiveAsync(id, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Event type status updated.";
            }
            catch (DomainException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return page is > 0 ? RedirectToRoute(EventTypeIndexRoute, new { page }) : RedirectToAction("Index");
        }
    }
}
