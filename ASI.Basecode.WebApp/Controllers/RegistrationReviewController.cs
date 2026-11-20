using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Policy = Policies.Management)]
public sealed class RegistrationReviewController(IHttpContextAccessor accessor, ILoggerFactory logger, IConfiguration configuration,
    IMapper mapper, IRegistrationReviewService review) : ControllerBase<RegistrationReviewController>(accessor, logger, configuration, mapper)
{
    [HttpGet]
    public async Task<IActionResult> Index(string status = "pending", int? eventId = null, int? page = null, CancellationToken cancellationToken = default)
    {
        ViewBag.Status = status;
        ViewBag.EventId = eventId;
        ViewData["EventOptions"] = await review.GetEventOptionsAsync(cancellationToken);
        if (status == "waitlisted" && eventId.HasValue)
            ViewData["PromotionCandidate"] = await review.GetPromotionCandidateAsync(eventId.Value, cancellationToken);
        return View(await review.GetPageAsync(status, eventId, page, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id, string status, int? eventId, int? page, CancellationToken cancellationToken = default) => Decide(() => review.ApproveAsync(id, cancellationToken), status, eventId, page, id: id, dialog: "approve");

    /// <summary>
    /// Approves the selected pending registrations through the single-record path and re-renders
    /// the queue with one result row per selected participant. The batch bound is a request-shape
    /// problem, so it is refused before the service is called; the service keeps the same guard for
    /// direct callers.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveSelected(int[] ids, string status = "pending", int? eventId = null, int? page = null, CancellationToken cancellationToken = default)
    {
        // Accept only the queue's own status keys, so a hand-edited value cannot render pending
        // rows under no tab, suppress the waitlist Promote block or re-post itself.
        var normalizedStatus = NormalizeStatus(status);
        ViewBag.Status = normalizedStatus;
        ViewBag.EventId = eventId;
        ViewData["EventOptions"] = await review.GetEventOptionsAsync(cancellationToken);
        // The POST re-renders the queue, so every list link keeps pointing at the GET route.
        ViewData["PagerPath"] = Url.Action(nameof(Index));
        ViewData["ListPath"] = Url.Action(nameof(Index));

        var selectedIds = ids ?? Array.Empty<int>();
        if (selectedIds.Length == 0)
        {
            ViewBag.BulkError = BulkApproval.RegistrationEmptySelectionMessage;
        }
        else if (selectedIds.Length > BulkApproval.MaxBatchSize)
        {
            ViewBag.BulkError = BulkApproval.RegistrationOverBoundMessage;
        }
        else
        {
            try
            {
                var outcomes = await review.ApproveManyAsync(selectedIds, eventId, cancellationToken);
                ViewBag.BulkResult = RegistrationBulkResult.From(outcomes);
            }
            catch (DomainException exception)
            {
                ViewBag.BulkError = exception.Message;
            }
        }

        return View("Index", await review.GetPageAsync(normalizedStatus, eventId, page, cancellationToken));
    }

    /// <summary>
    /// Accepts only the queue's own status keys, so a posted or hand-edited value cannot render a
    /// page the queue service does not understand.
    /// </summary>
    private static string NormalizeStatus(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "confirmed" => "confirmed",
        "waitlisted" => "waitlisted",
        "cancelled" => "cancelled",
        _ => "pending"
    };

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Decline(int id, string reason, string status, int? eventId, int? page, CancellationToken cancellationToken = default) => Decide(() => review.DeclineAsync(id, reason, cancellationToken), status, eventId, page, id: id, dialog: "decline", reason: reason);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(int id, string reason, string status, int? eventId, int? page, CancellationToken cancellationToken = default) => Decide(() => review.CancelAsync(id, reason, cancellationToken), status, eventId, page, id: id, dialog: "registration-cancel", reason: reason);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Promote(int eventId, int expectedRegistrationId, int? page, CancellationToken cancellationToken = default) =>
        Decide(() => review.PromoteNextAsync(eventId, expectedRegistrationId, cancellationToken), "waitlisted", eventId, page, "Next eligible participant promoted.", eventId, "promote");

    private async Task<IActionResult> Decide(Func<Task> command, string status, int? eventId, int? page, string successMessage = "Registration updated.", int id = 0, string dialog = null, string reason = null)
    {
        var dossier = eventId.HasValue && Request.Form["dossier"] == "true";
        try { await command(); TempData["SuccessMessage"] = successMessage; }
        catch (DomainException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            if (dossier)
            {
                TempData["DossierError"] = ex.Message;
                TempData["DossierDialog"] = dialog == "promote" ? null : dialog;
                TempData["DossierId"] = id;
                TempData["DossierReason"] = reason;
            }
            else
            {
                TempData["ReviewDialog"] = dialog == "promote" ? null : dialog;
                TempData["ReviewId"] = id;
                TempData["ReviewReason"] = reason;
            }
        }
        var search = Request.Query["search"].ToString();
        var pageSize = Request.Query["pageSize"].ToString();
        if (dossier) return RedirectToAction("Details", "Event", new { id = eventId, status, page, search, pageSize });
        return RedirectToAction(nameof(Index), new { status, eventId, page, search, pageSize });
    }
}
