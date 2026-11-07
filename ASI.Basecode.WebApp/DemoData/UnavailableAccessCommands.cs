using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Registered for membership access when <c>DemoData:Enabled</c> is off. The access dialogs stay part
/// of the real screens, but the commands they post to return the standard refusal outcome instead of
/// pretending to work (register rule 2). Account access uses the real service; its reactivation stays
/// unavailable until 5.5. (Register M-14b, M-15a, M-15b.)
/// </summary>
public sealed class UnavailableAccessCommands : IAccountAccessCommands, IMembershipAccessCommands
{
    private const string Message = "This action is not available on this deployment.";

    public Task DeactivateAsync(int id, string reason, CancellationToken cancellationToken = default) => Refuse();

    public Task ReactivateAsync(int id, string reason, CancellationToken cancellationToken = default) => Refuse();

    private static Task Refuse() =>
        Task.FromException(new DomainException(OutcomeCode.Invalid, Message));
}
