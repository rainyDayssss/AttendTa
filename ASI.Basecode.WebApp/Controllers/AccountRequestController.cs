using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = "admin,officer")]
    public class AccountRequestController : ControllerBase<AccountRequestController>
    {
        private readonly IAccountRequestService _requests;
        public AccountRequestController(IAccountRequestService requests, IHttpContextAccessor accessor,
            ILoggerFactory loggerFactory, IConfiguration configuration, IMapper mapper)
            : base(accessor, loggerFactory, configuration, mapper) { _requests = requests; }

        public IActionResult Index(int page = 1)
        {
            var result = _requests.GetPending(page);
            ViewBag.Page = Math.Max(1, page);
            ViewBag.TotalPages = result.TotalPages;
            return View(result.Items);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Approve(int id)
        {
            try { _requests.Approve(id, GetActorUserId()); TempData["SuccessMessage"] = "Request approved."; }
            catch (InvalidDataException ex) { TempData["ErrorMessage"] = ex.Message; }
            catch (Exception) { TempData["ErrorMessage"] = "The request could not be approved."; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Reject(AccountRequestReviewViewModel model)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.ReviewNotes))
            {
                TempData["ErrorMessage"] = "A rejection reason is required.";
                return RedirectToAction(nameof(Index));
            }
            try { _requests.Reject(model.RequestId, GetActorUserId(), model.ReviewNotes); TempData["SuccessMessage"] = "Request rejected."; }
            catch (InvalidDataException ex) { TempData["ErrorMessage"] = ex.Message; }
            catch (Exception) { TempData["ErrorMessage"] = "The request could not be rejected."; }
            return RedirectToAction(nameof(Index));
        }
    }
}
