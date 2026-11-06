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

/// <summary>
/// Management-only decisions for pending account requests. The queue page posts here and
/// returns to the same status filter; JSON callers keep the outcome envelope.
/// </summary>
[Authorize(Policy = Policies.Management)]
[AutoValidateAntiforgeryToken]
public sealed class ManagementAccountRequestController : ControllerBase<ManagementAccountRequestController>
{
    private readonly IAccountRequestService _accountRequestService;

    public ManagementAccountRequestController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        IAccountRequestService accountRequestService)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _accountRequestService = accountRequestService;
    }

    /// <summary>Approves a pending request and returns to the queue.</summary>
    [HttpPost]
    public async Task<IActionResult> Approve(int id, string status = "pending")
    {
        try
        {
            await _accountRequestService.ApproveAsync(id, GetActorUserId(), HttpContext.RequestAborted);
            return Succeeded("Approved", "Account request approved.", status);
        }
        catch (DomainException exception)
        {
            return HandleDecisionException(exception, status);
        }
    }

    /// <summary>Rejects a pending request with a required reason and returns to the queue.</summary>
    [HttpPost]
    public async Task<IActionResult> Reject(int id, string reason, string status = "pending")
    {
        try
        {
            await _accountRequestService.RejectAsync(id, reason, GetActorUserId(), HttpContext.RequestAborted);
            return Succeeded("Rejected", "Account request rejected.", status);
        }
        catch (DomainException exception)
        {
            return HandleDecisionException(exception, status);
        }
    }

    private IActionResult Succeeded(string outcome, string message, string status)
    {
        if (IsAjaxRequest)
        {
            return Ok(new { outcome, message });
        }

        TempData["SuccessMessage"] = message;
        return RedirectToQueue(status);
    }

    private IActionResult HandleDecisionException(DomainException exception, string status)
    {
        if (IsAjaxRequest)
        {
            return HandleDomainException(exception, model: null);
        }

        TempData["ErrorMessage"] = exception.Message;
        return RedirectToQueue(status);
    }

    private IActionResult RedirectToQueue(string status) =>
        RedirectToAction("Index", "AccountRequest", new { status = NormalizeStatus(status) });

    private static string NormalizeStatus(string status) => status?.Trim().ToLowerInvariant() switch
    {
        "approved" => "approved",
        "rejected" => "rejected",
        "all" => "all",
        _ => "pending"
    };
}
