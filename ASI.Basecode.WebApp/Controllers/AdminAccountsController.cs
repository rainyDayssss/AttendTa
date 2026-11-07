using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Administrator-only account console: the read-only list, role assignment and account access.
    /// </summary>
    [Authorize(Policy = Policies.Administrator)]
    [AutoValidateAntiforgeryToken]
    public class AdminAccountsController : ControllerBase<AdminAccountsController>
    {
        private readonly IAccountAdministrationService _accountAdministrationService;
        private readonly IAccountRoleService _accountRoleService;
        private readonly IAccessDialogService _accessDialogService;
        private readonly IAccountAccessCommands _accountAccessCommands;

        public AdminAccountsController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IAccountAdministrationService accountAdministrationService,
            IAccountRoleService accountRoleService,
            IAccessDialogService accessDialogService,
            IAccountAccessCommands accountAccessCommands) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _accountAdministrationService = accountAdministrationService;
            _accountRoleService = accountRoleService;
            _accessDialogService = accessDialogService;
            _accountAccessCommands = accountAccessCommands;
        }

        [HttpGet]
        [Route("admin/accounts")]
        public async Task<IActionResult> Index(string search = null, int? page = null)
        {
            ViewBag.Search = search;
            var result = await _accountAdministrationService.GetPageAsync(search, page, HttpContext.RequestAborted);
            return View(result);
        }

        [HttpGet]
        [Route("admin/accounts/{id:int}/access")]
        public async Task<IActionResult> Access(int id)
        {
            try
            {
                var model = await _accessDialogService.GetAccountAccessAsync(id, HttpContext.RequestAborted);
                return PartialView("_AccessDialog", model);
            }
            catch (DomainException exception) when (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }
        }

        [HttpPost]
        [Route("admin/accounts/{id:int}/access/deactivate")]
        public async Task<IActionResult> Deactivate(int id, string reason)
        {
            try
            {
                await _accountAccessCommands.DeactivateAsync(id, reason, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Account deactivated.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("admin/accounts/{id:int}/access/reactivate")]
        public async Task<IActionResult> Reactivate(int id, string reason)
        {
            try
            {
                await _accountAccessCommands.ReactivateAsync(id, reason, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Account reactivated.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("admin/accounts/{id:int}/role")]
        public async Task<IActionResult> ChangeRole(int id)
        {
            try
            {
                var model = await _accountRoleService.GetRoleChangeAsync(id, HttpContext.RequestAborted);
                ViewBag.Roles = ToSelectList(model.Roles);
                return View(model);
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [Route("admin/accounts/{id:int}/role")]
        public async Task<IActionResult> ChangeRole(int id, ChangeAccountRoleViewModel model)
        {
            // The route owns the target account; a posted account id can never retarget the write.
            model.AccountId = id;

            if (!ModelState.IsValid)
            {
                return await PopulateRoleFormAsync(model) ? View(model) : RedirectToAction(nameof(Index));
            }

            try
            {
                await _accountRoleService.ChangeRoleAsync(model, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Role updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DomainException exception)
            {
                if (!await PopulateRoleFormAsync(model))
                {
                    return RedirectToAction(nameof(Index));
                }

                return HandleDomainException(exception, model, nameof(Index));
            }
        }

        /// <summary>
        /// Restores the display fields and role choices after a failed POST without replacing the
        /// submitted role or reason. Returns false when the account no longer exists.
        /// </summary>
        private async Task<bool> PopulateRoleFormAsync(ChangeAccountRoleViewModel model)
        {
            try
            {
                var current = await _accountRoleService.GetRoleChangeAsync(model.AccountId, HttpContext.RequestAborted);
                model.FullName = current.FullName;
                model.Email = current.Email;
                model.CurrentRoleCode = current.CurrentRoleCode;
                model.CurrentRoleLabel = current.CurrentRoleLabel;
                model.Roles = current.Roles;
                ViewBag.Roles = ToSelectList(current.Roles);
                return true;
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
                return false;
            }
        }

        private static List<SelectListItem> ToSelectList(IReadOnlyList<RoleOption> roles) =>
            roles.Select(role => new SelectListItem(role.Label, role.Code)).ToList();
    }
}
