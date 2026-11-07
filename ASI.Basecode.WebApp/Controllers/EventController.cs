using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.DemoData;
using ASI.Basecode.WebApp.Formatting;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Management draft Event screens. Posted <c>datetime-local</c> values are Asia/Manila and are
    /// converted to UTC here, so Services never convert time zones (AD-11).
    /// </summary>
    [Authorize(Policy = Policies.Management)]
    [AutoValidateAntiforgeryToken]
    public class EventController : ControllerBase<EventController>
    {
        private const string EventIndexRoute = "EventIndex";

        private readonly IEventLifecycleService _eventService;
        private readonly IEventStateCommands _eventStateCommands;
        private readonly IEventStateOverlay _eventStateOverlay;
        private readonly IRegistrationReviewService _review;
        private readonly TimeProvider _timeProvider;

        public EventController(IHttpContextAccessor httpContextAccessor,
                               ILoggerFactory loggerFactory,
                               IConfiguration configuration,
                               IMapper mapper,
                               IEventLifecycleService eventService,
                               IEventStateCommands eventStateCommands,
                                IEventStateOverlay eventStateOverlay,
                                IRegistrationReviewService review,
                                TimeProvider timeProvider) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _eventService = eventService;
            _eventStateCommands = eventStateCommands;
            _eventStateOverlay = eventStateOverlay;
            _review = review;
            _timeProvider = timeProvider;
        }

        [HttpGet]
        [Route("Event", Name = EventIndexRoute)]
        [Route("Event/Index")]
        public async Task<IActionResult> Index(int? page, CancellationToken cancellationToken) =>
            View(_eventStateOverlay.Apply(await _eventService.GetPageAsync(page, cancellationToken)));

        [HttpGet]
        [Route("Event/Details")]
        [Route("Event/{id:int}")]
        public async Task<IActionResult> Details(int id, string status = "pending", int? page = null, CancellationToken cancellationToken = default)
        {
            var item = await _eventService.GetEventAsync(id, cancellationToken);
            if (item == null) return NotFound();
            status = status?.ToLowerInvariant();
            if (status is not ("pending" or "confirmed" or "waitlisted" or "cancelled")) status = "pending";
            var roster = await _review.GetPageAsync(status, id, page, cancellationToken);
            async Task<int> Count(string value) => value == status ? roster.TotalCount :
                (await _review.GetPageAsync(value, id, 1, cancellationToken)).TotalCount;
            var model = new EventDossierViewModel
            {
                Event = _eventStateOverlay.Apply(item), Status = status, Roster = roster,
                PendingCount = await Count("pending"), ConfirmedCount = await Count("confirmed"),
                WaitlistedCount = await Count("waitlisted"),
                HasStarted = item.StartsAt <= _timeProvider.GetUtcNow().UtcDateTime
            };
            return View(model);
        }

        private IActionResult StateReturn(int id, bool dossier, string returnUrl = null) =>
            !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : dossier
                    ? RedirectToAction(nameof(Details), new { id })
                    : RedirectToRoute(EventIndexRoute);

        [HttpGet]
        public async Task<IActionResult> Cover(int id, CancellationToken cancellationToken)
        {
            try
            {
                return View(await _eventService.GetCoverManagementAsync(id, cancellationToken));
            }
            catch (DomainException exception) when (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadCover(int id, IFormFile cover, CancellationToken cancellationToken)
        {
            try
            {
                await using var content = cover?.OpenReadStream();
                await _eventService.UploadCoverAsync(id, content, Path.GetExtension(cover?.FileName), cancellationToken);
                TempData["SuccessMessage"] = "Event cover updated.";
                return RedirectToAction(nameof(Cover), new { id });
            }
            catch (InvalidDataException exception)
            {
                ModelState.AddModelError("cover", exception.Message);
                return await CoverViewAsync(id, cancellationToken);
            }
            catch (DomainException exception)
            {
                return await HandleCoverErrorAsync(id, exception, cancellationToken);
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveCover(int id, CancellationToken cancellationToken)
        {
            try
            {
                await _eventService.RemoveDraftCoverAsync(id, cancellationToken);
                TempData["SuccessMessage"] = "Draft event cover removed.";
                return RedirectToAction(nameof(Cover), new { id });
            }
            catch (DomainException exception)
            {
                return await HandleCoverErrorAsync(id, exception, cancellationToken);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
            View(await WithOptionsAsync(new EventViewModel(), cancellationToken));

        [HttpPost]
        public async Task<IActionResult> Create(EventViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return View(await WithOptionsAsync(model, cancellationToken));
            }

            ToUtc(model);
            try
            {
                await _eventService.CreateDraftAsync(model, cancellationToken);
                TempData["SuccessMessage"] = "Draft event created.";
                return RedirectToRoute(EventIndexRoute);
            }
            catch (DomainException ex)
            {
                ToManila(model);
                return await HandleAsync(ex, model, cancellationToken);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var model = await _eventService.GetEventAsync(id, cancellationToken);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToRoute(EventIndexRoute);
            }

            if (model.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
            {
                TempData["ErrorMessage"] = "Only draft or published events can be edited.";
                return RedirectToRoute(EventIndexRoute);
            }

            ToManila(model);
            return View(await WithOptionsAsync(model, cancellationToken));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EventViewModel model, CancellationToken cancellationToken)
        {
            var current = await _eventService.GetEventAsync(model.EventId, cancellationToken);
            if (current == null || current.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
            {
                TempData["ErrorMessage"] = "This event can no longer be edited.";
                return RedirectToRoute(EventIndexRoute);
            }
            model.EventStatusId = current.EventStatusId;
            model.TypeName = current.TypeName;
            model.VenueName = current.VenueName;
            ModelState.Remove(nameof(model.EventStatusId));
            if (!ModelState.IsValid)
            {
                return View(await WithOptionsAsync(model, cancellationToken));
            }

            ToUtc(model);
            try
            {
                await _eventService.UpdateDraftAsync(model, cancellationToken);
                TempData["SuccessMessage"] = "Event updated.";
                return RedirectToRoute(EventIndexRoute);
            }
            catch (DomainException ex)
            {
                ToManila(model);
                return await HandleAsync(ex, model, cancellationToken);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
        {
            try
            {
                await _eventService.PublishAsync(id, cancellationToken);
                TempData["SuccessMessage"] = "Event published.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return RedirectToRoute(EventIndexRoute);
        }

        [HttpPost]
        public async Task<IActionResult> OpenCheckIn(int id, CancellationToken cancellationToken, bool dossier = false, string returnUrl = null)
        {
            try
            {
                await _eventStateCommands.OpenCheckInAsync(id, cancellationToken);
                TempData["SuccessMessage"] = "Check-in opened.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return StateReturn(id, dossier, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> CloseCheckIn(int id, CancellationToken cancellationToken, bool dossier = false, string returnUrl = null)
        {
            try
            {
                await _eventStateCommands.CloseCheckInAsync(id, cancellationToken);
                TempData["SuccessMessage"] = "Check-in closed.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return StateReturn(id, dossier, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken, bool dossier = false)
        {
            try
            {
                await _eventService.CompleteAsync(id, cancellationToken);
                TempData["SuccessMessage"] = "Event completed.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return StateReturn(id, dossier);
        }

        [HttpPost]
        public async Task<IActionResult> CancelFromDossier(int id, string reason, CancellationToken cancellationToken)
        {
            try
            {
                await _eventService.CancelAsync(id, reason, cancellationToken);
                TempData["SuccessMessage"] = "Event cancelled.";
            }
            catch (DomainException exception)
            {
                TempData["DossierError"] = exception.Message;
                TempData["DossierDialog"] = "event-cancel";
                TempData["DossierReason"] = reason;
                TempData["DossierId"] = id;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
        {
            var model = await _eventService.GetEventAsync(id, cancellationToken);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToRoute(EventIndexRoute);
            }

            if (model.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
            {
                TempData["ErrorMessage"] = "Only draft or published events can be cancelled.";
                return RedirectToRoute(EventIndexRoute);
            }

            return View(new EventCancelViewModel
            {
                EventId = model.EventId,
                Title = model.Title,
                EventStatusId = model.EventStatusId,
                StartsAt = model.StartsAt
            });
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(EventCancelViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return await CancelViewAsync(model, cancellationToken);
            }

            try
            {
                await _eventService.CancelAsync(model.EventId, model.Reason, cancellationToken);
                TempData["SuccessMessage"] = "Event cancelled.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return RedirectToRoute(EventIndexRoute);
        }

        private async Task<IActionResult> CancelViewAsync(EventCancelViewModel model, CancellationToken cancellationToken)
        {
            var current = await _eventService.GetEventAsync(model.EventId, cancellationToken);
            if (current == null)
            {
                TempData["ErrorMessage"] = "Event not found.";
                return RedirectToRoute(EventIndexRoute);
            }

            model.Title = current.Title;
            model.EventStatusId = current.EventStatusId;
            model.StartsAt = current.StartsAt;
            return View(model);
        }

        private async Task<EventViewModel> WithOptionsAsync(EventViewModel model, CancellationToken cancellationToken)
        {
            model.FormOptions = await _eventService.GetFormOptionsAsync(cancellationToken);
            return model;
        }

        private async Task<IActionResult> CoverViewAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                return View("Cover", await _eventService.GetCoverManagementAsync(id, cancellationToken));
            }
            catch (DomainException exception) when (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }
        }

        private async Task<IActionResult> HandleCoverErrorAsync(int id, DomainException exception, CancellationToken cancellationToken)
        {
            if (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }

            if (exception.Field != null)
            {
                ModelState.AddModelError(exception.Field, exception.Message);
            }
            else
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return await CoverViewAsync(id, cancellationToken);
        }

        private async Task<IActionResult> HandleAsync(DomainException exception, EventViewModel model, CancellationToken cancellationToken)
        {
            if (exception.Field == null)
            {
                return HandleDomainException(exception, model, nameof(Index));
            }

            model.FormOptions = await _eventService.GetFormOptionsAsync(cancellationToken);
            return HandleDomainException(exception, model);
        }

        // Form controls post Asia/Manila wall-clock values; SQL stores UTC.
        private static void ToUtc(EventViewModel model)
        {
            if (model.StartsAt != default)
            {
                model.StartsAt = ManilaTimeFormatter.ToUtc(model.StartsAt);
            }

            if (model.RegistrationDeadline != default)
            {
                model.RegistrationDeadline = ManilaTimeFormatter.ToUtc(model.RegistrationDeadline);
            }
        }

        private static void ToManila(EventViewModel model)
        {
            if (model.StartsAt != default)
            {
                model.StartsAt = ManilaTimeFormatter.ToLocal(model.StartsAt);
            }

            if (model.RegistrationDeadline != default)
            {
                model.RegistrationDeadline = ManilaTimeFormatter.ToLocal(model.RegistrationDeadline);
            }
        }
    }
}
