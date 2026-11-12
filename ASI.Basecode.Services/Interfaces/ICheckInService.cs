using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Door scanner screen data and scan check-in. The real <c>CheckInService</c> reads the published
/// event and the real attendance log, and delegates every scan to the shared
/// <see cref="IAttendanceRecordingService"/>.
/// </summary>
public interface ICheckInService
{
    Task<CheckInScreenViewModel> GetScreenAsync(int? eventId, CancellationToken cancellationToken = default);

    Task<CheckInOutcomeViewModel> ScanAsync(int eventId, string token, CancellationToken cancellationToken = default);
}
