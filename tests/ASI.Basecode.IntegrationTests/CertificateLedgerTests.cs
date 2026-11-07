using System.Reflection;
using System.Text;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.DemoData;
using Microsoft.Extensions.Options;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class CertificateLedgerTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LedgerPagesSeededReviewsForEveryValidAttendeeOnBothTabs()
    {
        var setup = new Setup(28, invalidateBeforeCompletion: true);
        var first = await setup.Service.GetPageAsync(null, CertificateLedgerTab.Eligibility, 1, TestContext.Current.CancellationToken);
        Assert.Equal(27, first.EligibilityCount);
        Assert.Equal(25, first.Rows.Count);
        Assert.Equal(7, first.Selector.SelectedEventId);
        Assert.True(first.Page.HasNextPage);
        var last = await setup.Service.GetPageAsync(7, CertificateLedgerTab.Eligibility, int.MaxValue, TestContext.Current.CancellationToken);
        Assert.Equal(2, last.Rows.Count);
        Assert.Equal(2, last.Page.PageNumber);
        await setup.Service.DecideAsync(1, true, null, TestContext.Current.CancellationToken);
        await setup.Service.IssueAsync(1, TestContext.Current.CancellationToken);
        var issued = await setup.Service.GetPageAsync(7, CertificateLedgerTab.Issued, 1, TestContext.Current.CancellationToken);
        Assert.Single(issued.Rows);
        Assert.Equal(1, issued.IssuedCount);
        Assert.Equal(26, issued.EligibilityCount);
    }

    [Fact]
    public async Task DecisionsRequirePendingValidAttendanceAndDeclineReason()
    {
        var setup = new Setup(3);
        await setup.Service.GetPageAsync(7, CertificateLedgerTab.Eligibility, 1, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.DecideAsync(1, false, "   ", TestContext.Current.CancellationToken));
        Assert.True(setup.Store.TryGetReview(1, out var pending));
        Assert.Equal(CertificateStatusCode.PendingApproval, pending.Status);
        await setup.Service.DecideAsync(1, false, " Participation requirements not met. ", TestContext.Current.CancellationToken);
        Assert.True(setup.Store.TryGetReview(1, out var declined));
        Assert.Equal("Participation requirements not met.", declined.Reason);
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.DecideAsync(1, true, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.IssueAsync(1, TestContext.Current.CancellationToken));
        await setup.Service.DecideAsync(2, true, null, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.DecideAsync(2, true, null, TestContext.Current.CancellationToken));
        setup.Store.InvalidateAttendance(3, "Correction.", Now);
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.DecideAsync(3, true, null, TestContext.Current.CancellationToken));
        setup.Confirmed = false;
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.IssueAsync(2, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IssuePublishesOnlyAfterRenderingAndKeepsSnapshotAndBytes()
    {
        var setup = new Setup(1);
        await setup.Service.GetPageAsync(7, CertificateLedgerTab.Eligibility, 1, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.IssueAsync(1, TestContext.Current.CancellationToken));
        await setup.Service.DecideAsync(1, true, null, TestContext.Current.CancellationToken);
        setup.Renderer.BeforeRender = () => Assert.False(setup.Store.IsCertificateIssued(1));
        await setup.Service.IssueAsync(1, TestContext.Current.CancellationToken);
        var pdf = await setup.Service.GetPdfAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal("AT-2026-000001", pdf.CertificateNumber);
        Assert.Equal("Member 1", setup.Renderer.Snapshot!.RecipientName);
        Assert.Equal("Leadership Summit", setup.Renderer.Snapshot.EventTitle);
        Assert.Equal(0, setup.Renderer.SampleCalls);
        Assert.Equal(1, setup.Renderer.Calls);
        setup.Event.Title = "Changed event";
        var again = await setup.Service.GetPdfAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(pdf.Bytes, again.Bytes);
        Assert.Equal("Leadership Summit", (await setup.Service.GetViewerAsync(1, TestContext.Current.CancellationToken)).EventTitle);
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.IssueAsync(1, TestContext.Current.CancellationToken));
        Assert.Throws<DomainException>(() => setup.Store.InvalidateAttendance(1, "Correction", Now));
    }

    [Fact]
    public async Task FailedRenderOrInvalidationDuringRenderNeverPublishesIssue()
    {
        var setup = new Setup(1);
        await setup.Service.GetPageAsync(7, CertificateLedgerTab.Eligibility, 1, TestContext.Current.CancellationToken);
        await setup.Service.DecideAsync(1, true, null, TestContext.Current.CancellationToken);
        setup.Renderer.Fail = true;
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.IssueAsync(1, TestContext.Current.CancellationToken));
        Assert.False(setup.Store.IsCertificateIssued(1));
        setup.Renderer.Fail = false;
        setup.Renderer.BeforeRender = () => setup.Store.InvalidateAttendance(1, "Corrected while rendering.", Now);
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.IssueAsync(1, TestContext.Current.CancellationToken));
        Assert.False(setup.Store.IsCertificateIssued(1));
    }

    [Fact]
    public async Task CurrentEventStateIsRecheckedAndUnknownDocumentsAreRefused()
    {
        var setup = new Setup(1);
        await setup.Service.GetPageAsync(7, CertificateLedgerTab.Eligibility, 1, TestContext.Current.CancellationToken);
        await setup.Service.DecideAsync(1, true, null, TestContext.Current.CancellationToken);
        setup.Event.EventStatusId = EventStatusCode.Cancelled;
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.IssueAsync(1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.GetPdfAsync(1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.GetViewerAsync(999, TestContext.Current.CancellationToken));
        setup.Actor.Role = RoleCodes.Member;
        await Assert.ThrowsAsync<DomainException>(() => setup.Service.GetPageAsync(7, CertificateLedgerTab.Eligibility, 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisabledProviderIsEmptyAndRefusesCommandsAndDocuments()
    {
        var service = new UnavailableCertificateLedgerService();
        Assert.Empty((await service.GetPageAsync(null, CertificateLedgerTab.Eligibility, 1, TestContext.Current.CancellationToken)).Rows);
        await Assert.ThrowsAsync<DomainException>(() => service.DecideAsync(1, true, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<DomainException>(() => service.IssueAsync(1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<DomainException>(() => service.GetViewerAsync(1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<DomainException>(() => service.GetPdfAsync(1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentIssuesPublishExactlyOneDocument()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = new Setup(1);
        await setup.Service.DecideAsync(1, true, null, ct);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothRendering = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Renderer.Gate = gate.Task;
        setup.Renderer.BeforeRender = () => { if (setup.Renderer.Calls == 2) bothRendering.SetResult(); };
        var first = CaptureAsync();
        var second = CaptureAsync();
        await bothRendering.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        gate.SetResult();
        var outcomes = await Task.WhenAll(first, second);
        Assert.Single(outcomes, outcome => outcome == null);
        Assert.Single(outcomes, outcome => outcome?.Outcome == OutcomeCode.Duplicate);
        Assert.True(setup.Store.IsCertificateIssued(1));

        async Task<DomainException?> CaptureAsync()
        {
            try { await setup.Service.IssueAsync(1, ct); return null; }
            catch (DomainException exception) { return exception; }
        }
    }

    [Fact]
    public async Task SeededIssueRendersCapturedSnapshotOnlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = new Setup(1);
        var snapshot = new CertificatePdfSnapshot("Original recipient", "Original event", Now.AddDays(-1), Now, "AT-2026-000001");
        setup.Store.SeedIssuedCertificate(1, 7, snapshot.CertificateNumber, Now, snapshot);
        setup.Event.Title = "Later change";
        await setup.Service.GetPdfAsync(1, ct);
        await setup.Service.GetPdfAsync(1, ct);
        Assert.Equal(snapshot, setup.Renderer.Snapshot);
        Assert.Equal(1, setup.Renderer.Calls);
        Assert.Equal(0, setup.Renderer.SampleCalls);
    }

    private sealed class Setup
    {
        public readonly DemoStore Store = new(Options.Create(new DemoDataOptions { TokenKey = "ledger-unit-key" }));
        public readonly FakeRenderer Renderer = new();
        public readonly Actor Actor = new();
        public readonly Event Event = new() { EventId = 7, Title = "Leadership Summit", StartsAt = Now.AddDays(-2), EventStatusId = EventStatusCode.Published };
        public readonly DemoCertificateLedgerService Service;
        public bool Confirmed = true;

        public Setup(int count, bool invalidateBeforeCompletion = false)
        {
            var rows = Enumerable.Range(1, count).Select(id => new RegistrationReviewRow(id, 7, Event.Title,
                $"Member {id}", $"2026-{id:D4}", Now.AddDays(-3), RegistrationStatusCode.Confirmed)).ToList();
            var events = Proxy<IEventRepository>((method, args) => method.Name switch
            {
                nameof(IEventRepository.CountAsync) => Task.FromResult(1),
                nameof(IEventRepository.GetPageAsync) => Task.FromResult<IReadOnlyList<EventListItem>>([new() { EventId = 7, Title = Event.Title, StartsAt = Event.StartsAt, EventStatusId = Event.EventStatusId }]),
                nameof(IEventRepository.GetEventAsync) => Task.FromResult(Event),
                _ => throw new NotSupportedException(method.Name)
            });
            var registrations = Proxy<IRegistrationRepository>((method, args) => method.Name switch
            {
                nameof(IRegistrationRepository.CountReviewQueueAsync) => Task.FromResult(rows.Count),
                nameof(IRegistrationRepository.GetReviewQueueAsync) => Task.FromResult<IReadOnlyList<RegistrationReviewRow>>(rows.Skip((int)args![2]!).Take((int)args[3]!).ToList()),
                nameof(IRegistrationRepository.IsConfirmedAsync) => Task.FromResult(Confirmed),
                nameof(IRegistrationRepository.DiscoverReviewAsync) => Task.FromResult(new RegistrationReviewIdentity(10, (int)args![0]!, 7)),
                _ => throw new NotSupportedException(method.Name)
            });
            foreach (var row in rows) Store.RecordAttendance(row.RegistrationId, 7, row.MemberName, row.StudentNumber, Now.AddDays(-2), CheckInMethodCode.QrCode);
            if (invalidateBeforeCompletion) Store.InvalidateAttendance(count, "Attendance corrected.", Now);
            // The demo ledger no longer creates reviews at query time; seed them after invalidation
            // because the helper filters to valid attendance.
            Store.SeedPendingReviewsForEvent(7, Now);
            Store.MarkCompleted(7);
            Service = new DemoCertificateLedgerService(events, registrations, Store, new DemoCertificateDocuments(Store, Renderer), Actor, new Clock());
        }
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Actor : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => 10;
        public string Role { get; set; } = RoleCodes.Officer;
    }

    private sealed class FakeRenderer : ICertificatePdfRenderer
    {
        public bool Fail;
        public int Calls;
        public int SampleCalls;
        public Action? BeforeRender;
        public Task Gate = Task.CompletedTask;
        public CertificatePdfSnapshot? Snapshot;
        public Task<byte[]> RenderSampleAsync(CancellationToken cancellationToken = default)
        {
            SampleCalls++;
            throw new InvalidOperationException("Sample rendering must not be used.");
        }
        public async Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Calls++;
            Snapshot = snapshot;
            BeforeRender?.Invoke();
            if (Fail) throw new InvalidOperationException("Renderer unavailable.");
            await Gate.WaitAsync(cancellationToken);
            return Encoding.ASCII.GetBytes("%PDF-1.7\ncertificate");
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, RepositoryProxy>();
        ((RepositoryProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class RepositoryProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}
