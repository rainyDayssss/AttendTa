using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Authentication;
using ASI.Basecode.WebApp.Models;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    public class AccountController : ControllerBase<AccountController>
    {
        private readonly SignInManager _signInManager;
        private readonly IUserService _users;
        private readonly IAccountRequestService _requests;
        public AccountController(SignInManager signInManager, IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory, IConfiguration configuration, IMapper mapper,
            IUserService users, IAccountRequestService requests)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        { _signInManager = signInManager; _users = users; _requests = requests; }

        [HttpGet, AllowAnonymous]
        public IActionResult Login() => View(new LoginViewModel());

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);
            var user = _users.AuthenticateUser(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email or password is incorrect, or the account is not active.");
                return View(model);
            }
            await _signInManager.SignInAsync(user);
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet, AllowAnonymous]
        public IActionResult Register() => View(new AccountRequestViewModel());

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public IActionResult Register(AccountRequestViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                _requests.Submit(model);
                TempData["SuccessMessage"] = "Your account request was submitted for review.";
                return RedirectToAction(nameof(Login));
            }
            catch (InvalidDataException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
            catch (Exception) { ModelState.AddModelError(string.Empty, "The request could not be saved. Please try again."); }
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOutUser()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }
    }
}
