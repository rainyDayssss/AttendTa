using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered when <c>DemoData:Enabled</c> is off. Invalidation is refused with the standard
/// outcome instead of pretending to work (register rule 2). (Register M-07.)
/// </summary>
public sealed class UnavailableAttendanceInvalidationService : IAttendanceInvalidationService
{
    private const string Message = "This action is not available on this deployment.";

    public Task InvalidateAsync(
        int eventId,
        int registrationId,
        string reason,
        CancellationToken cancellationToken = default) =>
        Task.FromException(new DomainException(OutcomeCode.Invalid, Message));
}
