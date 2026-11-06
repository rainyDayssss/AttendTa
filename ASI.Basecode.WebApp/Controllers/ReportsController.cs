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

/// <summary>Officer reports. The report surface ticket fills the per-event participation contract.</summary>
[Authorize(Policy = Policies.Management)]
public sealed class ReportsController : ControllerBase<ReportsController>
{
    public ReportsController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
    }

    [HttpGet]
    [Route("Reports/Participation")]
    public IActionResult Participation(int? eventId) => View(ParticipationReportViewModel.Empty());
}
