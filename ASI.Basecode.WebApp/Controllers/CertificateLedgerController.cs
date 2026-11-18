using System;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;
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

public sealed class CertificateLedgerController : ControllerBase<CertificateLedgerController>
{
    private readonly ICertificateLedgerService _ledger;
    public CertificateLedgerController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        ICertificateLedgerService ledger)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _ledger = ledger;
    }

    [HttpGet]
    [Route("Certificates")]
    [Authorize(Policy = Policies.Management)]
    public Task<IActionResult> Index(int? eventId, string tab = null, int? page = null) =>
        RenderLedger(eventId, ParseTab(tab), page);

    [HttpGet]
    [Route("Certificates/Viewer/{id:int}")]
    [Authorize(Policy = Policies.Management)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Viewer(int id)
    {
        try { return View(await _ledger.GetViewerAsync(id, HttpContext.RequestAborted)); }
        catch (DomainException exception) { return DocumentFailure(exception, id); }
    }

    [HttpGet]
    [Route("Certificates/Pdf/{id:int}")]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Pdf(int id, bool download = false)
    {
        try
        {
            var document = await _ledger.GetPdfAsync(id, HttpContext.RequestAborted);
            return download
                ? File(document.Bytes, "application/pdf", $"{document.CertificateNumber}.pdf")
                : File(document.Bytes, "application/pdf");
        }
        catch (DomainException exception) { return DocumentFailure(exception, id); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.Management)]
    public Task<IActionResult> Approve(int id, int? eventId, string tab, int? page) =>
        Command(() => _ledger.DecideAsync(id, true, null, HttpContext.RequestAborted), eventId, tab, page, "Certificate eligibility approved.");

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.Management)]
    public Task<IActionResult> Decline(int id, string reason, int? eventId, string tab, int? page) =>
        Command(() => _ledger.DecideAsync(id, false, reason, HttpContext.RequestAborted), eventId, tab, page, "Certificate eligibility declined.");

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.Management)]
    public Task<IActionResult> Issue(int id, int? eventId, string tab, int? page) =>
        Command(() => _ledger.IssueAsync(id, HttpContext.RequestAborted), eventId, tab, page, "Certificate issued. View it in the Issued Registry.");

    /// <summary>
    /// Approves the selected pending reviews through the single-record path and re-renders the
    /// ledger with one result row per selected member. The batch bound is a request-shape problem,
    /// so it is refused before the service is called; the service keeps the same guard for direct
    /// callers. The posted route values restore the reader's event, tab, search, filter and page.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.Management)]
    public async Task<IActionResult> ApproveSelected(int[] ids, int? eventId, string tab, int? page)
    {
        var activeTab = ParseTab(tab);

        // The POST re-renders the ledger, so every list link keeps pointing at the GET route.
        ViewData["PagerPath"] = Url.Action(nameof(Index));
        ViewData["ListPath"] = Url.Action(nameof(Index));

        var selectedIds = ids ?? Array.Empty<int>();
        if (selectedIds.Length == 0)
        {
            ViewBag.BulkError = BulkApproval.EmptySelectionMessageFor("certificate review");
        }
        else if (selectedIds.Length > BulkApproval.MaxBatchSize)
        {
            ViewBag.BulkError = BulkApproval.OverBoundMessageFor("certificate review");
        }
        else
        {
            try
            {
                var outcomes = await _ledger.ApproveManyAsync(selectedIds, HttpContext.RequestAborted);
                ViewBag.BulkResult = new BulkResultViewModel(
                    "Approval results",
                    outcomes.Select(outcome => new BulkResultItem(Label(outcome), outcome.Succeeded, outcome.Message)).ToList(),
                    "Approved",
                    "Not approved");
            }
            catch (DomainException exception)
            {
                ViewBag.BulkError = exception.Message;
            }
        }

        return await RenderLedger(eventId, activeTab, page);
    }

    private static CertificateLedgerTab ParseTab(string tab) =>
        string.Equals(tab, "issued", StringComparison.OrdinalIgnoreCase)
            ? CertificateLedgerTab.Issued
            : CertificateLedgerTab.Eligibility;

    private static string Label(CertificateApprovalOutcome outcome) =>
        string.IsNullOrWhiteSpace(outcome.StudentNumber)
            ? outcome.MemberName
            : $"{outcome.MemberName} ({outcome.StudentNumber})";

    /// <summary>
    /// Renders the ledger for a GET or a POST re-render. Both paths share one failure surface, so a
    /// batch never lands on an unhandled exception page.
    /// </summary>
    private async Task<IActionResult> RenderLedger(int? eventId, CertificateLedgerTab activeTab, int? page)
    {
        try
        {
            return View("Index", await _ledger.GetPageAsync(eventId, activeTab, page, HttpContext.RequestAborted));
        }
        catch (DomainException exception)
        {
            ViewData["LedgerError"] = exception.Message;
            return View("Index", new CertificateLedgerViewModel { ActiveTab = activeTab });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Certificate ledger read failed ({ExceptionType}).", exception.GetType().Name);
            ViewData["LedgerError"] = "The certificate ledger could not be loaded. Please try again.";
            return View("Index", new CertificateLedgerViewModel { ActiveTab = activeTab });
        }
    }

    private async Task<IActionResult> Command(Func<Task> command, int? eventId, string tab, int? page, string success)
    {
        TempData.Remove("SuccessMessage");
        TempData.Remove("ErrorMessage");
        try { await command(); TempData["SuccessMessage"] = success; }
        catch (DomainException exception) { TempData["ErrorMessage"] = exception.Message; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Certificate command failed ({ExceptionType}).", exception.GetType().Name);
            TempData["ErrorMessage"] = "The certificate could not be updated. Please reload the ledger and try again.";
        }
        return RedirectToAction(nameof(Index), new { eventId, tab = tab == "issued" ? "issued" : "eligibility", page });
    }

    private IActionResult DocumentFailure(DomainException exception, int id)
    {
        Response.StatusCode = exception.Outcome switch
        {
            OutcomeCode.NotFound => StatusCodes.Status404NotFound,
            OutcomeCode.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status409Conflict
        };
        ViewData["DocumentError"] = exception.Outcome == OutcomeCode.NotFound
            && (User.IsInRole(RoleCodes.Admin) || User.IsInRole(RoleCodes.Officer))
            ? "The certificate document is unavailable. Return to the registry or ask an administrator to check the stored PDF."
            : exception.Message;
        return View("Viewer", CertificateViewerViewModel.Empty(id));
    }
}
