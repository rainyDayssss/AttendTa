using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ASI.Basecode.WebApp.Controllers
{
    public class VenueController : ControllerBase<VenueController>
    {
        private readonly IVenueService _venueService;

        public VenueController(IHttpContextAccessor httpContextAccessor,
                               ILoggerFactory loggerFactory,
                               IConfiguration configuration,
                               IMapper mapper,
                               IVenueService venueService) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _venueService = venueService;
        }

        public IActionResult Index()
        {
            var venues = _venueService.GetVenues();
            return View(venues);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new VenueViewModel());
        }

        [HttpPost]
        public IActionResult Create(VenueViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _venueService.AddVenue(model);
                TempData["SuccessMessage"] = "Venue added successfully.";
                return RedirectToAction("Index");
            }
            catch (InvalidDataException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var model = _venueService.GetVenue(id);
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            return View(model);
        }

        [HttpPost]
        public IActionResult Edit(VenueViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _venueService.UpdateVenue(model);
                TempData["SuccessMessage"] = "Venue updated successfully.";
                return RedirectToAction("Index");
            }
            catch (InvalidDataException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(model);
            }
        }
    }
}
