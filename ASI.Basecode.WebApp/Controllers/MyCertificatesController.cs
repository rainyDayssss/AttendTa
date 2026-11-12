using System;
using System.Threading;
using System.Threading.Tasks;
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

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Policy = Policies.Member)]
public sealed class MyCertificatesController : ControllerBase<MyCertificatesController>
{
    private readonly IMyCertificatesService _certificates;

    public MyCertificatesController(IHttpContextAccessor accessor, ILoggerFactory loggerFactory,
        IConfiguration configuration, IMapper mapper, IMyCertificatesService certificates)
        : base(accessor, loggerFactory, configuration, mapper) => _certificates = certificates;

    [HttpGet]
    [Route("MyEvents/Certificates/{id:int}/Pdf")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Pdf(int id, bool download, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _certificates.GetPdfAsync(id, cancellationToken);
            return download
                ? File(document.Bytes, "application/pdf", $"{document.CertificateNumber}.pdf")
                : File(document.Bytes, "application/pdf");
        }
        catch (DomainException exception)
        {
            Response.StatusCode = exception.Outcome switch
            {
                OutcomeCode.NotFound => StatusCodes.Status404NotFound,
                OutcomeCode.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status409Conflict
            };
            ViewData["DocumentError"] = exception.Outcome is OutcomeCode.NotFound or OutcomeCode.Forbidden
                ? "This certificate is unavailable or you do not have access to it."
                : exception.Message;
            return View("Unavailable");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Certificate document read failed ({ExceptionType}).", exception.GetType().Name);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ViewData["DocumentError"] = "The certificate could not be loaded. Please return to your history and try again.";
            return View("Unavailable");
        }
    }
}
