using System;
using System.Collections.Generic;
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
/// Officer certificate eligibility and issuance ledger plus its PDF viewer. The certificate
/// surface ticket fills the contracts and the pages.
/// </summary>
[Authorize(Policy = Policies.Management)]
public sealed class CertificateLedgerController : ControllerBase<CertificateLedgerController>
{
    public CertificateLedgerController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IMapper mapper)
        : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
    }

    [HttpGet]
    [Route("Certificates")]
    public IActionResult Index(int? eventId, string tab = null)
    {
        var activeTab = string.Equals(tab, "issued", StringComparison.OrdinalIgnoreCase)
            ? CertificateLedgerTab.Issued
            : CertificateLedgerTab.Eligibility;

        return View(new CertificateLedgerViewModel
        {
            ActiveTab = activeTab,
            Selector = new EventSelectorViewModel
            {
                SelectedEventId = eventId,
                HiddenValues = new Dictionary<string, string>
                {
                    ["tab"] = activeTab == CertificateLedgerTab.Issued ? "issued" : "eligibility"
                }
            }
        });
    }

    [HttpGet]
    [Route("Certificates/Viewer/{id:int}")]
    public IActionResult Viewer(int id) => View(CertificateViewerViewModel.Empty(id));
}
