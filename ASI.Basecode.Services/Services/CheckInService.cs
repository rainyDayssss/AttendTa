using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The real door scanner (register M-05, removed by 4.3). The screen composes the published-event
/// read and the real attendance log; a scan normalizes the pass token, resolves it to a confirmed
/// registration of the event and delegates the validation and write to the shared
/// <see cref="IAttendanceRecordingService"/>. The screen and the outcome JSON keep their existing
/// shape; the controller is unchanged.
/// </summary>
public sealed class CheckInService(
    IEventRepository events,
    IRegistrationRepository registrations,
    IAttendanceRepository attendance,
    IAttendanceRecordingService recording) : ICheckInService
{
    private const string BlankTokenMessage = "Scan or enter a pass code to check in.";
    private const string InvalidPassMessage = "This pass is not valid for this event.";
    private const string EventClosedMessage = "Check-in is not available for this event.";
    private const string WindowClosedMessage = "Check-in is closed for this event. Open it before scanning.";

    public async Task<CheckInScreenViewModel> GetScreenAsync(int? eventId, CancellationToken cancellationToken = default)
    {
        var total = await events.CountPublishedAsync(cancellationToken);
        if (total == 0)
        {
            return CheckInScreenViewModel.Empty();
        }

        var published = await events.GetPublishedPageAsync(0, total, cancellationToken);
        if (published.Count == 0)
        {
            return CheckInScreenViewModel.Empty();
        }

        var options = published
            .Select(eventItem => new EventSelectorOption(eventItem.EventId, eventItem.Title))
            .ToList();

        var selected = SelectEvent(published, eventId);

        return new CheckInScreenViewModel
        {
            Selector = new EventSelectorViewModel
            {
                Label = "Event",
                Name = "eventId",
                SelectedEventId = selected.EventId,
                Options = options
            },
            EventId = selected.EventId,
            EventTitle = selected.Title,
            IsCheckInOpen = selected.IsCheckInOpen,
            CanChangeWindow = true,
            RecentResults = await RecentResultsAsync(selected.EventId, cancellationToken)
        };
    }

    public async Task<CheckInOutcomeViewModel> ScanAsync(int eventId, string token, CancellationToken cancellationToken = default)
    {
        var normalizedToken = token?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return CheckInOutcomeViewModel.InvalidResult(BlankTokenMessage);
        }

        // A draft, cancelled or completed event never matches the published read.
        var eventItem = await events.GetPublishedEventAsync(eventId, cancellationToken);
        if (eventItem == null)
        {
            return CheckInOutcomeViewModel.ClosedResult(EventClosedMessage);
        }

        if (!eventItem.IsCheckInOpen)
        {
            return CheckInOutcomeViewModel.ClosedResult(WindowClosedMessage);
        }

        // The pass PNG encodes the real registrations.qr_token, so the scan matches that stored
        // value for a confirmed registration of this event.
        var registrationId = await registrations.FindConfirmedRegistrationIdByTokenAsync(eventId, normalizedToken, cancellationToken);
        if (registrationId is not int id)
        {
            return CheckInOutcomeViewModel.InvalidResult(InvalidPassMessage);
        }

        var result = await recording.RecordAsync(eventId, id, CheckInMethodCode.QrCode, cancellationToken);
        return CheckInOutcomeViewModel.FromRecordingResult(result);
    }

    private async Task<IReadOnlyList<CheckInResultViewModel>> RecentResultsAsync(int eventId, CancellationToken cancellationToken)
    {
        var rows = await attendance.GetRecentForEventAsync(eventId, cancellationToken);
        return rows
            .Select(row => new CheckInResultViewModel
            {
                RegistrationId = row.RegistrationId,
                Outcome = row.IsValid ? "Recorded" : "Invalidated",
                MemberName = row.MemberName,
                StudentNumber = row.StudentNumber,
                RecordedAt = row.RecordedAt,
                Message = row.InvalidationReason,
                IsInvalidated = !row.IsValid,
                CanInvalidate = row.IsValid && !row.IsCertificateIssued
            })
            .ToList();
    }

    private static PublishedEventListItem SelectEvent(IReadOnlyList<PublishedEventListItem> published, int? requestedEventId)
    {
        if (requestedEventId is int requested)
        {
            var match = published.FirstOrDefault(eventItem => eventItem.EventId == requested);
            if (match != null)
            {
                return match;
            }
        }

        return published.FirstOrDefault(eventItem => eventItem.IsCheckInOpen) ?? published[0];
    }
}
