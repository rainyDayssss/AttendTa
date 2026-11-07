using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo manual lookup and manual check-in (register M-06). The search is a bounded read of the
/// event's confirmed registrations, filtered by name or student number in memory and paged at the
/// shared fixed page size. The explicit Check in action applies the same refusal rules as a scan
/// (register rule 8) and records <c>student_number</c> or <c>manual_search</c>. Searching or
/// viewing never records attendance, and nothing here writes SQL, a stored file or the Activity Log.
/// </summary>
public sealed class DemoCheckInLookupService : ICheckInLookupService
{
    private const int LookupLimit = 500;

    private readonly IEventRepository _events;
    private readonly IRegistrationRepository _registrations;
    private readonly DemoStore _store;
    private readonly TimeProvider _timeProvider;

    public DemoCheckInLookupService(
        IEventRepository events,
        IRegistrationRepository registrations,
        DemoStore store,
        TimeProvider timeProvider)
    {
        _events = events;
        _registrations = registrations;
        _store = store;
        _timeProvider = timeProvider;
    }

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

        var confirmed = await _registrations.GetReviewQueueAsync(
            RegistrationStatusCode.Confirmed, eventId, 0, LookupLimit, cancellationToken);

        var matches = confirmed
            .Where(row => Contains(row.MemberName, trimmed) || Contains(row.StudentNumber, trimmed))
            .OrderBy(row => row.MemberName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.RegistrationId)
            .ToList();

        var request = new PageRequest(pageNumber);
        var page = request.NormalizePageNumber(matches.Count);
        var rows = matches
            .Skip(request.GetOffset(page))
            .Take(PageRequest.FixedPageSize)
            .Select(row => Row(row))
            .ToList();

        return new CheckInLookupViewModel
        {
            Query = trimmed,
            Rows = rows,
            PageNumber = page,
            TotalCount = matches.Count
        };
    }

    public async Task<CheckInOutcomeViewModel> CheckInAsync(
        int eventId,
        int registrationId,
        string query,
        CancellationToken cancellationToken = default)
    {
        // A draft, cancelled or completed event never matches the published read.
        var eventItem = await _events.GetPublishedEventAsync(eventId, cancellationToken);
        if (eventItem == null)
        {
            return CheckInOutcomeViewModel.ClosedResult("Check-in is not available for this event.");
        }

        if (!_store.IsCheckInOpen(eventId))
        {
            return CheckInOutcomeViewModel.ClosedResult("Check-in is closed for this event. Open it before checking in.");
        }

        // The fresh confirmed read is also the event-match and current-status check: a cancelled
        // registration or a registration from another event is not in this list.
        var confirmed = await _registrations.GetReviewQueueAsync(
            RegistrationStatusCode.Confirmed, eventId, 0, LookupLimit, cancellationToken);
        var row = confirmed.FirstOrDefault(candidate => candidate.RegistrationId == registrationId);
        if (row == null)
        {
            return CheckInOutcomeViewModel.InvalidResult("This participant is not confirmed for this event.");
        }

        if (_store.TryGetAttendance(registrationId, out var existing))
        {
            if (!existing.IsValid)
            {
                return CheckInOutcomeViewModel.InvalidResult(
                    "This attendance was invalidated and cannot be checked in again.");
            }

            return new CheckInOutcomeViewModel
            {
                Outcome = CheckInOutcomeViewModel.Repeat,
                Message = "Already checked in. This is the original time.",
                MemberName = existing.MemberName,
                StudentNumber = existing.StudentNumber,
                RecordedAt = existing.RecordedAt,
                RegistrationId = existing.RegistrationId
            };
        }

        var method = string.Equals(query?.Trim(), row.StudentNumber, StringComparison.OrdinalIgnoreCase)
            ? CheckInMethodCode.StudentNumber
            : CheckInMethodCode.ManualSearch;

        var recorded = _store.RecordAttendance(
            registrationId,
            eventId,
            row.MemberName,
            row.StudentNumber,
            _timeProvider.GetUtcNow().UtcDateTime,
            method);

        return new CheckInOutcomeViewModel
        {
            Outcome = CheckInOutcomeViewModel.Success,
            Message = "Check-in recorded.",
            MemberName = recorded.MemberName,
            StudentNumber = recorded.StudentNumber,
            RecordedAt = recorded.RecordedAt,
            RegistrationId = recorded.RegistrationId
        };
    }

    private CheckInLookupRowViewModel Row(RegistrationReviewRow row)
    {
        var hasAttendance = _store.TryGetAttendance(row.RegistrationId, out var attendance);
        return new CheckInLookupRowViewModel
        {
            RegistrationId = row.RegistrationId,
            MemberName = row.MemberName,
            StudentNumber = row.StudentNumber,
            IsCheckedIn = hasAttendance && attendance.IsValid,
            IsInvalidated = hasAttendance && !attendance.IsValid
        };
    }

    private static bool Contains(string value, string query) =>
        !string.IsNullOrEmpty(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
