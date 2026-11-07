using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Seeds the demo store at start-up from rows already in the connected database (register M-01;
/// register rule 5). It only composes existing repository reads and never writes a SQL row, a
/// stored file or an Activity Log entry. The ready-to-complete event keeps its attendance
/// and certificate data so the demo completes it live through the real service. Missing rows are
/// logged, not fatal, so the app still starts on a sparsely populated database.
/// </summary>
public sealed class DemoSeedService : IHostedService
{
    private const int MaxSeededCheckIns = 8;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DemoStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DemoSeedService> _logger;

    public DemoSeedService(
        IServiceScopeFactory scopeFactory,
        DemoStore store,
        TimeProvider timeProvider,
        ILogger<DemoSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var registrations = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();

            var total = await events.CountAsync(cancellationToken);
            if (total == 0)
            {
                _logger.LogInformation("Demo data seeded nothing: the connected database has no events.");
                return;
            }

            var rows = await events.GetPageAsync(0, total, cancellationToken);
            var confirmed = await registrations.GetReviewQueueAsync(
                RegistrationStatusCode.Confirmed, null, 0, 500, cancellationToken);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var published = rows
                .Where(row => row.EventStatusId == EventStatusCode.Published)
                .OrderBy(row => row.StartsAt)
                .ThenBy(row => row.EventId)
                .ToList();
            if (published.Count == 0)
            {
                _logger.LogInformation("Demo data seeded nothing: the connected database has no published events.");
                return;
            }

            var confirmedByEvent = confirmed
                .GroupBy(row => row.EventId)
                .ToDictionary(group => group.Key, group => group.OrderBy(row => row.RegistrationId).ToList());
            var started = published.Where(row => row.StartsAt <= now).ToList();

            // The ready-to-complete event is the started one with the most confirmed registrations so
            // the attendance and certificate screens open populated; ties break on the lowest event
            // id. It stays Published in SQL so the demo completes it live through the real service.
            var readyToComplete = started
                .OrderByDescending(row => confirmedByEvent.TryGetValue(row.EventId, out var list) ? list.Count : 0)
                .ThenBy(row => row.EventId)
                .FirstOrDefault();

            if (readyToComplete != null)
            {
                SeedReadyToCompleteEvent(readyToComplete.EventId, readyToComplete.Title, readyToComplete.StartsAt, confirmedByEvent);
            }

            _logger.LogInformation(
                "Demo data active (M-01, M-07). Ready to complete event {ReadyToCompleteEventId}.",
                readyToComplete?.EventId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Do not log the exception: provider messages can carry connection details.
            _logger.LogWarning(
                "Demo data seed could not read the connected database ({ExceptionType}); demo screens stay empty.",
                exception.GetType().Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SeedReadyToCompleteEvent(
        int eventId,
        string eventTitle,
        DateTime startsAt,
        IReadOnlyDictionary<int, List<RegistrationReviewRow>> confirmedByEvent)
    {
        if (!confirmedByEvent.TryGetValue(eventId, out var confirmed) || confirmed.Count == 0)
        {
            return;
        }

        var recorded = new List<int>();
        foreach (var (row, index) in confirmed.Take(MaxSeededCheckIns).Select((row, index) => (row, index)))
        {
            _store.RecordAttendance(
                row.RegistrationId,
                eventId,
                row.MemberName,
                row.StudentNumber,
                startsAt.AddMinutes(5 + index),
                CheckInMethodCode.QrCode);
            recorded.Add(row.RegistrationId);
        }

        // One check-in is invalidated so the attendance and review screens show a correction.
        int? invalidatedRegistrationId = recorded.Count >= 2 ? recorded[^1] : null;
        if (invalidatedRegistrationId is int invalidated)
        {
            const string reason = "Pass could not be verified at the door.";
            _store.InvalidateAttendance(invalidated, reason, startsAt.AddHours(1));
            _store.SeedReview(invalidated, eventId, CertificateStatusCode.NotEligible, reason, startsAt.AddHours(1));
        }

        var valid = recorded.Where(registrationId => registrationId != invalidatedRegistrationId).ToList();
        _store.SeedPendingReviewsForEvent(eventId, startsAt.AddHours(2));

        // A pending, a declined and an issued certificate, so every review state opens populated.
        if (valid.Count >= 1)
        {
            _store.SeedIssuedCertificate(
                valid[0],
                eventId,
                CertificateNumber(startsAt, valid[0]),
                startsAt.AddDays(1),
                new CertificatePdfSnapshot(confirmed.First(row => row.RegistrationId == valid[0]).MemberName,
                    eventTitle, startsAt, startsAt.AddDays(1), CertificateNumber(startsAt, valid[0])));
        }

        if (valid.Count >= 2)
        {
            _store.SeedReview(valid[1], eventId, CertificateStatusCode.PendingApproval, null, startsAt.AddHours(2));
        }

        if (valid.Count >= 3)
        {
            _store.SeedReview(
                valid[2],
                eventId,
                CertificateStatusCode.NotEligible,
                "Attendance did not meet the participation requirement.",
                startsAt.AddHours(3));
        }
    }

    private static string CertificateNumber(DateTime startsAt, int registrationId) =>
        $"AT-{startsAt:yyyy}-{registrationId:D6}";
}
