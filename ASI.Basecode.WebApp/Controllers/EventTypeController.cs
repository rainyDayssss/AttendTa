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
    public class EventTypeController : ControllerBase<EventTypeController>
    {
        private readonly IEventTypeService _eventTypeService;

        public EventTypeController(IHttpContextAccessor httpContextAccessor,
                                   ILoggerFactory loggerFactory,
                                   IConfiguration configuration,
                                   IMapper mapper,
                                   IEventTypeService eventTypeService) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            _eventTypeService = eventTypeService;
        }

        public IActionResult Index()
        {
            var eventTypes = _eventTypeService.GetEventTypes();
            return View(eventTypes);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new EventTypeViewModel());
        }

        [HttpPost]
        public IActionResult Create(EventTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _eventTypeService.AddEventType(model);
                TempData["SuccessMessage"] = "Event type added successfully.";
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
            var model = _eventTypeService.GetEventType(id);
            if (model == null)
            {
                return RedirectToAction("Index");
            }

            return View(model);
        }

        [HttpPost]
        public IActionResult Edit(EventTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _eventTypeService.UpdateEventType(model);
                TempData["SuccessMessage"] = "Event type updated successfully.";
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
