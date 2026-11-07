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
    public async Task CompletedEventMapsSummaryFactsAndLeavesDetailFieldsEmpty()
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
        Assert.Equal(1, summary.LastEventId);
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

    private static Event Event(int eventId, EventStatusCode status) => new()
    {
        EventId = eventId,
        Title = "Event",
        StartsAt = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc),
        EventStatusId = status
    };

    private sealed class FakeReportFacts(ParticipationReportSummaryFacts facts) : IParticipationReportRepository
    {
        public int Calls { get; private set; }
        public int? LastEventId { get; private set; }

        public Task<ParticipationReportSummaryFacts> GetSummaryFactsAsync(int eventId, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastEventId = eventId;
            return Task.FromResult(facts);
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
    }
}
