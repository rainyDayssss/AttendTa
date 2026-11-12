using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers;

/// <summary>Streams event covers from private storage after service-level event-state authorization.</summary>
[Authorize]
public sealed class EventCoverController : ControllerBase<EventCoverController>
{
    private readonly IEventLifecycleService _eventService;

    public EventCoverController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        IEventLifecycleService eventService)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _eventService = eventService;
    }

    [HttpGet]
    public async Task<IActionResult> Image(int id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        try
        {
            var cover = await _eventService.OpenCoverAsync(id, cancellationToken);
            return File(cover.Content, cover.ContentType, enableRangeProcessing: false);
        }
        catch (DomainException exception) when (exception.Outcome is OutcomeCode.NotFound or OutcomeCode.Forbidden)
        {
            return exception.Outcome == OutcomeCode.NotFound ? NotFound() : Forbid();
        }
    }
}
