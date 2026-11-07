using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Manual lookup and manual check-in by name or student number (register M-06). 4.5 replaces this
/// contract with the real registration-scoped search and manual check-in.
/// </summary>
public interface ICheckInLookupService
{
    Task<CheckInLookupViewModel> SearchAsync(int eventId, string query, int? pageNumber, CancellationToken cancellationToken = default);

    Task<CheckInOutcomeViewModel> CheckInAsync(int eventId, int registrationId, string query, CancellationToken cancellationToken = default);
}
