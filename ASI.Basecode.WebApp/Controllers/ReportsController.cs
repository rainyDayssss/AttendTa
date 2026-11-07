using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers;

/// <summary>
/// Officer reports. The per-event participation report is a read-only Management surface; the
/// service contract hides whether the demo provider or the real 7.1–7.3 reads answer it.
/// </summary>
[Authorize(Policy = Policies.Management)]
public sealed class ReportsController : ControllerBase<ReportsController>
{
    private readonly IParticipationReportService _reports;

    public ReportsController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        IParticipationReportService reports)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _reports = reports;
    }

    [HttpGet]
    [Route("Reports/Participation")]
    public async Task<IActionResult> Participation(int? eventId, int? page, CancellationToken cancellationToken) =>
        View(await _reports.GetReportAsync(eventId, page, cancellationToken));
}
