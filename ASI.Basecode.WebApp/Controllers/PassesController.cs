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

namespace ASI.Basecode.WebApp.Controllers;

/// <summary>
/// Member door passes. A separate controller from My Events: that host is owned by another surface,
/// and this route is the one 4.13 consumes. Only the actor's own confirmed registrations on
/// published events can produce a pass.
/// </summary>
[Authorize(Policy = Policies.Member)]
public sealed class PassesController : ControllerBase<PassesController>
{
    private readonly IPassService _passes;

    public PassesController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        IPassService passes)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _passes = passes;
    }

    [HttpGet]
    [Route("Passes")]
    [Route("Passes/Index")]
    public async Task<IActionResult> Index()
    {
        var model = await _passes.GetMyPassesAsync(HttpContext.RequestAborted);
        return View(model);
    }
}
