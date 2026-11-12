using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Manual lookup and manual check-in by name or student number. The real
/// <see cref="Services.CheckInLookupService"/> serves both switch states: the search is a read-only,
/// server-paged read of the selected event's Confirmed registrations, and the explicit Check in runs
/// through the shared attendance recording engine.
/// </summary>
public interface ICheckInLookupService
{
    Task<CheckInLookupViewModel> SearchAsync(int eventId, string query, int? pageNumber, CancellationToken cancellationToken = default);

    Task<CheckInOutcomeViewModel> CheckInAsync(int eventId, int registrationId, string query, CancellationToken cancellationToken = default);
}
