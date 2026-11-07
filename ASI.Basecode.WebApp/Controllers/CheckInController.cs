using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Formatting;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers;

/// <summary>
/// Door scanner screen. The page composes the scanner, the shared event selector and check-in
/// control, the server-rendered manual lookup and the recorded check-in log. Scans answer AJAX
/// callers with the AD-9 JSON outcome and form callers with a redirect and TempData; the manual
/// check-in and invalidation always redirect.
/// </summary>
[Authorize(Policy = Policies.Management)]
[AutoValidateAntiforgeryToken]
public sealed class CheckInController : ControllerBase<CheckInController>
{
    private readonly ICheckInService _checkIn;
    private readonly ICheckInLookupService _lookup;
    private readonly IAttendanceInvalidationService _invalidation;

    public CheckInController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        ICheckInService checkIn,
        ICheckInLookupService lookup,
        IAttendanceInvalidationService invalidation)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _checkIn = checkIn;
        _lookup = lookup;
        _invalidation = invalidation;
    }

    [HttpGet]
    [Route("CheckIn")]
    [Route("CheckIn/Index")]
    public async Task<IActionResult> Index(int? eventId, string q, int? page, CancellationToken cancellationToken)
    {
        var screen = await _checkIn.GetScreenAsync(eventId, cancellationToken);
        var outcomeJson = TempData["CheckInOutcome"] as string;
        var outcomeEventId = TempData["CheckInOutcomeEventId"] as int?;
        if (outcomeJson != null && outcomeEventId == screen.EventId)
        {
            ViewData["CheckInOutcome"] = JsonSerializer.Deserialize<CheckInOutcomeViewModel>(outcomeJson);
        }
        if (screen.EventId is int selectedEventId && !string.IsNullOrWhiteSpace(q))
        {
            screen.Lookup = await _lookup.SearchAsync(selectedEventId, q, page, cancellationToken);
        }

        return View(screen);
    }

    [HttpPost]
    public async Task<IActionResult> Scan(int eventId, string token, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _checkIn.ScanAsync(eventId, token, cancellationToken);
            if (IsAjaxRequest)
            {
                return Json(outcome);
            }

            SetOutcomeMessage(eventId, outcome);
        }
        catch (DomainException exception)
        {
            if (IsAjaxRequest)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    new { outcome = CheckInOutcomeViewModel.Invalid, message = exception.Message });
            }

            SetOutcomeMessage(eventId, CheckInOutcomeViewModel.InvalidResult(exception.Message));
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn(
        int eventId,
        int registrationId,
        string q,
        int? page,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _lookup.CheckInAsync(eventId, registrationId, q, cancellationToken);
            SetOutcomeMessage(eventId, outcome);
        }
        catch (DomainException exception)
        {
            SetOutcomeMessage(eventId, CheckInOutcomeViewModel.InvalidResult(exception.Message));
        }

        return RedirectToAction(nameof(Index), new { eventId, q, page });
    }

    [HttpPost]
    public async Task<IActionResult> Invalidate(
        int eventId,
        int registrationId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await _invalidation.InvalidateAsync(eventId, registrationId, reason, cancellationToken);
            TempData["SuccessMessage"] = "Attendance invalidated.";
        }
        catch (DomainException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    private void SetOutcomeMessage(int eventId, CheckInOutcomeViewModel outcome)
    {
        // Carry the full result across POST/redirect/GET, so manual and no-JS operators get the
        // same persistent identity/time/outcome panel as camera and pass-code AJAX callers.
        TempData["CheckInOutcome"] = JsonSerializer.Serialize(outcome);
        TempData["CheckInOutcomeEventId"] = eventId;
        var message = outcome.Outcome switch
        {
            CheckInOutcomeViewModel.Success => $"{outcome.MemberName} checked in.",
            CheckInOutcomeViewModel.Repeat =>
                $"{outcome.MemberName} was already checked in at {ManilaTimeFormatter.Format(outcome.RecordedAt)}.",
            _ => outcome.Message
        };

        TempData[outcome.IsRecorded ? "SuccessMessage" : "ErrorMessage"] = message;
    }
}
