using ASI.Basecode.Services.ServiceModels;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Participant-facing event browsing, registration submission and member self-cancellation.
    /// Any authenticated role may browse; registration additionally requires an eligible member
    /// profile. Cancellation withdraws the actor's own registration before the Event Start Cutoff.
    /// </summary>
    public interface IMemberEventService
    {
        Task<PagedResult<MemberEventListItemViewModel>> GetFeedAsync(int? pageNumber, CancellationToken cancellationToken = default);

        /// <summary>Returns <c>null</c> for an event that does not exist or is not member-visible.</summary>
        Task<MemberEventDetailViewModel> GetDetailAsync(int eventId, CancellationToken cancellationToken = default);

        Task<MemberRegistrationResult> RegisterAsync(int eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels the actor's own pending, confirmed or waitlisted registration for a published
        /// event strictly before the Event Start Cutoff. No reason is required from the member.
        /// </summary>
        Task CancelRegistrationAsync(int eventId, CancellationToken cancellationToken = default);
    }
}
