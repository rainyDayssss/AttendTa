using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services;

/// <summary>
/// The real manual lookup and manual check-in (register M-06, removed by 4.5). The search is a
/// registration-scoped, server-paged read of the selected event's Confirmed registrations matching a
/// name or student number substring; it never writes. The explicit Check in pre-checks the published
/// event and open window, reads the row's student number to choose <c>student_number</c> or
/// <c>manual_search</c>, then delegates every validation and the write to the shared
/// <see cref="IAttendanceRecordingService"/>, so the scan and manual paths cannot drift.
/// </summary>
public sealed class CheckInLookupService(
    IEventRepository events,
    IRegistrationRepository registrations,
    IAttendanceRecordingService recording) : ICheckInLookupService
{
    private const string EventClosedMessage = "Check-in is not available for this event.";
    private const string WindowClosedMessage = "Check-in is closed for this event. Open it before checking in.";
    private const string NotConfirmedMessage = "This participant is not confirmed for this event.";

    public async Task<CheckInLookupViewModel> SearchAsync(
        int eventId,
        string query,
        int? pageNumber,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return CheckInLookupViewModel.Empty();
        }

        var total = await registrations.CountConfirmedLookupAsync(eventId, trimmed, cancellationToken);
        if (total == 0)
        {
            return new CheckInLookupViewModel { Query = trimmed };
        }

        var request = new PageRequest(pageNumber);
        var page = request.NormalizePageNumber(total);
        var rows = await registrations.GetConfirmedLookupPageAsync(
            eventId,
            trimmed,
            request.GetOffset(page),
            PageRequest.FixedPageSize,
            cancellationToken);

        return new CheckInLookupViewModel
        {
            Query = trimmed,
            Rows = rows
                .Select(row => new CheckInLookupRowViewModel
                {
                    RegistrationId = row.RegistrationId,
                    MemberName = row.MemberName,
                    StudentNumber = row.StudentNumber,
                    IsCheckedIn = row.HasAttendance && !row.IsAttendanceInvalidated,
                    IsInvalidated = row.IsAttendanceInvalidated
                })
                .ToList(),
            PageNumber = page,
            TotalCount = total
        };
    }

    public async Task<CheckInOutcomeViewModel> CheckInAsync(
        int eventId,
        int registrationId,
        string query,
        CancellationToken cancellationToken = default)
    {
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

        // The confirmed read is also the event-match and current-status check: a cancelled
        // registration or a registration from another event has no student number here.
        var studentNumber = await registrations.GetConfirmedStudentNumberAsync(eventId, registrationId, cancellationToken);
        if (studentNumber == null)
        {
            return CheckInOutcomeViewModel.InvalidResult(NotConfirmedMessage);
        }

        var method = string.Equals(query?.Trim(), studentNumber, StringComparison.OrdinalIgnoreCase)
            ? CheckInMethodCode.StudentNumber
            : CheckInMethodCode.ManualSearch;

        var result = await recording.RecordAsync(eventId, registrationId, method, cancellationToken);
        return CheckInOutcomeViewModel.FromRecordingResult(result);
    }
}
