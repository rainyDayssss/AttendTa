using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// The member Participation History reads. Registrations and attendance each use a real
/// owner-scoped cross-event query behind the same contract, so the controller and views do not
/// change.
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

    /// <summary>
    /// Returns the actor's own attendance records, newest recorded first, one shared page of at
    /// most <see cref="PageRequest.FixedPageSize"/> rows, with the method label and the
    /// invalidated state already resolved.
    /// </summary>
    Task<PagedResult<MyEventAttendanceRowViewModel>> GetAttendanceAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default);
}
