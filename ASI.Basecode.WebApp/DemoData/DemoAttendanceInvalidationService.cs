using System;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Demo attendance invalidation (register M-07). The reason is required, the registration must
/// belong to the event, and an issued certificate refuses the change; a pending or approved review
/// becomes not eligible with that reason through the store (register rules 4, 5 and 8). Nothing
/// here writes SQL, a stored file or the Activity Log.
/// </summary>
public sealed class DemoAttendanceInvalidationService : IAttendanceInvalidationService
{
    private readonly IRegistrationRepository _registrations;
    private readonly DemoStore _store;
    private readonly TimeProvider _timeProvider;

    public DemoAttendanceInvalidationService(
        IRegistrationRepository registrations,
        DemoStore store,
        TimeProvider timeProvider)
    {
        _registrations = registrations;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task InvalidateAsync(
        int eventId,
        int registrationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(OutcomeCode.Invalid, "An invalidation reason is required.", "reason");
        }

        var registration = await _registrations.DiscoverReviewAsync(registrationId, cancellationToken);
        if (registration == null || registration.EventId != eventId)
        {
            throw new DomainException(OutcomeCode.NotFound, "Attendance not found for this event.");
        }

        if (_store.IsCertificateIssued(registrationId))
        {
            throw new DomainException(
                OutcomeCode.Invalid,
                "A certificate has already been issued for this attendance.");
        }

        _store.InvalidateAttendance(registrationId, reason, _timeProvider.GetUtcNow().UtcDateTime);
    }
}
