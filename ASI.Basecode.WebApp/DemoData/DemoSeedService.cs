using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.DemoData;

/// <summary>
/// Seeds the demo event's attendance and certificate reviews as real SQL rows at start-up (register
/// M-01; 6.6 hand-over). The ready-to-complete event stays Published in SQL so the demo completes it
/// live through the real service, whose completion hook then finds the seeded reviews and leaves
/// them untouched. Existing attendance and decisions are never overwritten, so a restart keeps them.
/// The still-demo issued document (M-11a) is seeded into <see cref="DemoStore"/> only when the real
/// review is approved. Missing rows are logged, not fatal, so the app still starts on a sparse
/// database.
/// </summary>
public sealed class DemoSeedService : IHostedService
{
    private const int MaxSeededCheckIns = 8;
    private const string InvalidatedReason = "Pass could not be verified at the door.";
    private const string DeclinedReason = "Attendance did not meet the participation requirement.";

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
            var attendance = scope.ServiceProvider.GetRequiredService<IAttendanceRepository>();
            var certificates = scope.ServiceProvider.GetRequiredService<ICertificateRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

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
            if (readyToComplete is null)
            {
                _logger.LogInformation("Demo data seeded nothing: no started published event is available.");
                return;
            }

            var eventItem = await events.GetEventAsync(readyToComplete.EventId, cancellationToken);
            if (eventItem is null)
            {
                _logger.LogInformation("Demo data seeded nothing: the ready-to-complete event could not be read.");
                return;
            }

            await unitOfWork.ExecuteInTransactionAsync(
                token => SeedEventAsync(eventItem, confirmedByEvent, attendance, certificates, unitOfWork, token),
                cancellationToken);

            _logger.LogInformation(
                "Demo data active (M-01, M-11a). Ready to complete event {ReadyToCompleteEventId}.",
                readyToComplete.EventId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Do not log the exception: provider messages can carry connection details.
            _logger.LogWarning(
                "Demo data seed could not write the demo attendance and reviews ({ExceptionType}); demo screens stay empty.",
                exception.GetType().Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedEventAsync(
        Event eventItem,
        IReadOnlyDictionary<int, List<RegistrationReviewRow>> confirmedByEvent,
        IAttendanceRepository attendance,
        ICertificateRepository certificates,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (!confirmedByEvent.TryGetValue(eventItem.EventId, out var confirmed) || confirmed.Count == 0)
        {
            return;
        }

        var recorded = confirmed.Take(MaxSeededCheckIns).ToList();
        var invalidatedRegistrationId = recorded.Count >= 2 ? recorded[^1].RegistrationId : (int?)null;

        // Attendance is a real SQL row from 6.6, so the real completion hook and the real invalidation
        // rules see it. A repeat start never rewrites an existing row.
        var attendanceByRegistration = new Dictionary<int, Attendance>();
        var index = 0;
        foreach (var row in recorded)
        {
            var existing = await attendance.GetByRegistrationIdAsync(row.RegistrationId, cancellationToken);
            if (existing is null)
            {
                var record = new Attendance
                {
                    RegistrationId = row.RegistrationId,
                    CheckInMethodId = CheckInMethodCode.QrCode,
                    RecordedBy = eventItem.CreatedBy,
                    RecordedAt = eventItem.StartsAt.AddMinutes(5 + index)
                };
                if (row.RegistrationId == invalidatedRegistrationId)
                {
                    record.InvalidatedBy = eventItem.CreatedBy;
                    record.InvalidatedAt = eventItem.StartsAt.AddHours(1);
                    record.InvalidationReason = InvalidatedReason;
                }

                await attendance.AddAsync(record, cancellationToken);
                attendanceByRegistration[row.RegistrationId] = record;
            }
            else
            {
                attendanceByRegistration[row.RegistrationId] = existing;
            }

            index++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Valid Attendance is the actual SQL row, not the seed's intent: an attendee whose row was
        // already invalidated never receives the approved or issued state.
        var valid = recorded
            .Where(row => attendanceByRegistration[row.RegistrationId].InvalidatedAt is null)
            .ToList();
        var known = new Dictionary<int, Certificate>();
        var staged = new List<Certificate>();
        foreach (var row in recorded)
        {
            var existing = await certificates.GetByRegistrationIdAsync(row.RegistrationId, cancellationToken);
            if (existing is not null)
            {
                known[row.RegistrationId] = existing;
                continue;
            }

            var certificate = new Certificate
            {
                RegistrationId = row.RegistrationId,
                CertificateStatusId = CertificateStatusCode.PendingApproval,
                CreatedBy = eventItem.CreatedBy,
                CreatedAt = eventItem.StartsAt.AddHours(2)
            };
            staged.Add(certificate);
            known[row.RegistrationId] = certificate;
        }

        // Every attendee whose actual attendance row is invalidated opens as not eligible with that
        // reason, whether the seed invalidated it or an earlier run did. A decision made in an earlier
        // session is never overwritten because existing certificate rows are not staged.
        foreach (var row in recorded)
        {
            var attendanceRow = attendanceByRegistration[row.RegistrationId];
            if (attendanceRow.InvalidatedAt is null || !staged.Contains(known[row.RegistrationId]))
            {
                continue;
            }

            var certificate = known[row.RegistrationId];
            certificate.CertificateStatusId = CertificateStatusCode.NotEligible;
            certificate.ReviewNotes = string.IsNullOrWhiteSpace(attendanceRow.InvalidationReason)
                ? InvalidatedReason
                : attendanceRow.InvalidationReason;
            certificate.UpdatedBy = attendanceRow.InvalidatedBy ?? eventItem.CreatedBy;
            certificate.UpdatedAt = attendanceRow.InvalidatedAt.Value;
        }

        if (valid.Count >= 3 && staged.Contains(known[valid[2].RegistrationId]))
        {
            var certificate = known[valid[2].RegistrationId];
            certificate.CertificateStatusId = CertificateStatusCode.NotEligible;
            certificate.ReviewNotes = DeclinedReason;
            certificate.ReviewedBy = eventItem.CreatedBy;
            certificate.ReviewedAt = eventItem.StartsAt.AddHours(3);
            certificate.UpdatedBy = eventItem.CreatedBy;
            certificate.UpdatedAt = eventItem.StartsAt.AddHours(3);
        }

        var issuedAt = eventItem.StartsAt.AddDays(1);
        var issuedNumber = valid.Count >= 1
            ? CertificateNumber(eventItem.StartsAt, valid[0].RegistrationId)
            : string.Empty;

        if (valid.Count >= 1 && staged.Contains(known[valid[0].RegistrationId]))
        {
            var certificate = known[valid[0].RegistrationId];
            certificate.CertificateStatusId = CertificateStatusCode.Issued;
            certificate.ReviewedBy = eventItem.CreatedBy;
            certificate.ReviewedAt = eventItem.StartsAt.AddHours(2);
            certificate.CertificateNumber = issuedNumber;
            certificate.RecipientNameSnapshot = valid[0].MemberName;
            certificate.EventTitleSnapshot = eventItem.Title;
            certificate.EventStartsAtSnapshot = eventItem.StartsAt;
            certificate.IssuedBy = eventItem.CreatedBy;
            certificate.IssuedAt = issuedAt;
            certificate.UpdatedBy = eventItem.CreatedBy;
            certificate.UpdatedAt = issuedAt;
        }

        if (staged.Count > 0)
        {
            await certificates.AddRangeAsync(staged, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // M-11a: the issued document is still demo state. Seed it only when the real review is
        // issued, so a decision made in an earlier session is never overwritten.
        if (valid.Count >= 1
            && known[valid[0].RegistrationId].CertificateStatusId == CertificateStatusCode.Issued
            && !_store.IsCertificateIssued(valid[0].RegistrationId))
        {
            _store.SeedIssuedCertificate(
                valid[0].RegistrationId,
                eventItem.EventId,
                issuedNumber,
                issuedAt,
                new CertificatePdfSnapshot(
                    valid[0].MemberName,
                    eventItem.Title,
                    eventItem.StartsAt,
                    issuedAt,
                    issuedNumber));
        }
    }

    private static string CertificateNumber(DateTime startsAt, int registrationId) =>
        $"AT-{startsAt:yyyy}-{registrationId:D6}";
}
