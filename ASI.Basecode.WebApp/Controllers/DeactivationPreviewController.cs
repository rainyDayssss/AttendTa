using System.Threading.Tasks;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
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
    /// Read-only Management preview of the registrations an account deactivation would cancel.
    /// The endpoint takes no lock and writes nothing.
    /// </summary>
    [Authorize(Policy = Policies.Management)]
    [Route("DeactivationPreview")]
    public sealed class DeactivationPreviewController : ControllerBase<DeactivationPreviewController>
    {
        private readonly IRegistrationCancellationService _registrationCancellationService;

        public DeactivationPreviewController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IRegistrationCancellationService registrationCancellationService) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _registrationCancellationService = registrationCancellationService;
        }

        [HttpGet("{userId:int}")]
        public async Task<IActionResult> Index(int userId)
        {
            var rows = await _registrationCancellationService.PreviewUpcomingForUserAsync(userId, HttpContext.RequestAborted);
            return PartialView("_DeactivationPreview", rows);
        }
    }
}
