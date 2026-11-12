using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
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
    private readonly IMyEventsService _myEvents;
    private readonly IMyCertificatesService _certificates;

    public MyEventsController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper,
        IMyEventsService myEvents,
        IMyCertificatesService certificates)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _myEvents = myEvents;
        _certificates = certificates;
    }

    [HttpGet]
    [Route("MyEvents")]
    public IActionResult Index() => RedirectToAction(nameof(Registrations));

    [HttpGet]
    [Route("MyEvents/Registrations")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Registrations(int? page, CancellationToken cancellationToken) =>
        View("Index", MyEventsViewModel.FromRegistrations(
            await _myEvents.GetRegistrationsAsync(page, cancellationToken)));

    [HttpGet]
    [Route("MyEvents/Attendance")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Attendance(int? page, CancellationToken cancellationToken) =>
        View("Index", MyEventsViewModel.FromAttendance(
            await _myEvents.GetAttendanceAsync(page, cancellationToken)));

    [HttpGet]
    [Route("MyEvents/Certificates")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Certificates(int? page, CancellationToken cancellationToken)
    {
        try
        {
            return View("Index", MyEventsViewModel.FromCertificates(
                await _certificates.GetPageAsync(page, cancellationToken)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Certificate history read failed ({ExceptionType}).", exception.GetType().Name);
            ViewData["CertificateHistoryError"] = "Your certificate history could not be loaded. Please try again.";
            return View("Index", MyEventsViewModel.Empty(MyEventsTab.Certificates));
        }
    }
}
