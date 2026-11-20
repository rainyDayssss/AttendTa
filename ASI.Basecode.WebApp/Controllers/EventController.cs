using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Formatting;
using ASI.Basecode.WebApp.Models;
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
        private readonly IRegistrationReviewService _review;
        private readonly TimeProvider _timeProvider;

        private bool IsRecordDialog => Request.Headers["X-Record-Dialog"] == "true";

        private IActionResult Editor(EventViewModel model)
        {
            ViewData["EventCreationToday"] = ManilaTimeFormatter.ToLocal(_timeProvider.GetUtcNow().UtcDateTime)
                .ToString("yyyy-MM-dd'T'00:00", System.Globalization.CultureInfo.InvariantCulture);
            return IsRecordDialog ? PartialView("_Editor", model) : View(model);
        }

        public EventController(IHttpContextAccessor httpContextAccessor,
                               ILoggerFactory loggerFactory,
                               IConfiguration configuration,
                               IMapper mapper,
                               IEventLifecycleService eventService,
                               IRegistrationReviewService review,
                               TimeProvider timeProvider) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _eventService = eventService;
            _review = review;
            _timeProvider = timeProvider;
        }

        [HttpGet]
        [Route("Event", Name = EventIndexRoute)]
        [Route("Event/Index")]
        public async Task<IActionResult> Index(int? page, CancellationToken cancellationToken)
        {
            ViewData["EventTypeFilters"] = await _eventService.GetTypeFilterOptionsAsync(cancellationToken);
            if (TempData["ListCancelId"] is int failedId)
            {
                ViewData["ListCancelId"] = failedId;
                ViewData["ListCancelError"] = TempData["ListCancelError"];
                ViewData["ListCancelReason"] = TempData["ListCancelReason"];
                ViewData["ListCancelEvent"] = await _eventService.GetEventAsync(failedId, cancellationToken);
            }
            return View(await _eventService.GetPageAsync(page, cancellationToken));
        }

        [HttpGet]
        [Route("Event/Details")]
        [Route("Event/{id:int}")]
        public Task<IActionResult> Details(int id, string status = "pending", int? page = null, CancellationToken cancellationToken = default) =>
            DossierAsync(id, status, page, cancellationToken);

        /// <summary>
        /// Approves the selected pending registrations of this event through the same use case the
        /// review queue uses, then re-renders the dossier roster with one result row per participant.
        /// The event id is the route-owned scope, so a posted id of another event is refused. The
        /// parameter is named <c>id</c> so the POST query carries the dossier's own route key and
        /// the pager and status tabs copy it forward unchanged.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ApproveSelected(int[] ids, int id, string status = "pending", int? page = null, CancellationToken cancellationToken = default)
        {
            var selectedIds = ids ?? Array.Empty<int>();
            if (selectedIds.Length == 0)
            {
                ViewBag.BulkError = BulkApproval.RegistrationEmptySelectionMessage;
            }
            else if (selectedIds.Length > BulkApproval.MaxBatchSize)
            {
                // The batch bound is a request-shape problem, so it is refused before the service
                // is called; the service keeps the same guard for direct callers.
                ViewBag.BulkError = BulkApproval.RegistrationOverBoundMessage;
            }
            else
            {
                try
                {
                    var outcomes = await _review.ApproveManyAsync(selectedIds, id, cancellationToken);
                    ViewBag.BulkResult = RegistrationBulkResult.From(outcomes);
                }
                catch (DomainException exception)
                {
                    ViewBag.BulkError = exception.Message;
                }
            }

            return await DossierAsync(id, status, page, cancellationToken);
        }

        private async Task<IActionResult> DossierAsync(int id, string status, int? page, CancellationToken cancellationToken)
        {
            status = status?.ToLowerInvariant();
            if (status is not ("pending" or "confirmed" or "waitlisted" or "cancelled")) status = "pending";
            var roster = await _review.GetPageAsync(status, id, page, cancellationToken);
            // Read the event after the roster so the dossier and its cancellation controls use
            // the most recent event snapshot if the event changes while the roster is loading.
            var item = await _eventService.GetEventAsync(id, cancellationToken);
            if (item == null) return NotFound();
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var row in roster.Items)
                row.CancellationUnavailableReason = RegistrationCancellationAvailability.Reason(item.EventStatusId, item.StartsAt, row.HasValidAttendance, now);
            ViewData["PromotionCandidate"] = await _review.GetPromotionCandidateAsync(id, cancellationToken);
            async Task<int> Count(string value) => value == status ? roster.TotalCount :
                await _review.GetCountAsync(value, id, cancellationToken);
            var model = new EventDossierViewModel
            {
                Event = item, Status = status, Roster = roster,
                PendingCount = await Count("pending"), ConfirmedCount = await Count("confirmed"),
                WaitlistedCount = await Count("waitlisted"), CancelledCount = await Count("cancelled"),
                HasStarted = item.StartsAt <= now
            };
            // A POST re-render keeps every list link pointing at the GET dossier route. Both values
            // must be a bare path: the pager and ListNavigation.Url append their own query string,
            // so a path that already carried "?id=…" would become "?id=3?status=…".
            ViewData["PagerPath"] = Url.Action(nameof(Details));
            if (Request.Method == "POST") ViewData["ListPath"] = Url.Action(nameof(Details));
            return View(nameof(Details), model);
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
                var item = await _eventService.GetCoverManagementAsync(id, cancellationToken);
                return item.CanUpload ? RedirectToAction(nameof(Edit), new { id }) : View("Cover", item);
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
                return await RedirectCoverErrorAsync(id, exception.Message, cancellationToken);
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
            Editor(await WithOptionsAsync(new EventViewModel(), cancellationToken));

        [HttpPost]
        public async Task<IActionResult> Create(EventViewModel model, IFormFile cover, CancellationToken cancellationToken)
        {
            NormalizeEmptySelection(nameof(model.TypeId), "Select an event type.");
            NormalizeEmptySelection(nameof(model.VenueId), "Select a venue.");
            if (!ModelState.IsValid)
            {
                ViewData["CoverReselect"] = cover != null;
                return Editor(await WithOptionsAsync(model, cancellationToken));
            }

            ToUtc(model);
            try
            {
                await using var content = cover?.OpenReadStream();
                await _eventService.CreateDraftWithCoverAsync(model, content, Path.GetExtension(cover?.FileName), cancellationToken);
                TempData["SuccessMessage"] = "Draft event created.";
                if (IsRecordDialog) return Json(new { saved = true });
                return RedirectToRoute(EventIndexRoute);
            }
            catch (DomainException ex)
            {
                ToManila(model);
                ViewData["CoverReselect"] = cover != null;
                return await HandleAsync(ex, model, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ToManila(model);
                ViewData["CoverReselect"] = cover != null;
                ModelState.AddModelError("cover", ex is InvalidDataException ? ex.Message : "The save could not be confirmed. Check the events list before trying again to avoid a duplicate.");
                return Editor(await WithOptionsAsync(model, cancellationToken));
            }
        }

        private void NormalizeEmptySelection(string field, string message)
        {
            if (ModelState.TryGetValue(field, out var entry) && string.IsNullOrWhiteSpace(entry.AttemptedValue))
            {
                entry.Errors.Clear();
                ModelState.AddModelError(field, message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var model = await _eventService.GetEventAsync(id, cancellationToken);
            if (model == null)
            {
                if (IsRecordDialog) return NotFound();
                return EventNotFoundResult();
            }

            if (model.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
            {
                if (IsRecordDialog) return NotFound();
                TempData["ErrorMessage"] = "Only draft or published events can be edited.";
                return RedirectToRoute(EventIndexRoute);
            }

            ToManila(model);
            return Editor(await WithOptionsAsync(model, cancellationToken));
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EventViewModel model, IFormFile cover, bool removeCover, CancellationToken cancellationToken)
        {
            var current = await _eventService.GetEventAsync(model.EventId, cancellationToken);
            if (current == null || current.EventStatusId is not (EventStatusCode.Draft or EventStatusCode.Published))
            {
                if (IsRecordDialog)
                {
                    ModelState.AddModelError(string.Empty, "This event can no longer be edited.");
                    return Editor(await WithOptionsAsync(model, cancellationToken));
                }

                TempData["ErrorMessage"] = "This event can no longer be edited.";
                return RedirectToRoute(EventIndexRoute);
            }
            model.EventStatusId = current.EventStatusId;
            model.TypeName = current.TypeName;
            model.VenueName = current.VenueName;
            model.HasCover = current.HasCover;
            ModelState.Remove(nameof(model.EventStatusId));
            if (!ModelState.IsValid)
            {
                ViewData["CoverReselect"] = cover != null;
                RestorePublishedFields(model, current);
                return Editor(await WithOptionsAsync(model, cancellationToken));
            }

            ToUtc(model);
            try
            {
                await using var content = cover?.OpenReadStream();
                await _eventService.UpdateDraftWithCoverAsync(model, content, Path.GetExtension(cover?.FileName), removeCover, cancellationToken);
                TempData["SuccessMessage"] = "Event updated.";
                if (IsRecordDialog) return Json(new { saved = true });
                return RedirectToRoute(EventIndexRoute);
            }
            catch (DomainException ex)
            {
                ToManila(model);
                ViewData["CoverReselect"] = cover != null;
                RestorePublishedFields(model, current);
                return await HandleAsync(ex, model, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ToManila(model);
                ViewData["CoverReselect"] = cover != null;
                RestorePublishedFields(model, current);
                ModelState.AddModelError("cover", ex is InvalidDataException ? ex.Message : "The save could not be confirmed. Check the event before trying again.");
                return Editor(await WithOptionsAsync(model, cancellationToken));
            }
        }

        private void RestorePublishedFields(EventViewModel model, EventViewModel current)
        {
            model.HasCover = current.HasCover;
            model.CoverVersion = current.CoverVersion;
            ModelState.Remove(nameof(model.CoverVersion));
            if (current.EventStatusId != EventStatusCode.Published) return;
            model.TypeId = current.TypeId;
            model.VenueId = current.VenueId;
            model.StartsAt = ManilaTimeFormatter.ToLocal(current.StartsAt);
            model.RegistrationDeadline = ManilaTimeFormatter.ToLocal(current.RegistrationDeadline);
            model.ParticipantLimit = current.ParticipantLimit;
            foreach (var field in new[] { nameof(model.TypeId), nameof(model.VenueId), nameof(model.StartsAt), nameof(model.RegistrationDeadline), nameof(model.ParticipantLimit) })
                ModelState.Remove(field);
        }

        [HttpPost]
        public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
        {
            await TryRunAsync(ct => _eventService.PublishAsync(id, ct), "Event published.", cancellationToken);
            return RedirectToRoute(EventIndexRoute);
        }

        [HttpPost]
        public async Task<IActionResult> OpenCheckIn(int id, CancellationToken cancellationToken, bool dossier = false, string returnUrl = null)
        {
            await TryRunAsync(ct => _eventService.SetCheckInWindowAsync(id, true, ct), "Check-in opened.", cancellationToken);
            return StateReturn(id, dossier, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> CloseCheckIn(int id, CancellationToken cancellationToken, bool dossier = false, string returnUrl = null)
        {
            await TryRunAsync(ct => _eventService.SetCheckInWindowAsync(id, false, ct), "Check-in closed.", cancellationToken);
            return StateReturn(id, dossier, returnUrl);
        }

        [HttpPost]
        public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken, bool dossier = false)
        {
            await TryRunAsync(ct => _eventService.CompleteAsync(id, ct), "Event completed.", cancellationToken);
            return StateReturn(id, dossier);
        }

        [HttpPost]
        public async Task<IActionResult> CancelFromList(int id, string reason, string returnUrl, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new DomainException(OutcomeCode.Invalid, "A cancellation reason is required.");
                if (reason.Length > 500)
                    throw new DomainException(OutcomeCode.Invalid, "The reason cannot exceed 500 characters.");
                await _eventService.CancelAsync(id, reason, cancellationToken);
                TempData["SuccessMessage"] = "Event cancelled.";
            }
            catch (DomainException exception)
            {
                TempData["ListCancelId"] = id;
                TempData["ListCancelError"] = exception.Message;
                TempData["ListCancelReason"] = reason;
            }
            return StateReturn(id, false, returnUrl);
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
                return EventNotFoundResult();
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

            await TryRunAsync(ct => _eventService.CancelAsync(model.EventId, model.Reason, ct), "Event cancelled.", cancellationToken);
            return RedirectToRoute(EventIndexRoute);
        }

        private async Task<IActionResult> CancelViewAsync(EventCancelViewModel model, CancellationToken cancellationToken)
        {
            var current = await _eventService.GetEventAsync(model.EventId, cancellationToken);
            if (current == null)
            {
                return EventNotFoundResult();
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

        private async Task TryRunAsync(Func<CancellationToken, Task> command, string successMessage, CancellationToken cancellationToken)
        {
            try
            {
                await command(cancellationToken);
                TempData["SuccessMessage"] = successMessage;
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }
        }

        private IActionResult EventNotFoundResult()
        {
            TempData["ErrorMessage"] = "Event not found.";
            return RedirectToRoute(EventIndexRoute);
        }

        private async Task<IActionResult> HandleCoverErrorAsync(int id, DomainException exception, CancellationToken cancellationToken)
        {
            if (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }

            return await RedirectCoverErrorAsync(id, exception.Message, cancellationToken);
        }

        private async Task<IActionResult> RedirectCoverErrorAsync(int id, string message, CancellationToken cancellationToken)
        {
            try
            {
                var item = await _eventService.GetCoverManagementAsync(id, cancellationToken);
                TempData["ErrorMessage"] = message;
                return RedirectToAction(item.CanUpload ? nameof(Edit) : nameof(Cover), new { id });
            }
            catch (DomainException exception) when (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }
        }

        private async Task<IActionResult> HandleAsync(DomainException exception, EventViewModel model, CancellationToken cancellationToken)
        {
            ModelState.AddModelError(exception.Field ?? string.Empty, exception.Message);
            return Editor(await WithOptionsAsync(model, cancellationToken));
        }

        // Form controls post Asia/Manila wall-clock values; SQL stores UTC.
        private static void ConvertTimes(EventViewModel model, Func<DateTime, DateTime> converter)
        {
            if (model.StartsAt != default)
            {
                model.StartsAt = converter(model.StartsAt);
            }

            if (model.RegistrationDeadline != default)
            {
                model.RegistrationDeadline = converter(model.RegistrationDeadline);
            }
        }

        private static void ToUtc(EventViewModel model) => ConvertTimes(model, ManilaTimeFormatter.ToUtc);

        private static void ToManila(EventViewModel model) => ConvertTimes(model, ManilaTimeFormatter.ToLocal);
    }
}
