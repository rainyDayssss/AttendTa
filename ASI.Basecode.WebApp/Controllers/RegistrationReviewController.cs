using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
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
        return View(await review.GetPageAsync(status, eventId, page, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id, string status, int? eventId, int? page, CancellationToken cancellationToken = default) => Decide(() => review.ApproveAsync(id, cancellationToken), status, eventId, page, id: id, dialog: "approve");
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Decline(int id, string reason, string status, int? eventId, int? page, CancellationToken cancellationToken = default) => Decide(() => review.DeclineAsync(id, reason, cancellationToken), status, eventId, page, id: id, dialog: "decline", reason: reason);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(int id, string reason, string status, int? eventId, int? page, CancellationToken cancellationToken = default) => Decide(() => review.CancelAsync(id, reason, cancellationToken), status, eventId, page, id: id, dialog: "registration-cancel", reason: reason);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Promote(int eventId, int? page, CancellationToken cancellationToken = default) =>
        Decide(() => review.PromoteNextAsync(eventId, cancellationToken), "waitlisted", eventId, page, "Next eligible participant promoted.", eventId, "promote");

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
                TempData["DossierDialog"] = dialog;
                TempData["DossierId"] = id;
                TempData["DossierReason"] = reason;
            }
        }
        if (dossier) return RedirectToAction("Details", "Event", new { id = eventId, status, page });
        return RedirectToAction(nameof(Index), new { status, eventId, page });
    }
}
