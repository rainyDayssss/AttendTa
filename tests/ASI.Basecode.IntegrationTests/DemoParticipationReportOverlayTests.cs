using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.DemoData;
using Microsoft.Extensions.Options;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class DemoParticipationReportOverlayTests
{
    [Fact]
    public async Task DemoAttendanceDrivesValidCountAndRateForCompletedEvent()
    {
        const int registrationId = 11;
        const int eventId = 22;
        var now = DateTime.UtcNow;

        var store = new DemoStore(Options.Create(new DemoDataOptions { TokenKey = "test-demo-token" }));
        store.RecordAttendance(registrationId, eventId, "Demo Member", "S-0001", now, CheckInMethodCode.QrCode);
        store.MarkCompleted(eventId);

        var real = new ParticipationReportService(
            new FakeEvents(eventId),
            new FakeReportFacts(new ParticipationReportSummaryFacts(0, 1, 0, 0, 0, null)));
        var service = new DemoParticipationReportService(real, new FakeRegistrations(registrationId, eventId, now), store);

        var report = await service.GetReportAsync(eventId, null, TestContext.Current.CancellationToken);

        Assert.Equal(EventStatusCode.Completed, report.EventStatusId);
        Assert.True(report.IsFinal);
        Assert.Equal(1, report.ValidAttendeeCount);
        Assert.Equal(1m, report.AttendanceRate);
        Assert.Equal(0, report.NoShowCount);
        var row = Assert.Single(report.Rows);
        Assert.True(row.Attended);
    }

    [Fact]
    public async Task RealValidCountDrivesNoShowsWhenDemoStoreHoldsNoAttendance()
    {
        const int registrationId = 11;
        const int eventId = 22;
        var now = DateTime.UtcNow;

        var store = new DemoStore(Options.Create(new DemoDataOptions { TokenKey = "test-demo-token" }));
        store.MarkCompleted(eventId);

        var real = new ParticipationReportService(
            new FakeEvents(eventId),
            new FakeReportFacts(new ParticipationReportSummaryFacts(0, 3, 0, 0, 2, 2m / 3m)));
        var service = new DemoParticipationReportService(real, new FakeRegistrations(registrationId, eventId, now), store);

        var report = await service.GetReportAsync(eventId, null, TestContext.Current.CancellationToken);

        Assert.True(report.IsFinal);
        Assert.Equal(2, report.ValidAttendeeCount);
        Assert.Equal(1, report.NoShowCount);
    }

    private sealed class FakeEvents(int eventId) : IEventRepository
    {
        private readonly Event _event = new()
        {
            EventId = eventId,
            Title = "Demo Event",
            StartsAt = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc),
            EventStatusId = EventStatusCode.Published
        };

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventListItem>>([new EventListItem { EventId = eventId, Title = _event.Title }]);

        public Task<Event> GetEventAsync(int selectedEventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_event.EventId == selectedEventId ? _event : null!);

        public Task<Event> LockEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddEventAsync(Event eventItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateEventAsync(Event eventItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublishedEventListItem>> GetPublishedPageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublishedEventDetail> GetPublishedEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMemberVisibleAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublishedEventListItem>> GetMemberVisiblePageAsync(int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PublishedEventDetail> GetMemberVisibleEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeReportFacts(ParticipationReportSummaryFacts facts) : IParticipationReportRepository
    {
        public Task<ParticipationReportSummaryFacts> GetSummaryFactsAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(facts);
    }

    private sealed class FakeRegistrations(int registrationId, int eventId, DateTime submittedAt) : IRegistrationRepository
    {
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(
            RegistrationStatusCode status, int? selectedEventId, int offset, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RegistrationReviewRow>>(
                status == RegistrationStatusCode.Confirmed
                    ? [new RegistrationReviewRow(registrationId, eventId, "Demo Event", "Demo Member", "S-0001", submittedAt, RegistrationStatusCode.Confirmed)]
                    : []);

        public Task<bool> IsConfirmedAsync(int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Registration registration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Registration> GetForCancellationAsync(int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int userId, DateTime nowUtc, int? eventId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RegistrationReviewIdentity> DiscoverReviewAsync(int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasEligibleWaitlistAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceAsync(int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountReviewQueueAsync(RegistrationStatusCode status, int? eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
