using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered when <c>DemoData:Enabled</c> is off. The manual lookup keeps its normal empty state
/// and the explicit check-in is refused (register rule 2). (Register M-06.)
/// </summary>
public sealed class UnavailableCheckInLookupService : ICheckInLookupService
{
    private const string Message = "This action is not available on this deployment.";

    public Task<CheckInLookupViewModel> SearchAsync(
        int eventId,
        string query,
        int? pageNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CheckInLookupViewModel.Empty());

    public Task<CheckInOutcomeViewModel> CheckInAsync(
        int eventId,
        int registrationId,
        string query,
        CancellationToken cancellationToken = default) =>
        Task.FromException<CheckInOutcomeViewModel>(new DomainException(OutcomeCode.Invalid, Message));
}
