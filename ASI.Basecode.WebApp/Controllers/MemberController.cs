using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ASI.Basecode.WebApp.Controllers
{
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

        public IActionResult Index(string search)
        {
            var members = _memberService.GetMembers(search);
            ViewBag.Search = search;
            return View(members);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new MemberViewModel());
        }

        [HttpPost]
        public IActionResult Create(MemberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _memberService.AddMember(model);
                TempData["SuccessMessage"] = "Member added successfully.";
                return RedirectToAction("Index");
            }
            catch (InvalidDataException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }

        [HttpGet]
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
        public IActionResult Edit(MemberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _memberService.UpdateMember(model);
                TempData["SuccessMessage"] = "Member updated successfully.";
                return RedirectToAction("Index");
            }
            catch (InvalidDataException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }

        [HttpPost]
        public IActionResult ToggleStatus(int id)
        {
            _memberService.ToggleStatus(id);
            TempData["SuccessMessage"] = "Member status updated.";
            return RedirectToAction("Index");
        }
    }
}
