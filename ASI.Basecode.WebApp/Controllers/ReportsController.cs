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
/// Officer reports. The per-event participation report is a read-only Management surface; the real
/// <see cref="IParticipationReportService"/> answers it from canonical SQL in both demo switch states.
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
