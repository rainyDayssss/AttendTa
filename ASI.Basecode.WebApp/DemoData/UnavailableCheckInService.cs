using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered when <c>DemoData:Enabled</c> is off. The scanner keeps its normal shape and shows its
/// empty state, and a scan is refused rather than pretending to record (register rule 2).
/// (Register M-05.)
/// </summary>
public sealed class UnavailableCheckInService : ICheckInService
{
    private const string Message = "This action is not available on this deployment.";

    public Task<CheckInScreenViewModel> GetScreenAsync(int? eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CheckInScreenViewModel.Empty());

    public Task<CheckInOutcomeViewModel> ScanAsync(int eventId, string token, CancellationToken cancellationToken = default) =>
        Task.FromException<CheckInOutcomeViewModel>(new DomainException(OutcomeCode.Invalid, Message));
}
