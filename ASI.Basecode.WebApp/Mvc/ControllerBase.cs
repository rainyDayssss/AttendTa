using ASI.Basecode.Resources;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Declare ControllerBase.
    /// </summary>
    public class ControllerBase<TController> : Controller where TController : class
    {
        /// <summary>AppConfiguration</summary>
        protected readonly IConfiguration _configuration;

        /// <summary>HttpContextAccessor</summary>
        protected readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>Logger</summary>
        protected ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the ControllerBase{TController} class.
        /// </summary>
        /// <param name="httpContextAccessor">HTTP context accessor</param>
        /// <param name="loggerFactory">Logger factory</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="mapper">Mapper</param>
        public ControllerBase(
                                IHttpContextAccessor httpContextAccessor,
                                ILoggerFactory loggerFactory,
                                IConfiguration configuration,
                                IMapper mapper = null)
        {
            this._httpContextAccessor = httpContextAccessor;
            this._configuration = configuration;
            this._logger = loggerFactory.CreateLogger<TController>();
            this._mapper = mapper;
        }

        /// <summary>Mapper</summary>
        protected IMapper _mapper { get; set; }

        /// <summary>
        /// Gets the authenticated database user ID for audit fields.
        /// Returns zero when the request has no valid user claim so services can
        /// reject writes that cannot satisfy the MVP user foreign keys.
        /// </summary>
        protected int GetActorUserId()
        {
            var claimValue = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claimValue, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Gets a value indicating whether the request expects a JSON response rather than a page.
        /// </summary>
        protected bool IsAjaxRequest
        {
            get
            {
                if (string.Equals(Request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Redraws a page form with its field errors, or returns a 400 JSON outcome for AJAX callers.
        /// </summary>
        /// <param name="model">The submitted model to redisplay.</param>
        /// <param name="message">The summary message used when no model-level error exists.</param>
        protected IActionResult InvalidModelResult(object model, string message = "Please correct the highlighted fields.")
        {
            if (IsAjaxRequest)
            {
                return BadRequest(new { outcome = OutcomeCode.Invalid.ToString(), message });
            }

            if (!ModelState.TryGetValue(string.Empty, out var entry) || entry.Errors.Count == 0)
            {
                ModelState.AddModelError(string.Empty, message);
            }

            return View(model);
        }

        /// <summary>
        /// Maps a <see cref="DomainException"/> to the AD-9 outcome contract: field errors redraw
        /// the form, field-less page failures use TempData and POST-redirect-GET, and AJAX callers
        /// receive JSON <c>{ outcome, message }</c> with the mapped HTTP status.
        /// </summary>
        /// <param name="exception">The business failure.</param>
        /// <param name="model">The submitted model to redisplay for a field error.</param>
        /// <param name="redirectAction">The GET action used for a field-less page failure.</param>
        protected IActionResult HandleDomainException(DomainException exception, object model, string redirectAction = null)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (IsAjaxRequest)
            {
                return StatusCode(ToStatusCode(exception.Outcome), new { outcome = exception.Outcome.ToString(), message = exception.Message });
            }

            if (!string.IsNullOrWhiteSpace(exception.Field))
            {
                ModelState.AddModelError(exception.Field, exception.Message);
                return View(model);
            }

            TempData["ErrorMessage"] = exception.Message;
            return string.IsNullOrWhiteSpace(redirectAction) ? View(model) : RedirectToAction(redirectAction);
        }

        private static int ToStatusCode(OutcomeCode outcome)
        {
            return outcome switch
            {
                OutcomeCode.Invalid => StatusCodes.Status400BadRequest,
                OutcomeCode.Forbidden => StatusCodes.Status403Forbidden,
                OutcomeCode.NotFound => StatusCodes.Status404NotFound,
                OutcomeCode.FileFailure => StatusCodes.Status500InternalServerError,
                _ => StatusCodes.Status409Conflict
            };
        }
    }
}
