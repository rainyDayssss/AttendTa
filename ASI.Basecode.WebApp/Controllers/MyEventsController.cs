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

/// <summary>
/// Member Participation History host. One action per tab keeps each tab's model loading isolated
/// so a surface ticket changes only its own action.
/// </summary>
[Authorize(Policy = Policies.Member)]
public sealed class MyEventsController : ControllerBase<MyEventsController>
{
    public MyEventsController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
    }

    [HttpGet]
    [Route("MyEvents")]
    public IActionResult Index() => RedirectToAction(nameof(Registrations));

    [HttpGet]
    [Route("MyEvents/Registrations")]
    public IActionResult Registrations() => View("Index", MyEventsViewModel.Empty(MyEventsTab.Registrations));

    [HttpGet]
    [Route("MyEvents/Attendance")]
    public IActionResult Attendance() => View("Index", MyEventsViewModel.Empty(MyEventsTab.Attendance));

    [HttpGet]
    [Route("MyEvents/Certificates")]
    public IActionResult Certificates() => View("Index", MyEventsViewModel.Empty(MyEventsTab.Certificates));
}
