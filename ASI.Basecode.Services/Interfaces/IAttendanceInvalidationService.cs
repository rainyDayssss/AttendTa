using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Attendance invalidation with a reason (R25). The real command runs in one transaction that
/// locks the participant's <c>users</c> row, the event and the attendance row, stamps the retained
/// row and appends one <c>attendance.invalidated</c> audit entry. Management only.
/// </summary>
public interface IAttendanceInvalidationService
{
    Task InvalidateAsync(int eventId, int registrationId, string reason, CancellationToken cancellationToken = default);
}
