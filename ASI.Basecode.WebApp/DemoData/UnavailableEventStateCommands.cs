using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered when <c>DemoData:Enabled</c> is off. The check-in and completion controls stay part of
/// the real screens, but the command they post to returns the standard refusal outcome instead of
/// pretending to work (register rule 2). (Register M-04, M-09.)
/// </summary>
public sealed class UnavailableEventStateCommands : IEventStateCommands
{
    private const string Message = "This action is not available on this deployment.";

    public Task OpenCheckInAsync(int eventId, CancellationToken cancellationToken = default) => Refuse();

    public Task CloseCheckInAsync(int eventId, CancellationToken cancellationToken = default) => Refuse();

    public Task CompleteAsync(int eventId, CancellationToken cancellationToken = default) => Refuse();

    private static Task Refuse() =>
        Task.FromException(new DomainException(OutcomeCode.Invalid, Message));
}
