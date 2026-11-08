using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public sealed class ParticipationReportServiceTests
{
    [Fact]
    public async Task CompletedEventMapsSummaryFactsAndEmptyRepositoryReturnsNoRows()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Completed" });
        events.Items.Add(new EventListItem { EventId = 2, Title = "Other" });
        var summary = new FakeReportFacts(new ParticipationReportSummaryFacts(2, 5, 1, 3, 4, 0.8m));
        var service = new ParticipationReportService(events, summary);

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, report.EventId);
        Assert.Equal("Event", report.EventTitle);
        Assert.Equal(EventStatusCode.Completed, report.EventStatusId);
        Assert.Equal("Completed", report.EventStatusLabel);
        Assert.Equal("badge-completed", report.EventStatusBadgeClass);
        Assert.True(report.IsFinal);
        Assert.Equal(2, report.PendingCount);
        Assert.Equal(5, report.ConfirmedCount);
        Assert.Equal(1, report.WaitlistedCount);
        Assert.Equal(3, report.CancelledCount);
        Assert.Equal(4, report.ValidAttendeeCount);
        Assert.Equal(0.8m, report.AttendanceRate);
        Assert.Equal(0, report.NoShowCount);
        Assert.Empty(report.Rows);
        Assert.Empty(report.CourseBreakdown);
        Assert.Empty(report.YearBreakdown);
        Assert.Equal(1, report.PageNumber);
        Assert.Equal(0, report.TotalCount);
        Assert.Equal(2, report.Selector.Options.Count);
        Assert.Equal(1, report.Selector.SelectedEventId);
        Assert.Equal(1, summary.Calls);
        Assert.Equal(1, summary.NoShowCalls);
        Assert.Equal(1, summary.DistributionCalls);
        Assert.Equal(1, summary.LastEventId);
    }

    [Fact]
    public async Task CompletedEventMapsNoShowsAndSignupSnapshotBreakdowns()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Completed" });
        var courses = new[]
        {
            new CourseDistributionRow("BSIT", 2),
            new CourseDistributionRow("bsit", 1),
            new CourseDistributionRow("BSED", 1),
            new CourseDistributionRow("   ", 1)
        };
        var years = new[]
        {
            new YearDistributionRow(1, 2),
            new YearDistributionRow(3, 1),
            new YearDistributionRow(1, 1)
        };
        var summary = new FakeReportFacts(
            new ParticipationReportSummaryFacts(0, 5, 0, 0, 5, 1m),
            1,
            courses,
            years);
        var service = new ParticipationReportService(events, summary);

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, report.NoShowCount);
        Assert.Equal(1, summary.NoShowCalls);
        Assert.Equal(1, summary.DistributionCalls);

        // Course labels are trimmed and grouped case-insensitively, largest share first then label.
        Assert.Equal(new[] { "BSIT", "BSED", "Unspecified" }, report.CourseBreakdown.Select(row => row.Label));
        Assert.Equal(new[] { 3, 1, 1 }, report.CourseBreakdown.Select(row => row.Count));
        Assert.Equal(new[] { 60, 20, 20 }, report.CourseBreakdown.Select(row => row.Percent));

        // Year labels order by the snapshot year; the schema forbids a zero year, so there is no
        // "Unspecified" year group.
        Assert.Equal(new[] { "Year 1", "Year 3" }, report.YearBreakdown.Select(row => row.Label));
        Assert.Equal(new[] { 3, 1 }, report.YearBreakdown.Select(row => row.Count));
        Assert.Equal(new[] { 75, 25 }, report.YearBreakdown.Select(row => row.Percent));
    }

    [Fact]
    public async Task ZeroAttendeesProducesEmptyBreakdowns()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Empty" });
        var service = new ParticipationReportService(
            events,
            new FakeReportFacts(new ParticipationReportSummaryFacts(0, 2, 0, 0, 0, 0m), 2));

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, report.NoShowCount);
        Assert.Empty(report.CourseBreakdown);
        Assert.Empty(report.YearBreakdown);
    }

    [Theory]
    [InlineData(EventStatusCode.Draft)]
    [InlineData(EventStatusCode.Published)]
    [InlineData(EventStatusCode.Cancelled)]
    public async Task NonCompletedEventIsNotFinalAndKeepsItsStatus(EventStatusCode status)
    {
        var events = new FakeEvents { EventItem = Event(1, status) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Event" });
        var service = new ParticipationReportService(
            events,
            new FakeReportFacts(new ParticipationReportSummaryFacts(0, 2, 0, 0, 1, 0.5m)));

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.False(report.IsFinal);
        Assert.Equal(status, report.EventStatusId);
        Assert.Equal(0.5m, report.AttendanceRate);
    }

    [Fact]
    public async Task ZeroConfirmedKeepsAttendanceRateNull()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Empty" });
        var service = new ParticipationReportService(
            events,
            new FakeReportFacts(new ParticipationReportSummaryFacts(0, 0, 0, 0, 0, null)));

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.True(report.IsFinal);
        Assert.Equal(0, report.ConfirmedCount);
        Assert.Equal(0, report.ValidAttendeeCount);
        Assert.Null(report.AttendanceRate);
    }

    [Fact]
    public async Task NoSelectionReturnsSelectorAndEmptyReportWithoutReadingFacts()
    {
        var events = new FakeEvents();
        events.Items.Add(new EventListItem { EventId = 1, Title = "First" });
        var summary = new FakeReportFacts(new ParticipationReportSummaryFacts(0, 0, 0, 0, 0, null));
        var service = new ParticipationReportService(events, summary);

        var report = await service.GetReportAsync(null, 2, TestContext.Current.CancellationToken);

        Assert.Null(report.EventId);
        Assert.Null(report.EventStatusId);
        Assert.False(report.IsFinal);
        Assert.Single(report.Selector.Options);
        Assert.Null(report.Selector.SelectedEventId);
        Assert.False(events.EventLookupUsed);
        Assert.Equal(0, summary.Calls);
        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task UnknownEventReturnsSelectorAndEmptyReportWithoutReadingFacts()
    {
        var events = new FakeEvents();
        events.Items.Add(new EventListItem { EventId = 1, Title = "First" });
        var summary = new FakeReportFacts(new ParticipationReportSummaryFacts(0, 0, 0, 0, 0, null));
        var service = new ParticipationReportService(events, summary);

        var report = await service.GetReportAsync(99, null, TestContext.Current.CancellationToken);

        Assert.Null(report.EventId);
        Assert.Single(report.Selector.Options);
        Assert.Null(report.Selector.SelectedEventId);
        Assert.True(events.EventLookupUsed);
        Assert.Equal(0, summary.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(4)]
    public async Task SummaryValuesDoNotDependOnTheRequestedPage(int? page)
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Paged" });
        var service = new ParticipationReportService(
            events,
            new FakeReportFacts(new ParticipationReportSummaryFacts(1, 6, 2, 3, 5, 5m / 6m)));

        var report = await service.GetReportAsync(1, page, TestContext.Current.CancellationToken);

        Assert.Equal(1, report.PendingCount);
        Assert.Equal(6, report.ConfirmedCount);
        Assert.Equal(2, report.WaitlistedCount);
        Assert.Equal(3, report.CancelledCount);
        Assert.Equal(5, report.ValidAttendeeCount);
        Assert.Equal(5m / 6m, report.AttendanceRate);
    }

    [Fact]
    public async Task DetailRowsPageInRepositoryOrderWhileTotalsStayFullDataset()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Paged" });
        var facts = new FakeReportFacts(new ParticipationReportSummaryFacts(0, 26, 0, 0, 20, 20m / 26m));
        for (var index = 1; index <= 26; index++)
        {
            facts.DetailRows.Add(DetailRow(index, $"Participant {index:D2}"));
        }

        var service = new ParticipationReportService(events, facts);

        var first = await service.GetReportAsync(1, 1, TestContext.Current.CancellationToken);

        Assert.Equal(1, first.PageNumber);
        Assert.Equal(26, first.TotalCount);
        Assert.Equal(25, first.Rows.Count);
        Assert.Equal(Enumerable.Range(1, 25), first.Rows.Select(row => row.RegistrationId));
        Assert.Equal(0, facts.LastSkip);
        Assert.Equal(PageRequest.FixedPageSize, facts.LastTake);

        var second = await service.GetReportAsync(1, 2, TestContext.Current.CancellationToken);

        Assert.Equal(2, second.PageNumber);
        Assert.Equal(26, second.TotalCount);
        var lastRow = Assert.Single(second.Rows);
        Assert.Equal(26, lastRow.RegistrationId);
        Assert.Equal(PageRequest.FixedPageSize, facts.LastSkip);

        // No row is repeated or skipped across the boundary.
        Assert.Empty(first.Rows.Select(row => row.RegistrationId)
            .Intersect(second.Rows.Select(row => row.RegistrationId)));

        // The summary still covers the full dataset on both pages.
        Assert.Equal(26, second.ConfirmedCount);
        Assert.Equal(20, second.ValidAttendeeCount);
        Assert.Equal(20m / 26m, second.AttendanceRate);
    }

    [Theory]
    [InlineData(null, 1, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(-4, 1, 0)]
    [InlineData(9, 2, 25)]
    public async Task InvalidPageRequestsNormalizeToTheFirstOrLastPage(int? page, int expectedPage, int expectedSkip)
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Paged" });
        var facts = new FakeReportFacts(new ParticipationReportSummaryFacts(0, 26, 0, 0, 20, 20m / 26m));
        for (var index = 1; index <= 26; index++)
        {
            facts.DetailRows.Add(DetailRow(index, $"Participant {index:D2}"));
        }

        var service = new ParticipationReportService(events, facts);

        var report = await service.GetReportAsync(1, page, TestContext.Current.CancellationToken);

        Assert.Equal(expectedPage, report.PageNumber);
        Assert.Equal(expectedSkip, facts.LastSkip);
        Assert.Equal(26, report.TotalCount);
        Assert.Equal(expectedPage == 2 ? 1 : 25, report.Rows.Count);
    }

    [Fact]
    public async Task DetailRowsMapStatusAndAttendanceLabelsForACompletedEvent()
    {
        var now = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Completed" });
        var facts = new FakeReportFacts(new ParticipationReportSummaryFacts(0, 3, 1, 1, 1, 1m / 3m));
        facts.DetailRows.Add(DetailRow(1, "Checked", checkedInAt: now));
        facts.DetailRows.Add(DetailRow(2, "Invalidated", checkedInAt: now, invalidated: true));
        facts.DetailRows.Add(DetailRow(3, "Absent"));
        facts.DetailRows.Add(DetailRow(4, "Pending", status: RegistrationStatusCode.Pending));
        facts.DetailRows.Add(DetailRow(5, "Cancelled", status: RegistrationStatusCode.Cancelled, checkedInAt: now));

        var service = new ParticipationReportService(events, facts);

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.Equal(5, report.Rows.Count);

        var checkedIn = report.Rows[0];
        Assert.True(checkedIn.Attended);
        Assert.False(checkedIn.IsNoShow);
        Assert.Equal("Checked in", checkedIn.AttendanceLabel);
        Assert.Equal("badge-open", checkedIn.AttendanceBadgeClass);
        Assert.Equal(now, checkedIn.CheckedInAt);
        Assert.Equal("Confirmed", checkedIn.StatusLabel);
        Assert.Equal("badge-open", checkedIn.StatusBadgeClass);

        var invalidated = report.Rows[1];
        Assert.False(invalidated.Attended);
        Assert.True(invalidated.IsNoShow);
        Assert.Equal("Invalidated", invalidated.AttendanceLabel);
        Assert.Equal("badge-inactive", invalidated.AttendanceBadgeClass);
        Assert.Equal(now, invalidated.CheckedInAt);

        var absent = report.Rows[2];
        Assert.False(absent.Attended);
        Assert.True(absent.IsNoShow);
        Assert.Equal("No-show", absent.AttendanceLabel);
        Assert.Equal("badge-inactive", absent.AttendanceBadgeClass);
        Assert.Null(absent.CheckedInAt);

        var pending = report.Rows[3];
        Assert.False(pending.Attended);
        Assert.False(pending.IsNoShow);
        Assert.Equal("—", pending.AttendanceLabel);
        Assert.Null(pending.AttendanceBadgeClass);
        Assert.Null(pending.CheckedInAt);
        Assert.Equal("Pending review", pending.StatusLabel);

        // A non-confirmed row that was checked in still shows its real attendance result.
        var cancelled = report.Rows[4];
        Assert.True(cancelled.Attended);
        Assert.False(cancelled.IsNoShow);
        Assert.Equal("Checked in", cancelled.AttendanceLabel);
    }

    [Fact]
    public async Task NonFinalEventShowsNotCheckedInWithoutANoShow()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Published) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Published" });
        var facts = new FakeReportFacts(new ParticipationReportSummaryFacts(0, 1, 0, 0, 0, 0m));
        facts.DetailRows.Add(DetailRow(1, "Waiting"));

        var service = new ParticipationReportService(events, facts);

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        var row = Assert.Single(report.Rows);
        Assert.False(row.Attended);
        Assert.False(row.IsNoShow);
        Assert.Equal("Not checked in", row.AttendanceLabel);
        Assert.Equal("badge-closed", row.AttendanceBadgeClass);
        Assert.Null(row.CheckedInAt);
    }

    [Fact]
    public async Task EmptyEventReturnsNoRowsAndSkipsTheDetailRead()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Empty" });
        var facts = new FakeReportFacts(new ParticipationReportSummaryFacts(0, 0, 0, 0, 0, null));

        var service = new ParticipationReportService(events, facts);

        var report = await service.GetReportAsync(1, 3, TestContext.Current.CancellationToken);

        Assert.Empty(report.Rows);
        Assert.Equal(0, report.TotalCount);
        Assert.Equal(1, report.PageNumber);
        Assert.Equal(1, facts.CountDetailCalls);
        Assert.Equal(0, facts.DetailPageCalls);
    }

    private static ParticipationReportDetailRow DetailRow(
        int registrationId,
        string memberName,
        RegistrationStatusCode status = RegistrationStatusCode.Confirmed,
        DateTime? checkedInAt = null,
        bool invalidated = false,
        string course = "BSIT",
        int yearLevel = 1,
        string studentNumber = "S-0001") =>
        new(registrationId, memberName, studentNumber, course, yearLevel, status, checkedInAt, invalidated);

    private static Event Event(int eventId, EventStatusCode status) => new()
    {
        EventId = eventId,
        Title = "Event",
        StartsAt = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc),
        EventStatusId = status
    };

    private sealed class FakeReportFacts(
        ParticipationReportSummaryFacts facts,
        int noShowCount = 0,
        IReadOnlyList<CourseDistributionRow>? courses = null,
        IReadOnlyList<YearDistributionRow>? years = null) : IParticipationReportRepository
    {
        public List<ParticipationReportDetailRow> DetailRows { get; } = [];
        public int Calls { get; private set; }
        public int NoShowCalls { get; private set; }
        public int DistributionCalls { get; private set; }
        public int CountDetailCalls { get; private set; }
        public int DetailPageCalls { get; private set; }
        public int? LastEventId { get; private set; }
        public int? LastSkip { get; private set; }
        public int? LastTake { get; private set; }

        public Task<ParticipationReportSummaryFacts> GetSummaryFactsAsync(int eventId, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastEventId = eventId;
            return Task.FromResult(facts);
        }

        public Task<int> GetNoShowCountAsync(int eventId, CancellationToken cancellationToken = default)
        {
            NoShowCalls++;
            LastEventId = eventId;
            return Task.FromResult(noShowCount);
        }

        public Task<ParticipationReportDistributionFacts> GetAttendeeDistributionAsync(int eventId, CancellationToken cancellationToken = default)
        {
            DistributionCalls++;
            LastEventId = eventId;
            return Task.FromResult(new ParticipationReportDistributionFacts(courses ?? [], years ?? []));
        }

        public Task<int> CountDetailRowsAsync(int eventId, CancellationToken cancellationToken = default)
        {
            CountDetailCalls++;
            LastEventId = eventId;
            return Task.FromResult(DetailRows.Count);
        }

        public Task<IReadOnlyList<ParticipationReportDetailRow>> GetDetailPageAsync(
            int eventId,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            DetailPageCalls++;
            LastEventId = eventId;
            LastSkip = skip;
            LastTake = take;
            return Task.FromResult<IReadOnlyList<ParticipationReportDetailRow>>(
                DetailRows.Skip(skip).Take(take).ToList());
        }
    }

    private sealed class FakeEvents : IEventRepository
    {
        public List<EventListItem> Items { get; } = [];
        public Event EventItem { get; set; } = null!;
        public bool EventLookupUsed { get; private set; }

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Items.Count);

        public Task<IReadOnlyList<EventListItem>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EventListItem>>(Items.Skip(skip).Take(take).ToList());

        public Task<Event> GetEventAsync(int eventId, CancellationToken cancellationToken = default)
        {
            EventLookupUsed = true;
            return Task.FromResult(EventItem?.EventId == eventId ? EventItem : null!);
        }

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
}
