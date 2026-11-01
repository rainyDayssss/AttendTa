using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class EventTypeService : IEventTypeService
    {
        private readonly IEventTypeRepository _repository;
        private readonly IMapper _mapper;

        public EventTypeService(IEventTypeRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public List<EventTypeViewModel> GetEventTypes()
        {
            var eventTypes = _repository.GetEventTypes().OrderBy(t => t.TypeName).ToList();
            return _mapper.Map<List<EventTypeViewModel>>(eventTypes);
        }

        public EventTypeViewModel GetEventType(int id)
        {
            var eventType = _repository.GetEventType(id);
            return eventType == null ? null : _mapper.Map<EventTypeViewModel>(eventType);
        }

        public void AddEventType(EventTypeViewModel model)
        {
            if (_repository.TypeNameExists(model.TypeName))
            {
                throw new InvalidDataException("Event type name already exists.");
            }

            var eventType = new EventType();
            _mapper.Map(model, eventType);
            _repository.AddEventType(eventType);
        }

        public void UpdateEventType(EventTypeViewModel model)
        {
            var eventType = _repository.GetEventType(model.TypeId);
            if (eventType == null)
            {
                throw new InvalidDataException("Event type not found.");
            }

            if (_repository.TypeNameExists(model.TypeName, model.TypeId))
            {
                throw new InvalidDataException("Event type name already exists.");
            }

            _mapper.Map(model, eventType);
            _repository.UpdateEventType(eventType);
        }
    }
}
