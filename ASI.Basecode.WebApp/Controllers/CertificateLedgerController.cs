using System;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;
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
    public async Task<IActionResult> Index(int? eventId, string tab = null, int? page = null)
    {
        var activeTab = string.Equals(tab, "issued", StringComparison.OrdinalIgnoreCase)
            ? CertificateLedgerTab.Issued
            : CertificateLedgerTab.Eligibility;

        try
        {
            return View(await _ledger.GetPageAsync(eventId, activeTab, page, HttpContext.RequestAborted));
        }
        catch (DomainException exception)
        {
            ViewData["LedgerError"] = exception.Message;
            return View(new CertificateLedgerViewModel { ActiveTab = activeTab });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Certificate ledger read failed ({ExceptionType}).", exception.GetType().Name);
            ViewData["LedgerError"] = "The certificate ledger could not be loaded. Please try again.";
            return View(new CertificateLedgerViewModel { ActiveTab = activeTab });
        }
    }

    [HttpGet]
    [Route("Certificates/Viewer/{id:int}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Viewer(int id)
    {
        try { return View(await _ledger.GetViewerAsync(id, HttpContext.RequestAborted)); }
        catch (DomainException exception) { return DocumentFailure(exception, id); }
    }

    [HttpGet]
    [Route("Certificates/Pdf/{id:int}")]
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
    public Task<IActionResult> Approve(int id, int? eventId, string tab, int? page) =>
        Command(() => _ledger.DecideAsync(id, true, null, HttpContext.RequestAborted), eventId, tab, page, "Certificate eligibility approved.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Decline(int id, string reason, int? eventId, string tab, int? page) =>
        Command(() => _ledger.DecideAsync(id, false, reason, HttpContext.RequestAborted), eventId, tab, page, "Certificate eligibility declined.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Issue(int id, int? eventId, string tab, int? page) =>
        Command(() => _ledger.IssueAsync(id, HttpContext.RequestAborted), eventId, tab, page, "Certificate issued. View it in the Issued Registry.");

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
        ViewData["DocumentError"] = exception.Message;
        return View("Viewer", CertificateViewerViewModel.Empty(id));
    }
}
