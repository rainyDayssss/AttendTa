using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered when <c>DemoData:Enabled</c> is off so My Events renders its normal empty state and
/// nothing breaks (register rule 2). (Register M-02.)
/// </summary>
public sealed class EmptyMyEventsService : IMyEventsService
{
    public Task<PagedResult<MyEventRegistrationCardViewModel>> GetRegistrationsAsync(
        int? pageNumber,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<MyEventRegistrationCardViewModel>(
            Array.Empty<MyEventRegistrationCardViewModel>(),
            1,
            0));
}
