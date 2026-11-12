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

/// <summary>Administrator-only read-only activity history.</summary>
public sealed class ActivityLogController : ControllerBase<ActivityLogController>
{
    private readonly IActivityLogReaderService _activityLogReaderService;

    public ActivityLogController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        IActivityLogReaderService activityLogReaderService)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _activityLogReaderService = activityLogReaderService;
    }

    [HttpGet("/admin/auditlogs")]
    [Authorize(Policy = Policies.Administrator)]
    public async Task<IActionResult> Index(int? page, CancellationToken cancellationToken)
    {
        var result = await _activityLogReaderService.GetPageAsync(page, cancellationToken);
        return View(result);
    }
}
