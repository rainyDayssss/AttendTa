using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.DemoData;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = Policies.Management)]
    [AutoValidateAntiforgeryToken]
    public class MemberController : ControllerBase<MemberController>
    {
        private readonly IMemberService _memberService;
        private readonly IAccessDialogService _accessDialogService;
        private readonly IMembershipAccessCommands _membershipAccessCommands;
        private readonly IAccessStateOverlay _accessStateOverlay;

        private bool IsRecordDialog => Request.Headers["X-Record-Dialog"] == "true";

        private IActionResult Editor(MemberViewModel model)
        {
            return IsRecordDialog ? PartialView("_Editor", model) : View(model);
        }

        private IActionResult EditorError(DomainException exception, MemberViewModel model)
        {
            if (!IsRecordDialog) return HandleDomainException(exception, model, "Index");
            ModelState.AddModelError(exception.Field ?? string.Empty, exception.Message);
            return Editor(model);
        }

        public MemberController(IHttpContextAccessor httpContextAccessor,
                                ILoggerFactory loggerFactory,
                                IConfiguration configuration,
                                IMapper mapper,
                                IMemberService memberService,
                                IAccessDialogService accessDialogService,
                                IMembershipAccessCommands membershipAccessCommands,
                                IAccessStateOverlay accessStateOverlay) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _memberService = memberService;
            _accessDialogService = accessDialogService;
            _membershipAccessCommands = membershipAccessCommands;
            _accessStateOverlay = accessStateOverlay;
        }

        [HttpGet]
        [Route("Member")]
        [Route("Member/Index")]
        public async Task<IActionResult> Index(string search, string status, int? page)
        {
            ViewBag.Search = search;
            ViewBag.Status = status;

            try
            {
                var members = await _memberService.GetPageAsync(search, status, page, HttpContext.RequestAborted);
                return View(_accessStateOverlay.Apply(members));
            }
            catch (DomainException exception)
            {
                ViewData["ListError"] = exception.Message;
                return View(EmptyPage());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning("Member list read failed ({ExceptionType}).", exception.GetType().Name);
                ViewData["ListError"] = "The member list could not be loaded. Please try again.";
                return View(EmptyPage());
            }
        }

        private static PagedResult<MemberViewModel> EmptyPage() =>
            new PagedResult<MemberViewModel>(Array.Empty<MemberViewModel>(), 1, 0);

        [HttpGet]
        [Route("Member/{id:int}/access")]
        public async Task<IActionResult> Access(int id)
        {
            try
            {
                var model = await _accessDialogService.GetMembershipAccessAsync(id, HttpContext.RequestAborted);
                return PartialView("_AccessDialog", _accessStateOverlay.Apply(model));
            }
            catch (DomainException exception) when (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }
        }

        [HttpPost]
        [Route("Member/{id:int}/access/deactivate")]
        public async Task<IActionResult> Deactivate(int id, string reason)
        {
            try
            {
                await _membershipAccessCommands.DeactivateAsync(id, reason, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Membership deactivated.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("Member/{id:int}/access/reactivate")]
        public async Task<IActionResult> Reactivate(int id, string reason)
        {
            try
            {
                await _membershipAccessCommands.ReactivateAsync(id, reason, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Membership reactivated.";
            }
            catch (DomainException exception)
            {
                TempData["ErrorMessage"] = exception.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("Member/Edit/{id:int}")]
        public IActionResult Edit(int id)
        {
            var model = _memberService.GetMember(id);
            if (model == null)
            {
                if (IsRecordDialog) return NotFound();
                return RedirectToAction("Index");
            }

            return Editor(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MemberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                RestoreCanonicalEmail(model);
                return Editor(model);
            }

            try
            {
                await _memberService.UpdateMemberAsync(model, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Member updated successfully.";
                if (IsRecordDialog) return Json(new { saved = true });
                return RedirectToAction("Index");
            }
            catch (DomainException ex)
            {
                RestoreCanonicalEmail(model);
                return EditorError(ex, model);
            }
        }

        private void RestoreCanonicalEmail(MemberViewModel model)
        {
            model.Email = _memberService.GetMember(model.MemberId)?.Email;
            ModelState.Remove(nameof(MemberViewModel.Email));
        }
    }
}
