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
    public async Task FilteredParticipantPagingKeepsSummaryAndBreakdownsEventWide()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Filtered" });
        var snapshot = new FakeSnapshot();
        for (var index = 1; index <= 12; index++) snapshot.Rows.Add(DetailRow(index, index <= 7 ? $"Match {index}" : $"Other {index}", checkedInAt: CheckedInAt));
        var options = new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 3, Search = "match", Filter = "Confirmed", Type = "attended" };
        var service = new ParticipationReportService(events, snapshot, options);
        var result = await service.GetReportAsync(1, 2, TestContext.Current.CancellationToken);
        Assert.Equal(7, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(new[] { 4, 5, 6 }, result.Rows.Select(row => row.RegistrationId));
        Assert.Equal(12, result.ConfirmedCount);
        Assert.Equal(12, result.ValidAttendeeCount);
        Assert.Equal(12, result.CourseBreakdown.Sum(item => item.Count));
    }

    private static readonly DateTime CheckedInAt = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CompletedEventDerivesEveryPanelFromOneSnapshot()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Completed" });
        events.Items.Add(new EventListItem { EventId = 2, Title = "Other" });
        var snapshot = new FakeSnapshot();
        snapshot.Rows.Add(DetailRow(1, "Attended A", checkedInAt: CheckedInAt, course: "BSIT", yearLevel: 1));
        snapshot.Rows.Add(DetailRow(2, "Attended B", checkedInAt: CheckedInAt, course: "bsit", yearLevel: 1));
        snapshot.Rows.Add(DetailRow(3, "Attended C", checkedInAt: CheckedInAt, course: "BSED", yearLevel: 3));
        snapshot.Rows.Add(DetailRow(4, "Attended D", checkedInAt: CheckedInAt, course: "   ", yearLevel: 2));
        snapshot.Rows.Add(DetailRow(5, "Invalidated", checkedInAt: CheckedInAt, invalidated: true));
        snapshot.Rows.Add(DetailRow(6, "Absent"));
        snapshot.Rows.Add(DetailRow(7, "Pending", status: RegistrationStatusCode.Pending));
        snapshot.Rows.Add(DetailRow(8, "Waitlisted", status: RegistrationStatusCode.Waitlisted));
        snapshot.Rows.Add(DetailRow(9, "Cancelled", status: RegistrationStatusCode.Cancelled));
        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.Equal(1, report.EventId);
        Assert.Equal("Event", report.EventTitle);
        Assert.Equal(EventStatusCode.Completed, report.EventStatusId);
        Assert.Equal("Completed", report.EventStatusLabel);
        Assert.Equal("badge-completed", report.EventStatusBadgeClass);
        Assert.True(report.IsFinal);
        Assert.Equal(1, report.PendingCount);
        Assert.Equal(6, report.ConfirmedCount);
        Assert.Equal(1, report.WaitlistedCount);
        Assert.Equal(1, report.CancelledCount);
        Assert.Equal(4, report.ValidAttendeeCount);
        Assert.Equal(4m / 6m, report.AttendanceRate);
        Assert.Equal(2, report.NoShowCount);
        Assert.Equal(9, report.TotalCount);
        Assert.Equal(2, report.Selector.Options.Count);
        Assert.Equal(1, report.Selector.SelectedEventId);

        // Course labels are trimmed and grouped case-insensitively, largest share first then label.
        Assert.Equal(new[] { "BSIT", "BSED", "Unspecified" }, report.CourseBreakdown.Select(row => row.Label));
        Assert.Equal(new[] { 2, 1, 1 }, report.CourseBreakdown.Select(row => row.Count));
        Assert.Equal(new[] { 50, 25, 25 }, report.CourseBreakdown.Select(row => row.Percent));

        // Year labels order by the snapshot year; the schema forbids a zero year.
        Assert.Equal(new[] { "Year 1", "Year 2", "Year 3" }, report.YearBreakdown.Select(row => row.Label));
        Assert.Equal(new[] { 2, 1, 1 }, report.YearBreakdown.Select(row => row.Count));
        Assert.Equal(new[] { 50, 25, 25 }, report.YearBreakdown.Select(row => row.Percent));

        // The panels agree by construction: one snapshot read feeds all of them.
        Assert.Equal(1, snapshot.Calls);
        Assert.Equal(1, snapshot.LastEventId);
    }

    [Fact]
    public async Task ZeroAttendeesProducesEmptyBreakdownsWithoutDividingByZero()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Empty" });
        var snapshot = new FakeSnapshot();
        snapshot.Rows.Add(DetailRow(1, "Absent A"));
        snapshot.Rows.Add(DetailRow(2, "Absent B"));
        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, report.NoShowCount);
        Assert.Equal(0m, report.AttendanceRate);
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
        var snapshot = new FakeSnapshot();
        snapshot.Rows.Add(DetailRow(1, "Attended", checkedInAt: CheckedInAt));
        snapshot.Rows.Add(DetailRow(2, "Absent"));
        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.False(report.IsFinal);
        Assert.Equal(status, report.EventStatusId);
        Assert.Equal(0.5m, report.AttendanceRate);
        Assert.Equal(0, report.NoShowCount);
    }

    [Fact]
    public async Task ZeroConfirmedKeepsAttendanceRateNull()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Empty" });
        var service = new ParticipationReportService(events, new FakeSnapshot());

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.True(report.IsFinal);
        Assert.Equal(0, report.ConfirmedCount);
        Assert.Equal(0, report.ValidAttendeeCount);
        Assert.Null(report.AttendanceRate);
    }

    [Fact]
    public async Task NoSelectionReturnsSelectorAndEmptyReportWithoutReadingTheSnapshot()
    {
        var events = new FakeEvents();
        events.Items.Add(new EventListItem { EventId = 1, Title = "First" });
        var snapshot = new FakeSnapshot();
        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(null, 2, TestContext.Current.CancellationToken);

        Assert.Null(report.EventId);
        Assert.Null(report.EventStatusId);
        Assert.False(report.IsFinal);
        Assert.Single(report.Selector.Options);
        Assert.Null(report.Selector.SelectedEventId);
        Assert.False(events.EventLookupUsed);
        Assert.Equal(0, snapshot.Calls);
        Assert.Empty(report.Rows);
    }

    [Fact]
    public async Task UnknownEventReturnsSelectorAndEmptyReportWithoutReadingTheSnapshot()
    {
        var events = new FakeEvents();
        events.Items.Add(new EventListItem { EventId = 1, Title = "First" });
        var snapshot = new FakeSnapshot();
        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(99, null, TestContext.Current.CancellationToken);

        Assert.Null(report.EventId);
        Assert.Single(report.Selector.Options);
        Assert.Null(report.Selector.SelectedEventId);
        Assert.True(events.EventLookupUsed);
        Assert.Equal(0, snapshot.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(4)]
    public async Task SummaryValuesDoNotDependOnTheRequestedPage(int? page)
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Paged" });
        var snapshot = new FakeSnapshot();
        for (var index = 1; index <= 26; index++)
        {
            snapshot.Rows.Add(index switch
            {
                <= 5 => DetailRow(index, $"Attended {index:D2}", checkedInAt: CheckedInAt),
                6 => DetailRow(index, "Absent"),
                7 => DetailRow(index, "Pending", status: RegistrationStatusCode.Pending),
                <= 9 => DetailRow(index, $"Waitlisted {index:D2}", status: RegistrationStatusCode.Waitlisted),
                _ => DetailRow(index, $"Cancelled {index:D2}", status: RegistrationStatusCode.Cancelled)
            });
        }

        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(1, page, TestContext.Current.CancellationToken);

        Assert.Equal(1, report.PendingCount);
        Assert.Equal(6, report.ConfirmedCount);
        Assert.Equal(2, report.WaitlistedCount);
        Assert.Equal(17, report.CancelledCount);
        Assert.Equal(5, report.ValidAttendeeCount);
        Assert.Equal(5m / 6m, report.AttendanceRate);
        Assert.Equal(1, report.NoShowCount);
        Assert.Equal(26, report.TotalCount);
    }

    [Fact]
    public async Task DetailRowsPageInSnapshotOrderWhileTotalsStayFullDataset()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Paged" });
        var snapshot = new FakeSnapshot();
        for (var index = 1; index <= 26; index++)
        {
            snapshot.Rows.Add(DetailRow(index, $"Participant {index:D2}", checkedInAt: CheckedInAt));
        }

        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var first = await service.GetReportAsync(1, 1, TestContext.Current.CancellationToken);

        Assert.Equal(1, first.PageNumber);
        Assert.Equal(26, first.TotalCount);
        Assert.Equal(25, first.Rows.Count);
        Assert.Equal(Enumerable.Range(1, 25), first.Rows.Select(row => row.RegistrationId));

        var second = await service.GetReportAsync(1, 2, TestContext.Current.CancellationToken);

        Assert.Equal(2, second.PageNumber);
        Assert.Equal(26, second.TotalCount);
        var lastRow = Assert.Single(second.Rows);
        Assert.Equal(26, lastRow.RegistrationId);

        // No row is repeated or skipped across the boundary.
        Assert.Empty(first.Rows.Select(row => row.RegistrationId)
            .Intersect(second.Rows.Select(row => row.RegistrationId)));

        // The summary still covers the full dataset on both pages.
        Assert.Equal(26, second.ConfirmedCount);
        Assert.Equal(26, second.ValidAttendeeCount);
        Assert.Equal(1m, second.AttendanceRate);
        Assert.Equal(0, second.NoShowCount);
    }

    [Theory]
    [InlineData(null, 1, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(-4, 1, 1)]
    [InlineData(9, 2, 26)]
    public async Task InvalidPageRequestsNormalizeToTheFirstOrLastPage(int? page, int expectedPage, int expectedFirstRegistrationId)
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Paged" });
        var snapshot = new FakeSnapshot();
        for (var index = 1; index <= 26; index++)
        {
            snapshot.Rows.Add(DetailRow(index, $"Participant {index:D2}", checkedInAt: CheckedInAt));
        }

        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(1, page, TestContext.Current.CancellationToken);

        Assert.Equal(expectedPage, report.PageNumber);
        Assert.Equal(expectedFirstRegistrationId, report.Rows[0].RegistrationId);
        Assert.Equal(26, report.TotalCount);
        Assert.Equal(expectedPage == 2 ? 1 : 25, report.Rows.Count);
    }

    [Fact]
    public async Task DetailRowsMapStatusAndAttendanceLabelsForACompletedEvent()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Completed" });
        var snapshot = new FakeSnapshot();
        snapshot.Rows.Add(DetailRow(1, "Checked", checkedInAt: CheckedInAt));
        snapshot.Rows.Add(DetailRow(2, "Invalidated", checkedInAt: CheckedInAt, invalidated: true));
        snapshot.Rows.Add(DetailRow(3, "Absent"));
        snapshot.Rows.Add(DetailRow(4, "Pending", status: RegistrationStatusCode.Pending));
        snapshot.Rows.Add(DetailRow(5, "Cancelled", status: RegistrationStatusCode.Cancelled, checkedInAt: CheckedInAt));

        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        Assert.Equal(5, report.Rows.Count);

        var checkedIn = report.Rows[0];
        Assert.True(checkedIn.Attended);
        Assert.False(checkedIn.IsNoShow);
        Assert.Equal("Checked in", checkedIn.AttendanceLabel);
        Assert.Equal("badge-open", checkedIn.AttendanceBadgeClass);
        Assert.Equal(CheckedInAt, checkedIn.CheckedInAt);
        Assert.Equal("Confirmed", checkedIn.StatusLabel);
        Assert.Equal("badge-open", checkedIn.StatusBadgeClass);

        var invalidated = report.Rows[1];
        Assert.False(invalidated.Attended);
        Assert.True(invalidated.IsNoShow);
        Assert.Equal("Invalidated", invalidated.AttendanceLabel);
        Assert.Equal("badge-inactive", invalidated.AttendanceBadgeClass);
        Assert.Equal(CheckedInAt, invalidated.CheckedInAt);

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
        var snapshot = new FakeSnapshot();
        snapshot.Rows.Add(DetailRow(1, "Waiting"));
        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(1, null, TestContext.Current.CancellationToken);

        var row = Assert.Single(report.Rows);
        Assert.False(row.Attended);
        Assert.False(row.IsNoShow);
        Assert.Equal("Not checked in", row.AttendanceLabel);
        Assert.Equal("badge-closed", row.AttendanceBadgeClass);
        Assert.Null(row.CheckedInAt);
    }

    [Fact]
    public async Task EmptyEventReturnsNoRowsFromTheSameSnapshotRead()
    {
        var events = new FakeEvents { EventItem = Event(1, EventStatusCode.Completed) };
        events.Items.Add(new EventListItem { EventId = 1, Title = "Empty" });
        var snapshot = new FakeSnapshot();
        var service = new ParticipationReportService(events, snapshot, new ASI.Basecode.Data.Queries.ListReadOptions { PageSize = 25 });

        var report = await service.GetReportAsync(1, 3, TestContext.Current.CancellationToken);

        Assert.Empty(report.Rows);
        Assert.Equal(0, report.TotalCount);
        Assert.Equal(1, report.PageNumber);
        Assert.Equal(1, snapshot.Calls);
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
        new(
            registrationId,
            memberName,
            studentNumber,
            course,
            yearLevel,
            status,
            checkedInAt,
            invalidated ? checkedInAt?.AddMinutes(1) : null);

    private static Event Event(int eventId, EventStatusCode status) => new()
    {
        EventId = eventId,
        Title = "Event",
        StartsAt = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc),
        EventStatusId = status
    };

    private sealed class FakeSnapshot : IParticipationReportRepository
    {
        public List<ParticipationReportDetailRow> Rows { get; } = [];
        public int Calls { get; private set; }
        public int? LastEventId { get; private set; }

        public Task<IReadOnlyList<ParticipationReportDetailRow>> GetSnapshotAsync(
            int eventId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastEventId = eventId;
            return Task.FromResult<IReadOnlyList<ParticipationReportDetailRow>>(Rows.ToList());
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
