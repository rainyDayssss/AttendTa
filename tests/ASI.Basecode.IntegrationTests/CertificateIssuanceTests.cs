using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using ASI.Basecode.WebApp.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Focused SQL coverage for 6.7: the transactional issuance pipeline, the retained PDF, the single
/// <c>certificate.issued</c> audit entry and every refusal in the ticket matrix.
/// </summary>
public sealed class CertificateIssuanceTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task IssueWritesNumberSnapshotsIssuerPathAndOneAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, new FakePdfRenderer(), store);

            await service.IssueAsync(setup.RegistrationId, ct);

            await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
            var certificate = await observer.Certificates.AsNoTracking()
                .SingleAsync(row => row.CertificateId == setup.CertificateId, ct);
            Assert.Equal(CertificateStatusCode.Issued, certificate.CertificateStatusId);
            Assert.Equal($"AT-2026-{setup.RegistrationId:D6}", certificate.CertificateNumber);
            Assert.Equal(setup.RecipientName, certificate.RecipientNameSnapshot);
            Assert.Equal(setup.EventTitle, certificate.EventTitleSnapshot);
            Assert.Equal(setup.EventStartsAt, certificate.EventStartsAtSnapshot!.Value);
            Assert.Equal(setup.OfficerName, certificate.IssuerNameSnapshot);
            Assert.Equal(setup.OfficerId, certificate.IssuedBy);
            Assert.Equal(Now, certificate.IssuedAt!.Value);
            Assert.StartsWith("certificates/", certificate.FilePath, StringComparison.Ordinal);
            Assert.EndsWith(".pdf", certificate.FilePath, StringComparison.Ordinal);

            await using var file = await store.OpenReadAsync(certificate.FilePath, ct);
            using var copy = new MemoryStream();
            await file.CopyToAsync(copy, ct);
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(copy.ToArray()), StringComparison.Ordinal);
            Assert.Empty(TemporaryFiles(setupRoot));

            var log = await observer.ActivityLogs.AsNoTracking()
                .SingleAsync(row => row.EntityType == "certificates" && row.EntityId == setup.CertificateId, ct);
            Assert.Equal(AuditActions.CertificateIssued, log.Action);
            Assert.Equal(setup.OfficerId, log.ActorUserId);
            Assert.Equal(Now, log.OccurredAt);
            Assert.DoesNotContain("path", log.NewValues!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, await IssuedAuditCountAsync(setup.CertificateId, ct));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateIssueIsRefusedAndLeavesTheStoredFileUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, new FakePdfRenderer(), store);

            await service.IssueAsync(setup.RegistrationId, ct);
            string path;
            byte[] bytes;
            await using (var first = EventDraftTestSupport.Context(fixture.ConnectionString))
            {
                var issued = await first.Certificates.AsNoTracking()
                    .SingleAsync(row => row.CertificateId == setup.CertificateId, ct);
                path = issued.FilePath;
                bytes = await ReadStoredAsync(store, path, ct);
            }

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.Duplicate, exception.Outcome);
            Assert.Single(CertificateFiles(setupRoot));
            Assert.Equal(bytes, await ReadStoredAsync(store, path, ct));
            Assert.Equal(1, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
            Assert.Equal(path, (await observer.Certificates.AsNoTracking()
                .SingleAsync(row => row.CertificateId == setup.CertificateId, ct)).FilePath);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentIssuesWriteOneCertificateOneFileAndOneAuditRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();

            // Both issuances pass their pre-checks and stop inside the renderer, so they reach the
            // write together. SM-4: the unique registration index and the locked re-read must leave
            // exactly one issued certificate, one retained file and one audit row.
            var gate = new GatedPdfRenderer(2);
            await using var firstContext = EventDraftTestSupport.Context(fixture.ConnectionString);
            await using var secondContext = EventDraftTestSupport.Context(fixture.ConnectionString);
            var first = Service(firstContext, setup.OfficerId, RoleCodes.Officer, gate, store);
            var second = Service(secondContext, setup.OfficerId, RoleCodes.Officer, gate, store);

            var firstIssue = CaptureAsync(first.IssueAsync(setup.RegistrationId, ct));
            var secondIssue = CaptureAsync(second.IssueAsync(setup.RegistrationId, ct));
            try
            {
                await gate.BothRendering.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            finally
            {
                gate.Release.TrySetResult();
            }

            var results = await Task.WhenAll(firstIssue, secondIssue)
                .WaitAsync(TimeSpan.FromSeconds(60), ct);

            Assert.Single(results, result => result is null);
            var refused = Assert.Single(results, result => result is not null);
            Assert.Equal(OutcomeCode.Duplicate, refused!.Outcome);

            await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
            var certificate = await observer.Certificates.AsNoTracking()
                .SingleAsync(row => row.CertificateId == setup.CertificateId, ct);
            Assert.Equal(CertificateStatusCode.Issued, certificate.CertificateStatusId);
            Assert.NotNull(certificate.FilePath);
            Assert.NotNull(certificate.IssuedAt);
            // The losing attempt cleans up the file it rendered before the refusal.
            Assert.Single(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            Assert.Equal(1, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await using var stream = await store.OpenReadAsync(certificate.FilePath!, ct);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, ct);
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(copy.ToArray()), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Theory]
    [InlineData(CertificateStatusCode.PendingApproval)]
    [InlineData(CertificateStatusCode.NotEligible)]
    public async Task UnapprovedReviewIsInvalidAndWritesNothing(CertificateStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync(reviewStatus: status);
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, new FakePdfRenderer(), store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            Assert.Equal(0, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await AssertUnchangedAsync(setup.CertificateId, status);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidatedAttendanceIsStaleAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync(invalidateAttendance: true);
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, new FakePdfRenderer(), store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.Stale, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            Assert.Equal(0, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await AssertUnchangedAsync(setup.CertificateId, CertificateStatusCode.Eligible);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task RendererFailureLeavesNoFileAndTheRowUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, new FailingPdfRenderer(), store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            Assert.Equal(0, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await AssertUnchangedAsync(setup.CertificateId, CertificateStatusCode.Eligible);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task NonPdfRenderIsInvalidAndLeavesNoFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(
                context,
                setup.OfficerId,
                RoleCodes.Officer,
                new FakePdfRenderer(System.Text.Encoding.ASCII.GetBytes("not a pdf")),
                store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.Invalid, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            Assert.Equal(0, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await AssertUnchangedAsync(setup.CertificateId, CertificateStatusCode.Eligible);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task DecisionThatLandsDuringRenderIsStaleAndDeletesTheNewFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            var renderer = new MutatingPdfRenderer(async () =>
            {
                await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
                var row = await writer.Certificates.SingleAsync(item => item.CertificateId == setup.CertificateId, ct);
                row.CertificateStatusId = CertificateStatusCode.NotEligible;
                row.ReviewNotes = "Changed while the document rendered.";
                await writer.SaveChangesAsync(ct);
            });
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, renderer, store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.Stale, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            Assert.Equal(0, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await AssertUnchangedAsync(setup.CertificateId, CertificateStatusCode.NotEligible);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task MemberCallerIsForbiddenAndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Member, new FakePdfRenderer(), store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.Forbidden, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            Assert.Equal(0, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await AssertUnchangedAsync(setup.CertificateId, CertificateStatusCode.Eligible);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task CommitFailureDeletesTheNewFileAndLeavesTheRowUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(
                context,
                setup.OfficerId,
                RoleCodes.Officer,
                new FakePdfRenderer(),
                store,
                new ThrowingAuditWriter());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            await AssertUnchangedAsync(setup.CertificateId, CertificateStatusCode.Eligible);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task AttendanceInvalidatedDuringRenderIsStaleAndDeletesTheNewFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            var renderer = new MutatingPdfRenderer(async () =>
            {
                await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
                var row = await writer.AttendanceRecords
                    .SingleAsync(item => item.RegistrationId == setup.RegistrationId, ct);
                row.InvalidatedBy = setup.OfficerId;
                row.InvalidatedAt = Now;
                row.InvalidationReason = "Invalidated while the document rendered.";
                await writer.SaveChangesAsync(ct);
            });
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, renderer, store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.Stale, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
            Assert.Equal(0, await IssuedAuditCountAsync(setup.CertificateId, ct));
            await AssertUnchangedAsync(setup.CertificateId, CertificateStatusCode.Eligible);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task UnknownRegistrationIsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync();
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, new FakePdfRenderer(), store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(int.MaxValue, ct));

            Assert.Equal(OutcomeCode.NotFound, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task ConfirmedRegistrationWithoutReviewIsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var setup = await SeedAsync(seedReview: false);
            await using var context = EventDraftTestSupport.Context(fixture.ConnectionString);
            var service = Service(context, setup.OfficerId, RoleCodes.Officer, new FakePdfRenderer(), store);

            var exception = await Assert.ThrowsAsync<DomainException>(
                () => service.IssueAsync(setup.RegistrationId, ct));

            Assert.Equal(OutcomeCode.NotFound, exception.Outcome);
            Assert.Empty(CertificateFiles(setupRoot));
            Assert.Empty(TemporaryFiles(setupRoot));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    private static CertificateLedgerService Service(
        AsiBasecodeDBContext context,
        int actorId,
        string role,
        ICertificatePdfRenderer renderer,
        IPrivateFileStore files,
        IActivityLogWriter? audit = null)
    {
        var unitOfWork = new UnitOfWork(context);
        var actor = new Actor(actorId, role);
        var clock = new FixedClock(Now);
        return new CertificateLedgerService(
            new EventRepository(unitOfWork),
            new CertificateRepository(unitOfWork),
            new RegistrationRepository(unitOfWork),
            new AttendanceRepository(unitOfWork),
            new UserRepository(unitOfWork),
            unitOfWork,
            actor,
            audit ?? new ActivityLogWriter(context, actor, clock),
            clock,
            renderer,
            files);
    }

    private async Task<Setup> SeedAsync(
        CertificateStatusCode reviewStatus = CertificateStatusCode.Eligible,
        bool invalidateAttendance = false,
        bool seedReview = true)
    {
        var ct = TestContext.Current.CancellationToken;
        var password = MemberEventTestSupport.NewPassword();
        var officer = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"issuer-{Guid.NewGuid():N}@attendta.test", password, RoleCode.Officer, ct);
        var memberUser = await MemberEventTestSupport.SeedUserAsync(
            fixture.ConnectionString, $"recipient-{Guid.NewGuid():N}@attendta.test", password, RoleCode.Member, ct);
        var (typeId, venueId) = await MemberEventTestSupport.SeedReferencesAsync(fixture.ConnectionString, officer.Id, ct);
        var title = $"Certificate issuance {Guid.NewGuid():N}";
        var startsAt = Now.AddDays(-2);
        var eventItem = await MemberEventTestSupport.SeedEventAsync(
            fixture.ConnectionString, officer.Id, typeId, venueId, EventStatusCode.Completed,
            startsAt, Now.AddDays(-3), 50, null, title, ct);
        var member = await MemberEventTestSupport.SeedMemberAsync(
            fixture.ConnectionString, memberUser.Id, MembershipStatusCode.Active, ct);
        var registration = await MemberEventTestSupport.SeedRegistrationAsync(
            fixture.ConnectionString, eventItem.EventId, member.MemberId, RegistrationStatusCode.Confirmed, officer.Id, ct);

        await using var writer = EventDraftTestSupport.Context(fixture.ConnectionString);
        writer.AttendanceRecords.Add(new Attendance
        {
            RegistrationId = registration.RegistrationId,
            CheckInMethodId = CheckInMethodCode.QrCode,
            RecordedBy = officer.Id,
            RecordedAt = Now.AddDays(-2),
            InvalidatedBy = invalidateAttendance ? officer.Id : null,
            InvalidatedAt = invalidateAttendance ? Now.AddHours(-1) : null,
            InvalidationReason = invalidateAttendance ? "Correction." : null
        });
        Certificate? certificate = null;
        if (seedReview)
        {
            certificate = new Certificate
            {
                RegistrationId = registration.RegistrationId,
                CertificateStatusId = reviewStatus,
                ReviewedBy = officer.Id,
                ReviewedAt = Now.AddDays(-1),
                CreatedBy = officer.Id,
                CreatedAt = Now.AddDays(-2)
            };
            writer.Certificates.Add(certificate);
        }

        await writer.SaveChangesAsync(ct);

        return new Setup(
            officer.Id,
            officer.Name,
            memberUser.Name,
            eventItem.EventId,
            title,
            startsAt,
            registration.RegistrationId,
            certificate?.CertificateId ?? 0);
    }

    private async Task AssertUnchangedAsync(int certificateId, CertificateStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        var certificate = await observer.Certificates.AsNoTracking()
            .SingleAsync(row => row.CertificateId == certificateId, ct);
        Assert.Equal(expected, certificate.CertificateStatusId);
        Assert.Null(certificate.CertificateNumber);
        Assert.Null(certificate.FilePath);
        Assert.Null(certificate.IssuedBy);
        Assert.Null(certificate.IssuedAt);
        Assert.Equal(0, await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.EntityType == "certificates" && row.EntityId == certificateId, ct));
    }

    private static async Task<DomainException?> CaptureAsync(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (DomainException exception)
        {
            return exception;
        }
    }

    private async Task<int> IssuedAuditCountAsync(int certificateId, CancellationToken cancellationToken)
    {
        await using var observer = EventDraftTestSupport.Context(fixture.ConnectionString);
        return await observer.ActivityLogs.AsNoTracking()
            .CountAsync(row => row.Action == AuditActions.CertificateIssued && row.EntityId == certificateId, cancellationToken);
    }

    private static async Task<byte[]> ReadStoredAsync(
        IPrivateFileStore store,
        string relativePath,
        CancellationToken cancellationToken)
    {
        await using var stream = await store.OpenReadAsync(relativePath, cancellationToken);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken);
        return copy.ToArray();
    }

    private static string[] CertificateFiles(string setupRoot)
    {
        var directory = Path.Combine(setupRoot, "certificates");
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();
    }

    private static string[] TemporaryFiles(string setupRoot)
    {
        var directory = Path.Combine(setupRoot, "temporary");
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();
    }

    private static (IPrivateFileStore Store, string BasePath, string SetupRoot) CreateStore()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "attendta-certificate-issuance", Guid.NewGuid().ToString("N"));
        var appRoot = Path.Combine(basePath, "app");
        var setupRoot = Path.Combine(basePath, "private");
        var webRoot = Path.Combine(appRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Common:SetupRoot"] = setupRoot
            })
            .Build();
        var environment = new TestWebHostEnvironment(appRoot, webRoot);
        return (new PrivateFileStore(configuration, environment), basePath, setupRoot);
    }

    private sealed record Setup(
        int OfficerId,
        string OfficerName,
        string RecipientName,
        int EventId,
        string EventTitle,
        DateTime EventStartsAt,
        int RegistrationId,
        int CertificateId);

    private sealed class ThrowingAuditWriter : IActivityLogWriter
    {
        public void Append(AuditEntry entry) =>
            throw new InvalidOperationException("The audit write failed.");
    }

    private sealed class FakePdfRenderer(byte[]? bytes = null) : ICertificatePdfRenderer
    {
        public Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.FromResult(bytes ?? PdfBytes());

        internal static byte[] PdfBytes() => System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n% AttendTa certificate\n");
    }

    private sealed class FailingPdfRenderer : ICertificatePdfRenderer
    {
        public Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.FromException<byte[]>(new IOException("The renderer failed."));
    }

    private sealed class MutatingPdfRenderer(Func<Task> mutate) : ICertificatePdfRenderer
    {
        public async Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            await mutate();
            return FakePdfRenderer.PdfBytes();
        }
    }

    /// <summary>
    /// Holds every render until the expected number of issuances are inside the renderer, so the
    /// concurrent write race is deterministic instead of timing-dependent.
    /// </summary>
    private sealed class GatedPdfRenderer(int expected) : ICertificatePdfRenderer
    {
        private readonly TaskCompletionSource bothRendering = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrived;

        public Task BothRendering => bothRendering.Task;
        public TaskCompletionSource Release => release;

        public async Task<byte[]> RenderSnapshotAsync(
            CertificatePdfSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrived) >= expected)
            {
                bothRendering.TrySetResult();
            }

            await release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return FakePdfRenderer.PdfBytes();
        }
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }

    private sealed class Actor(int userId, string role) : ICurrentActor
    {
        public bool IsAuthenticated => true;
        public int UserId => userId;
        public string Role => role;
    }

    private sealed class TestWebHostEnvironment(string contentRoot, string webRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AttendTa.CertificateIssuanceTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
