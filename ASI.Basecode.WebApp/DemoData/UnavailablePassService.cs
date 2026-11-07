using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered when <c>DemoData:Enabled</c> is off. The Door Passes screen keeps its normal shape and
/// shows its empty state instead of pretending a pass exists (register rule 2). (Register M-03.)
/// </summary>
public sealed class UnavailablePassService : IPassService
{
    public Task<PassesViewModel> GetMyPassesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(PassesViewModel.Empty());
}
