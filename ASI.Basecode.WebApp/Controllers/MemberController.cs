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
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = Policies.Management)]
    [AutoValidateAntiforgeryToken]
    public class MemberController : ControllerBase<MemberController>
    {
        private readonly IMemberService _memberService;

        public MemberController(IHttpContextAccessor httpContextAccessor,
                                ILoggerFactory loggerFactory,
                                IConfiguration configuration,
                                IMapper mapper,
                                IMemberService memberService) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _memberService = memberService;
        }

        [HttpGet]
        [Route("Member")]
        [Route("Member/Index")]
        public async Task<IActionResult> Index(string search, string status, int? page)
        {
            var members = await _memberService.GetPageAsync(search, status, page, HttpContext.RequestAborted);
            ViewBag.Search = search;
            ViewBag.Status = status;
            return View(members);
        }

        [HttpGet]
        [Route("Member/{id:int}/access")]
        public IActionResult Access(int id) => PartialView("_AccessDialog", MembershipAccessViewModel.Empty(id));

        [HttpGet]
        [Route("Member/Edit/{id:int}")]
        public IActionResult Edit(int id)
        {
            var model = _memberService.GetMember(id);
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MemberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                RestoreCanonicalEmail(model);
                return View(model);
            }

            try
            {
                await _memberService.UpdateMemberAsync(model, HttpContext.RequestAborted);
                TempData["SuccessMessage"] = "Member updated successfully.";
                return RedirectToAction("Index");
            }
            catch (DomainException ex)
            {
                RestoreCanonicalEmail(model);
                return HandleDomainException(ex, model, "Index");
            }
        }

        private void RestoreCanonicalEmail(MemberViewModel model)
        {
            model.Email = _memberService.GetMember(model.MemberId)?.Email;
            ModelState.Remove(nameof(MemberViewModel.Email));
        }
    }
}
