using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.DemoData;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers;

/// <summary>
/// Participant-facing published event feed, registration submission and member self-cancellation.
/// Every action is authenticated; registration additionally requires an eligible member profile in
/// the service, and cancellation only ever touches the actor's own registration.
/// </summary>
[Authorize]
[AutoValidateAntiforgeryToken]
public sealed class MemberEventsController : ControllerBase<MemberEventsController>
{
    private const string FeedRoute = "MemberEventFeed";

    private readonly IMemberEventService _memberEvents;
    private readonly IEventStateOverlay _eventStateOverlay;

    public MemberEventsController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        IMemberEventService memberEvents,
        IEventStateOverlay eventStateOverlay)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _memberEvents = memberEvents;
        _eventStateOverlay = eventStateOverlay;
    }

    [HttpGet]
    [Route("Events", Name = FeedRoute)]
    [Route("MemberEvents")]
    public async Task<IActionResult> Index(int? page, CancellationToken cancellationToken) =>
        View(_eventStateOverlay.Apply(await _memberEvents.GetFeedAsync(page, cancellationToken)));

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _memberEvents.GetDetailAsync(id, cancellationToken);
        return model == null ? NotFound() : View(_eventStateOverlay.Apply(model));
    }

    [HttpPost]
    public async Task<IActionResult> Register(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _memberEvents.RegisterAsync(id, cancellationToken);
            TempData["SuccessMessage"] = result.SeatNotGuaranteed
                ? "Registration submitted. This event is full, so a seat is not guaranteed."
                : "Registration submitted. Your request is pending review.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainException exception)
        {
            TempData["ErrorMessage"] = exception.Message;

            // A duplicate implies the event is published and still open, so the detail page is safe.
            return exception.Outcome == OutcomeCode.Duplicate
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectToRoute(FeedRoute);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _memberEvents.CancelRegistrationAsync(id, cancellationToken);
            TempData["SuccessMessage"] = "Your registration was cancelled. This cannot be undone.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainException exception)
        {
            TempData["ErrorMessage"] = exception.Message;

            // A missing or stale registration has no safe detail page to return to.
            return exception.Outcome is OutcomeCode.NotFound or OutcomeCode.Stale
                ? RedirectToRoute(FeedRoute)
                : RedirectToAction(nameof(Details), new { id });
        }
    }
}
