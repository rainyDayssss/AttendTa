using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers;

/// <summary>Door scanner screen. The scanner surface ticket fills the contract and the page.</summary>
[Authorize(Policy = Policies.Management)]
public sealed class CheckInController : ControllerBase<CheckInController>
{
    public CheckInController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
    }

    [HttpGet]
    [Route("CheckIn")]
    [Route("CheckIn/Index")]
    public IActionResult Index(int? eventId) => View(CheckInScreenViewModel.Empty());
}
