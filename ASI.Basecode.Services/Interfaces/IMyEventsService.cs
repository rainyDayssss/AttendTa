using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// The member Participation History registrations read. The demo provider composes the existing
/// per-event reads now (register M-02); 3.13 implements the real cross-event owner query behind
/// this same contract, so the controller and views do not change.
/// </summary>
public interface IMyEventsService
{
    /// <summary>
    /// Returns the actor's own registrations across events, newest start first, one shared page
    /// of at most <see cref="PageRequest.FixedPageSize"/> cards, with the status label and badge
    /// already resolved for the current time.
    /// </summary>
    Task<PagedResult<MyEventRegistrationCardViewModel>> GetRegistrationsAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default);
}
