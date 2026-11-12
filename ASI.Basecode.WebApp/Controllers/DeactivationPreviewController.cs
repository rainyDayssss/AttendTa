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
        private readonly IAccessDialogService _accessDialogService;

        public DeactivationPreviewController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IAccessDialogService accessDialogService) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _accessDialogService = accessDialogService;
        }

        [HttpGet("{userId:int}")]
        public async Task<IActionResult> Index(int userId)
        {
            try
            {
                // The preview read returns the account's authoritative identity and always lists the
                // upcoming registrations a deactivation would cancel, regardless of active state.
                var model = await _accessDialogService.GetDeactivationPreviewAsync(userId, HttpContext.RequestAborted);
                return PartialView("_DeactivationPreview", model);
            }
            catch (DomainException exception) when (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }
        }
    }
}
