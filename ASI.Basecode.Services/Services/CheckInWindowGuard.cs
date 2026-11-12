#nullable enable

using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The published-event and open-window pre-check shared by the scan and manual check-in paths.
/// A draft, cancelled or completed event never matches the published read, so both paths refuse it
/// with the same message; the closed-window message stays with each caller.
/// </summary>
internal static class CheckInWindowGuard
{
    private const string EventClosedMessage = "Check-in is not available for this event.";

    /// <summary>
    /// Returns the closed outcome when the event is not published or its check-in window is shut,
    /// or <c>null</c> when the caller may continue.
    /// </summary>
    internal static async Task<CheckInOutcomeViewModel?> ClosedOutcomeAsync(
        IEventRepository events,
        int eventId,
        string windowMessage,
        CancellationToken cancellationToken = default)
    {
        var eventItem = await events.GetPublishedEventAsync(eventId, cancellationToken);
        if (eventItem == null)
        {
            return CheckInOutcomeViewModel.ClosedResult(EventClosedMessage);
        }

        return eventItem.IsCheckInOpen
            ? null
            : CheckInOutcomeViewModel.ClosedResult(windowMessage);
    }
}
