using System;
using System.Threading;
using System.Threading.Tasks;
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

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Anonymous membership request submission and acknowledgment.
    /// </summary>
    public class AccountRequestController : ControllerBase<AccountRequestController>
    {
        private readonly IAccountRequestService _accountRequestService;
        private readonly IAccountRequestQueueService _accountRequestQueueService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountRequestController"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="configuration">The configuration.</param>
        /// <param name="mapper">The mapper.</param>
        /// <param name="accountRequestService">The membership request service.</param>
        public AccountRequestController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IAccountRequestService accountRequestService,
            IAccountRequestQueueService accountRequestQueueService) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _accountRequestService = accountRequestService;
            _accountRequestQueueService = accountRequestQueueService;
        }

        [HttpGet]
        [Authorize(Policy = Policies.Management)]
        [Route("AccountRequest")]
        [Route("AccountRequest/Index")]
        public async Task<IActionResult> Index(string status = "pending", string search = null, string sort = "newest", int? page = null)
        {
            var normalizedStatus = status?.Trim().ToLowerInvariant() switch
            {
                "approved" => "approved",
                "rejected" => "rejected",
                "all" => "all",
                _ => "pending"
            };
            var normalizedSort = string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase) ? "oldest" : "newest";
            ViewBag.Status = normalizedStatus;
            var result = await _accountRequestQueueService.GetPageAsync(normalizedStatus, page, search, normalizedSort, HttpContext.RequestAborted);
            return View(result);
        }

        [HttpPost]
        [Authorize(Policy = Policies.Management)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string status = "pending", string search = null, string sort = "newest", int? page = null)
        {
            try
            {
                await _accountRequestService.ApproveAsync(id, GetActorUserId(), HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Account request approved.";
                return Redirect(Url.Action(nameof(Index)) + Request.QueryString);
            }
            catch (DomainException exception)
            {
                return await DecisionError(exception, id, null, false, page, search, sort);
            }
        }

        [HttpPost]
        [Authorize(Policy = Policies.Management)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason, string status = "pending", string search = null, string sort = "newest", int? page = null)
        {
            try
            {
                await _accountRequestService.RejectAsync(id, reason, GetActorUserId(), HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Account request rejected.";
                return Redirect(Url.Action(nameof(Index)) + Request.QueryString);
            }
            catch (DomainException exception)
            {
                return await DecisionError(exception, id, reason, true, page, search, sort);
            }
        }

        private async Task<IActionResult> DecisionError(DomainException exception, int id, string reason, bool isRejection, int? page, string search, string sort)
        {
            var normalizedSort = string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase) ? "oldest" : "newest";
            ViewBag.Status = "pending";
            ViewBag.DecisionError = exception.Message;
            ViewBag.DecisionRequestId = id;
            ViewBag.DecisionReason = reason;
            ViewBag.IsRejection = isRejection;
            ViewData["PagerPath"] = Url.Action(nameof(Index));
            var result = await _accountRequestQueueService.GetPageAsync("pending", page, search, normalizedSort, HttpContext.RequestAborted);
            return View("Index", result);
        }

        /// <summary>
        /// Shows the anonymous membership request form.
        /// </summary>
        /// <returns>The request form.</returns>
        [HttpGet]
        [AllowAnonymous]
        [ActionName("Request")]
        public IActionResult RequestForm()
        {
            return View("Request", new AccountRequestViewModel());
        }

        /// <summary>
        /// Validates and stores a pending membership request.
        /// </summary>
        /// <param name="model">The submitted request details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The acknowledgment redirect, or the form with field or summary errors.</returns>
        [HttpPost]
        [AllowAnonymous]
        [ActionName("Request")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRequest(AccountRequestViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                ClearPasswords(model);
                return InvalidModelResult(model);
            }

            try
            {
                await _accountRequestService.SubmitAsync(model, cancellationToken);
                return RedirectToAction(nameof(Submitted));
            }
            catch (DomainException exception)
            {
                ClearPasswords(model);
                return HandleDomainException(exception, model, "Request");
            }
        }

        /// <summary>
        /// Confirms that a request was submitted without promising email or exposing its details.
        /// </summary>
        /// <returns>The acknowledgment page.</returns>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Submitted()
        {
            return View();
        }

        private void ClearPasswords(AccountRequestViewModel model)
        {
            model.Password = null;
            model.ConfirmPassword = null;

            foreach (var field in new[] { nameof(AccountRequestViewModel.Password), nameof(AccountRequestViewModel.ConfirmPassword) })
            {
                if (ModelState.TryGetValue(field, out var entry))
                {
                    entry.AttemptedValue = string.Empty;
                    entry.RawValue = string.Empty;
                }
            }
        }
    }
}
