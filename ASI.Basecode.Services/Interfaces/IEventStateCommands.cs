using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

/// <summary>
/// Event check-in window and completion commands. The demo provider implements this contract now;
/// 4.2 (check-in window) and 3.12 (completion) implement the real one behind the same contract and
/// raise the same <see cref="ASI.Basecode.Resources.DomainException"/> outcomes.
/// </summary>
public interface IEventStateCommands
{
    /// <summary>Opens the check-in window of a published event.</summary>
    Task OpenCheckInAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Closes the check-in window of a published event.</summary>
    Task CloseCheckInAsync(int eventId, CancellationToken cancellationToken = default);

    /// <summary>Completes a published event whose start time has passed, closing check-in.</summary>
    Task CompleteAsync(int eventId, CancellationToken cancellationToken = default);
}
