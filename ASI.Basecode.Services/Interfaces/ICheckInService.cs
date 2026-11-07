using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Door scanner screen data and scan check-in (register M-05). 4.3 replaces this contract with the
/// real validation engine and attendance write.
/// </summary>
public interface ICheckInService
{
    Task<CheckInScreenViewModel> GetScreenAsync(int? eventId, CancellationToken cancellationToken = default);

    Task<CheckInOutcomeViewModel> ScanAsync(int eventId, string token, CancellationToken cancellationToken = default);
}
