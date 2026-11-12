using AutoMapper;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Queries;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.DependencyInjection;

namespace ASI.Basecode.WebApp
{
    // AutoMapper configuration
    internal partial class StartupConfigurer
    {
        /// <summary>
        /// Configure auto mapper
        /// </summary>
        private void ConfigureAutoMapper()
        {
            var mapperConfiguration = new MapperConfiguration(config =>
            {
                config.AddProfile(new AutoMapperProfileConfiguration());
            });

            this._services.AddSingleton<IMapper>(sp => mapperConfiguration.CreateMapper());
        }

        private class AutoMapperProfileConfiguration : Profile
        {
            public AutoMapperProfileConfiguration()
            {
                CreateMap<EventType, EventTypeViewModel>();
                CreateMap<Venue, VenueViewModel>();
                CreateMap<EventViewModel, Event>();
                CreateMap<Event, EventViewModel>();
                CreateMap<EventListItem, EventViewModel>();
                CreateMap<PublishedEventListItem, MemberEventListItemViewModel>();
                CreateMap<PublishedEventDetail, MemberEventDetailViewModel>();

                // Participation History rows. The status label depends on the per-row start cutoff,
                // which the service passes as a mapping item, and the pass slot is filled by the pass
                // surface, so the map never invents one.
                CreateMap<MyEventRegistrationRow, MyEventRegistrationCardViewModel>()
                    .ForMember(destination => destination.StatusLabel, options => options.MapFrom(
                        (source, destination, member, context) => RegistrationStatusDisplay.Label(
                            source.Status,
                            (bool)context.Items[MyEventsMappingItems.CutoffPassed])))
                    .ForMember(destination => destination.StatusBadgeClass, options => options.MapFrom(
                        (source, destination, member, context) => RegistrationStatusDisplay.BadgeClass(
                            source.Status,
                            (bool)context.Items[MyEventsMappingItems.CutoffPassed])))
                    .ForMember(destination => destination.CanViewEvent, options => options.MapFrom(source =>
                        source.EventStatusId == EventStatusCode.Published
                        || source.EventStatusId == EventStatusCode.Completed))
                    .ForMember(destination => destination.Pass, options => options.Ignore());

                CreateMap<MyAttendanceRow, MyEventAttendanceRowViewModel>()
                    .ForMember(destination => destination.CheckInMethodLabel, options => options.MapFrom(source =>
                        CheckInMethodDisplay.Label(source.CheckInMethod)))
                    .ForMember(destination => destination.IsInvalidated, options => options.MapFrom(source =>
                        !ParticipationFacts.IsValidAttendance(source.InvalidatedAt)));
            }
        }
    }
}
