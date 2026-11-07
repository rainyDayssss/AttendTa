using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo door scanner (register M-05). The screen composes the existing published-event read and the
/// in-memory store; a scan matches the real <c>registrations.qr_token</c> of a confirmed registration,
/// reloads the real registration status, and applies the same refusal rules 4.3 will (register
/// rule 8). Nothing here writes SQL, a stored file or the Activity Log.
/// </summary>
public sealed class DemoCheckInService : ICheckInService
{
    private const int LookupLimit = 500;
    private const int RecentLogLimit = 25;

    private readonly IEventRepository _events;
    private readonly IRegistrationRepository _registrations;
    private readonly DemoStore _store;
    private readonly TimeProvider _timeProvider;

    public DemoCheckInService(
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

    public async Task<CheckInScreenViewModel> GetScreenAsync(int? eventId, CancellationToken cancellationToken = default)
    {
        var total = await _events.CountPublishedAsync(cancellationToken);
        if (total == 0)
        {
            return CheckInScreenViewModel.Empty();
        }

        var published = await _events.GetPublishedPageAsync(0, total, cancellationToken);
        if (published.Count == 0)
        {
            return CheckInScreenViewModel.Empty();
        }

        var options = published
            .Select(eventItem => new EventSelectorOption(eventItem.EventId, eventItem.Title))
            .ToList();

        var selected = SelectEvent(published, eventId);
        var isOpen = _store.IsCheckInOpen(selected.EventId);

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
            IsCheckInOpen = isOpen,
            CanChangeWindow = true,
            RecentResults = RecentResults(selected.EventId)
        };
    }

    public async Task<CheckInOutcomeViewModel> ScanAsync(int eventId, string token, CancellationToken cancellationToken = default)
    {
        var normalizedToken = token?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            return CheckInOutcomeViewModel.InvalidResult("Scan or enter a pass code to check in.");
        }

        // A draft, cancelled or completed event never matches the published read.
        var eventItem = await _events.GetPublishedEventAsync(eventId, cancellationToken);
        if (eventItem == null)
        {
            return CheckInOutcomeViewModel.ClosedResult("Check-in is not available for this event.");
        }

        if (!_store.IsCheckInOpen(eventId))
        {
            return CheckInOutcomeViewModel.ClosedResult("Check-in is closed for this event. Open it before scanning.");
        }

        // The pass PNG encodes the real registrations.qr_token, so the scan matches that stored
        // value for a confirmed registration of this event (register rule 12 hand-over from M-03).
        var matchId = await _registrations.FindConfirmedRegistrationIdByTokenAsync(eventId, normalizedToken, cancellationToken);
        if (matchId is not int registrationId)
        {
            return CheckInOutcomeViewModel.InvalidResult("This pass is not valid for this event.");
        }

        // Reload the real status so a registration cancelled after the pass was printed is refused.
        if (!await _registrations.IsConfirmedAsync(registrationId, cancellationToken))
        {
            return CheckInOutcomeViewModel.InvalidResult("This registration is not confirmed for this event.");
        }

        // Identity for the outcome panel and the log comes from the same confirmed row.
        var row = (await _registrations.GetReviewQueueAsync(
                RegistrationStatusCode.Confirmed, eventId, 0, LookupLimit, cancellationToken))
            .First(candidate => candidate.RegistrationId == registrationId);

        return Record(row.RegistrationId, eventId, row.MemberName, row.StudentNumber, CheckInMethodCode.QrCode);
    }

    private CheckInOutcomeViewModel Record(
        int registrationId,
        int eventId,
        string memberName,
        string studentNumber,
        CheckInMethodCode method)
    {
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

        var recorded = _store.RecordAttendance(
            registrationId,
            eventId,
            memberName,
            studentNumber,
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

    private IReadOnlyList<CheckInResultViewModel> RecentResults(int eventId) =>
        _store.GetAttendanceForEvent(eventId)
            .Take(RecentLogLimit)
            .Select(record => new CheckInResultViewModel
            {
                RegistrationId = record.RegistrationId,
                Outcome = record.IsValid ? "Recorded" : "Invalidated",
                MemberName = record.MemberName,
                StudentNumber = record.StudentNumber,
                RecordedAt = record.RecordedAt,
                Message = record.InvalidationReason,
                IsInvalidated = !record.IsValid,
                CanInvalidate = record.IsValid && !_store.IsCertificateIssued(record.RegistrationId)
            })
            .ToList();

    private PublishedEventListItem SelectEvent(IReadOnlyList<PublishedEventListItem> published, int? requestedEventId)
    {
        if (requestedEventId is int requested)
        {
            var match = published.FirstOrDefault(eventItem => eventItem.EventId == requested);
            if (match != null)
            {
                return match;
            }
        }

        return published.FirstOrDefault(eventItem => IsOpen(eventItem.EventId)) ?? published[0];
    }

    private bool IsOpen(int eventId) => _store.IsCheckInOpen(eventId);
}
