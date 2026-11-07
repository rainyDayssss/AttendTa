using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Attendance invalidation with a reason (register M-07). 4.6 replaces this contract with the real
/// audited invalidation and its certificate-review hook.
/// </summary>
public interface IAttendanceInvalidationService
{
    Task InvalidateAsync(int eventId, int registrationId, string reason, CancellationToken cancellationToken = default);
}
