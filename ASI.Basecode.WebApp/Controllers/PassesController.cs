using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Mvc;
using ASI.Basecode.WebApp.Rendering;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Controllers;

/// <summary>
/// Member door passes. A separate controller from My Events: that host is owned by another surface,
/// and this route is the one 4.13 consumes. Only the actor's own confirmed registrations on
/// published events can produce a pass, and one authorized GET serves both the inline QR image and
/// its download.
/// </summary>
[Authorize(Policy = Policies.Member)]
public sealed class PassesController : ControllerBase<PassesController>
{
    private readonly IPassService _passes;
    private readonly IQrPassPngRenderer _renderer;

    public PassesController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        IPassService passes,
        IQrPassPngRenderer renderer)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _passes = passes;
        _renderer = renderer;
    }

    [HttpGet]
    [Route("Passes")]
    [Route("Passes/Index")]
    public async Task<IActionResult> Index()
    {
        var model = await _passes.GetMyPassesAsync(HttpContext.RequestAborted);
        return View(Decorate(model));
    }

    /// <summary>
    /// Streams the pass QR PNG in memory. The service authorizes the request and returns only the
    /// token; every refusal is the same 403 with the same body and no image bytes.
    /// </summary>
    [HttpGet]
    [Route("Passes/Qr/{id:int}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Qr(int id, bool download, CancellationToken cancellationToken)
    {
        try
        {
            var pass = await _passes.GetPassDownloadAsync(id, cancellationToken);
            var png = _renderer.Render(pass.QrToken);
            return download
                ? File(png, "image/png", pass.FileName)
                : File(png, "image/png");
        }
        catch (DomainException exception) when (exception.Outcome == OutcomeCode.Forbidden)
        {
            // One indistinguishable refusal: the same status, body and no image bytes.
            Response.StatusCode = StatusCodes.Status403Forbidden;
            Response.Headers.CacheControl = "no-store";
            return Content(PassService.UnavailableMessage, "text/plain; charset=utf-8");
        }
    }

    /// <summary>
    /// Fills the authorized image and download links. The token stays in the service, so the view
    /// only ever sees these URLs.
    /// </summary>
    private PassesViewModel Decorate(PassesViewModel model) => new()
    {
        Active = Decorate(model.Active),
        Past = Decorate(model.Past)
    };

    private System.Collections.Generic.IReadOnlyList<PassViewModel> Decorate(System.Collections.Generic.IReadOnlyList<PassViewModel> passes)
    {
        var decorated = new PassViewModel[passes.Count];
        for (var index = 0; index < passes.Count; index++)
        {
            var pass = passes[index];
            if (!pass.HasPass)
            {
                decorated[index] = pass;
                continue;
            }

            decorated[index] = new PassViewModel
            {
                RegistrationId = pass.RegistrationId,
                EventId = pass.EventId,
                EventTitle = pass.EventTitle,
                EventTypeName = pass.EventTypeName,
                VenueName = pass.VenueName,
                StartsAt = pass.StartsAt,
                HolderName = pass.HolderName,
                StudentNumber = pass.StudentNumber,
                StatusLabel = pass.StatusLabel,
                AccentClass = pass.AccentClass,
                GateCode = pass.GateCode,
                QrImageUrl = Url.Action(nameof(Qr), new { id = pass.RegistrationId }),
                DownloadUrl = Url.Action(nameof(Qr), new { id = pass.RegistrationId, download = true }),
                HasPass = true
            };
        }

        return decorated;
    }
}
