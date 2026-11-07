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
        var service = new DemoParticipationReportService(
            real, new FakeRegistrations(registrationId, eventId, now), new FakeAttendance(), store);

        var report = await service.GetReportAsync(eventId, null, TestContext.Current.CancellationToken);

        Assert.Equal(EventStatusCode.Completed, report.EventStatusId);
        Assert.True(report.IsFinal);
        Assert.Equal(1, report.ValidAttendeeCount);
        Assert.Equal(1m, report.AttendanceRate);
        Assert.Equal(0, report.NoShowCount);
        var row = Assert.Single(report.Rows);
        Assert.True(row.Attended);

        // The demo store holds attendance, so the overlay keeps its attendance-derived breakdowns.
        var course = Assert.Single(report.CourseBreakdown);
        Assert.Equal("Unspecified", course.Label);
        Assert.Equal(1, course.Count);
        Assert.Equal(100, course.Percent);
        var year = Assert.Single(report.YearBreakdown);
        Assert.Equal("Unspecified", year.Label);
        Assert.Equal(1, year.Count);
        Assert.Equal(100, year.Percent);
    }

    [Fact]
    public async Task RealNoShowsAndBreakdownsPassThroughWhenDemoStoreHoldsNoAttendance()
    {
        const int registrationId = 11;
        const int eventId = 22;
        var now = DateTime.UtcNow;

        var store = new DemoStore(Options.Create(new DemoDataOptions { TokenKey = "test-demo-token" }));
        store.MarkCompleted(eventId);

        var real = new ParticipationReportService(
            new FakeEvents(eventId),
            new FakeReportFacts(
                new ParticipationReportSummaryFacts(0, 3, 0, 0, 2, 2m / 3m),
                0,
                [new CourseDistributionRow("BSIT", 1), new CourseDistributionRow("BSED", 1)],
                [new YearDistributionRow(2, 1), new YearDistributionRow(1, 1)]));
        var service = new DemoParticipationReportService(real, new FakeRegistrations(registrationId, eventId, now), new FakeAttendance(), store);

        var report = await service.GetReportAsync(eventId, null, TestContext.Current.CancellationToken);

        Assert.True(report.IsFinal);
        Assert.Equal(2, report.ValidAttendeeCount);
        // Confirmed - Valid would be 1 here, so a 0 proves the real no-show fact passed through.
        Assert.Equal(0, report.NoShowCount);

        // With no demo attendance the real no-shows and snapshot breakdowns stand unchanged.
        Assert.Equal(new[] { "BSED", "BSIT" }, report.CourseBreakdown.Select(row => row.Label));
        Assert.Equal(new[] { 1, 1 }, report.CourseBreakdown.Select(row => row.Count));
        Assert.Equal(new[] { 50, 50 }, report.CourseBreakdown.Select(row => row.Percent));
        Assert.Equal(new[] { "Year 1", "Year 2" }, report.YearBreakdown.Select(row => row.Label));
        Assert.Equal(new[] { 1, 1 }, report.YearBreakdown.Select(row => row.Count));
        Assert.Equal(new[] { 50, 50 }, report.YearBreakdown.Select(row => row.Percent));
    }

    [Fact]
    public async Task RealInvalidatedRowOverridesTheSeededStoreRow()
    {
        const int registrationId = 11;
        const int eventId = 22;
        var now = DateTime.UtcNow;

        var store = new DemoStore(Options.Create(new DemoDataOptions { TokenKey = "test-demo-token" }));
        store.RecordAttendance(registrationId, eventId, "Demo Member", "S-0001", now.AddMinutes(-30), CheckInMethodCode.QrCode);
        store.MarkCompleted(eventId);

        // The real facts still claim one valid attendee; the overlay must let the real invalidated
        // row win over the seeded store row and derive its own counts from the merged map.
        var real = new ParticipationReportService(
            new FakeEvents(eventId),
            new FakeReportFacts(new ParticipationReportSummaryFacts(0, 1, 0, 0, 1, 1m)));
        var service = new DemoParticipationReportService(
            real,
            new FakeRegistrations(registrationId, eventId, now),
            new FakeAttendance(new EventAttendanceRow(
                registrationId, now.AddMinutes(-30), now.AddMinutes(-5), "Pass could not be verified.")),
            store);

        var report = await service.GetReportAsync(eventId, null, TestContext.Current.CancellationToken);

        Assert.True(report.IsFinal);
        Assert.Equal(0, report.ValidAttendeeCount);
        Assert.Equal(0m, report.AttendanceRate);
        Assert.Equal(1, report.NoShowCount);
        Assert.Empty(report.CourseBreakdown);
        Assert.Empty(report.YearBreakdown);

        var row = Assert.Single(report.Rows);
        Assert.False(row.Attended);
        Assert.True(row.IsNoShow);
        Assert.Equal("Invalidated", row.AttendanceLabel);
        Assert.Equal(now.AddMinutes(-30), row.CheckedInAt);
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

    private sealed class FakeReportFacts(
        ParticipationReportSummaryFacts facts,
        int noShowCount = 0,
        IReadOnlyList<CourseDistributionRow>? courses = null,
        IReadOnlyList<YearDistributionRow>? years = null) : IParticipationReportRepository
    {
        public Task<ParticipationReportSummaryFacts> GetSummaryFactsAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(facts);

        public Task<int> GetNoShowCountAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(noShowCount);

        public Task<ParticipationReportDistributionFacts> GetAttendeeDistributionAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ParticipationReportDistributionFacts(courses ?? [], years ?? []));
    }

    private sealed class FakeAttendance(params EventAttendanceRow[] rows) : IAttendanceRepository
    {
        public Task<IReadOnlyList<EventAttendanceRow>> GetForEventAsync(int eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventAttendanceRow>>(rows);

        public Task<Attendance> LockAttendanceAsync(int attendanceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Attendance> GetByRegistrationIdAsync(int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Attendance attendance, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AttendanceLogRow>> GetRecentForEventAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> GetValidAttendedRegistrationIdsAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMyAttendanceAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyAttendanceRow>> GetMyAttendanceAsync(int userId, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
        public Task<int> CountConfirmedLookupAsync(int eventId, string query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConfirmedLookupRow>> GetConfirmedLookupPageAsync(int eventId, string query, int skip, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetConfirmedStudentNumberAsync(int eventId, int registrationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
