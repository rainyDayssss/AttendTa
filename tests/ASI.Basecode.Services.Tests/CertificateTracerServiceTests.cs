using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Xunit;

namespace ASI.Basecode.Services.Tests;

public sealed class CertificateTracerServiceTests
{
    private static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.7 sample");

    [Fact]
    public async Task RejectsUnknownOrUnconfirmedRegistrationBeforeRenderingOrStorage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var registrations = new FakeRegistrationRepository { IsConfirmed = false };
        var renderer = new FakeRenderer();
        var files = new FakePrivateFileStore();
        var service = CreateService(registrations, renderer, files);

        Assert.Null(await service.CreateSamplePdfDownloadAsync(72, cancellationToken));
        Assert.Null(await service.CreateSamplePdfDownloadAsync(0, cancellationToken));
        Assert.Equal(1, registrations.LookupCount);
        Assert.Equal(0, renderer.CallCount);
        Assert.Equal(0, files.SaveCount);
    }

    [Fact]
    public async Task PromotesAndOpensSampleThenDeletesCertificateOnlyWhenDownloadIsDisposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var files = new FakePrivateFileStore();
        var service = CreateService(new FakeRegistrationRepository { IsConfirmed = true }, new FakeRenderer(), files);

        var download = await service.CreateSamplePdfDownloadAsync(72, cancellationToken);
        Assert.NotNull(download);
        Assert.Equal(PrivateFileArea.Temporary, files.SavedArea);
        Assert.Equal(PrivateFileArea.Certificates, files.PromotedArea);
        Assert.Empty(files.DeletedPaths);

        await using (download)
        {
            using var content = new MemoryStream();
            await download.CopyToAsync(content, cancellationToken);
            Assert.Equal(SamplePdf, content.ToArray());
            Assert.Empty(files.DeletedPaths);
        }

        Assert.Equal(new[] { FakePrivateFileStore.CertificateKey }, files.DeletedPaths);
    }

    [Fact]
    public async Task RendererFailureDoesNotCreateOrLeaveAnyFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var files = new FakePrivateFileStore();
        var service = CreateService(
            new FakeRegistrationRepository { IsConfirmed = true },
            new FakeRenderer { Failure = new InvalidOperationException("render failed") },
            files);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSamplePdfDownloadAsync(72, cancellationToken));
        Assert.Equal(0, files.SaveCount);
        Assert.Empty(files.DeletedPaths);
    }

    [Fact]
    public async Task PromotionFailureDeletesOwnedTemporaryFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var files = new FakePrivateFileStore { PromoteFailure = new IOException("promotion failed") };
        var service = CreateService(new FakeRegistrationRepository { IsConfirmed = true }, new FakeRenderer(), files);

        await Assert.ThrowsAsync<IOException>(() => service.CreateSamplePdfDownloadAsync(72, cancellationToken));
        Assert.Equal(new[] { FakePrivateFileStore.TemporaryPath }, files.DeletedPaths);
    }

    [Fact]
    public async Task OpenFailureDeletesPromotedCertificate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var files = new FakePrivateFileStore { OpenFailure = new IOException("open failed") };
        var service = CreateService(new FakeRegistrationRepository { IsConfirmed = true }, new FakeRenderer(), files);

        await Assert.ThrowsAsync<IOException>(() => service.CreateSamplePdfDownloadAsync(72, cancellationToken));
        Assert.Equal(new[] { FakePrivateFileStore.CertificateKey }, files.DeletedPaths);
    }

    [Fact]
    public async Task ReadFailureDisposesStreamBeforeDeletingPromotedCertificate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var failingStream = new FailingReadStream();
        var files = new FakePrivateFileStore { ReadStream = failingStream };
        var service = CreateService(new FakeRegistrationRepository { IsConfirmed = true }, new FakeRenderer(), files);
        var download = await service.CreateSamplePdfDownloadAsync(72, cancellationToken);
        Assert.NotNull(download);

        await Assert.ThrowsAsync<IOException>(() => download.CopyToAsync(Stream.Null, cancellationToken));
        await download.DisposeAsync();

        Assert.True(failingStream.IsDisposed);
        Assert.Equal(new[] { FakePrivateFileStore.CertificateKey }, files.DeletedPaths);
    }

    private static CertificateTracerService CreateService(
        IRegistrationRepository registrations,
        ICertificatePdfRenderer renderer,
        IPrivateFileStore files) => new(registrations, renderer, files, NullLogger<CertificateTracerService>.Instance);

    private sealed class FakeRegistrationRepository : IRegistrationRepository
    {
        public Task<RegistrationReviewIdentity> DiscoverReviewAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyPassRow>> GetMyPassRowsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OwnedPassRow> GetOwnedPassAsync(int registrationId, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> FindConfirmedRegistrationIdByTokenAsync(int eventId, string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RegistrationPromotionCandidate> DiscoverPromotionCandidateAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasEligibleWaitlistAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasValidAttendanceForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasIssuedCertificateForEventAsync(int eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountReviewQueueAsync(ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode status, int? eventId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RegistrationReviewRow>> GetReviewQueueAsync(ASI.Basecode.Data.Models.Lookups.RegistrationStatusCode status, int? eventId, int offset, int limit, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Registration> GetForCancellationAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Registration>> GetOpenForEventAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<int>> DiscoverOpenEventIdsForUserAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpcomingRegistrationRow>> GetUpcomingForUserAsync(int id, DateTime now, int? eventId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public bool IsConfirmed { get; init; }
        public int LookupCount { get; private set; }

        public Task<bool> IsConfirmedAsync(int registrationId, CancellationToken cancellationToken = default)
        {
            LookupCount++;
            return Task.FromResult(IsConfirmed);
        }

        public Task<bool> ExistsAsync(int eventId, int memberId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MemberRegistrationRow> GetMemberRegistrationAsync(int eventId, int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountMyRegistrationsAsync(int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyEventRegistrationRow>> GetMyRegistrationsAsync(int userId, int skip, int take, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountConfirmedAsync(int eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Registration registration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRenderer : ICertificatePdfRenderer
    {
        public Exception? Failure { get; init; }
        public int CallCount { get; private set; }

        public Task<byte[]> RenderSampleAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(SamplePdf);
        }

        public Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default) =>
            RenderSampleAsync(cancellationToken);
    }

    private sealed class FakePrivateFileStore : IPrivateFileStore
    {
        public const string TemporaryPath = "C:/private/temporary/sample.pdf";
        public const string CertificateKey = "certificates/sample.pdf";
        public int SaveCount { get; private set; }
        public PrivateFileArea? SavedArea { get; private set; }
        public PrivateFileArea? PromotedArea { get; private set; }
        public Exception? PromoteFailure { get; init; }
        public Exception? OpenFailure { get; init; }
        public Stream? ReadStream { get; init; }
        public List<string> DeletedPaths { get; } = new();

        public Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            SavedArea = area;
            return Task.FromResult(TemporaryPath);
        }

        public string CreateTemporaryPath(string extension) => TemporaryPath;

        public Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default)
        {
            PromotedArea = area;
            if (PromoteFailure is not null)
            {
                throw PromoteFailure;
            }

            return Task.FromResult(CertificateKey);
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (OpenFailure is not null)
            {
                throw OpenFailure;
            }

            return Task.FromResult(ReadStream ?? new MemoryStream(SamplePdf, writable: false));
        }

        public void Delete(string relativePath) => DeletedPaths.Add(relativePath);
    }

    private sealed class FailingReadStream : Stream
    {
        public bool IsDisposed { get; private set; }
        public override bool CanRead => !IsDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("read failed");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(new IOException("read failed"));
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
