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
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = Policies.Member)]
    public class ProfileController : ControllerBase<ProfileController>
    {
        private readonly IMemberService _memberService;

        public ProfileController(IHttpContextAccessor httpContextAccessor,
                                 ILoggerFactory loggerFactory,
                                 IConfiguration configuration,
                                 IMapper mapper,
                                 IMemberService memberService) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _memberService = memberService;
        }

        [HttpGet]
        [Route("Profile")]
        [Route("Profile/Index")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var profile = await _memberService.GetSelfProfileAsync(HttpContext.RequestAborted);
                return View(profile);
            }
            catch (DomainException exception) when (exception.Outcome == OutcomeCode.NotFound)
            {
                return NotFound();
            }
        }
    }
}
