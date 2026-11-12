using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.Text;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class PrivateFileStoreTests
{
    [Fact]
    public async Task SavesImagesAtTwoMibBoundaryAndCleansRejectedUploads()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        try
        {
            var bytes = JpegBytes(2 * 1024 * 1024);
            var relativePath = await store.SaveAsync(PrivateFileArea.VenueImages, new MemoryStream(bytes), ".jpg", cancellationToken);

            Assert.StartsWith("uploads/venues/", relativePath, StringComparison.Ordinal);
            Assert.EndsWith(".jpg", relativePath, StringComparison.Ordinal);
            Assert.Equal(bytes.Length, new FileInfo(Resolve(setupRoot, relativePath)).Length);

            var eventBytes = JpegBytes(2 * 1024 * 1024);
            var eventPath = await store.SaveAsync(PrivateFileArea.EventImages, new MemoryStream(eventBytes), ".jpg", cancellationToken);
            Assert.StartsWith("uploads/events/", eventPath, StringComparison.Ordinal);
            Assert.Equal(eventBytes.Length, new FileInfo(Resolve(setupRoot, eventPath)).Length);

            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(
                PrivateFileArea.VenueImages,
                new MemoryStream(JpegBytes(2 * 1024 * 1024 + 1)),
                ".jpg",
                cancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(
                PrivateFileArea.EventImages,
                new MemoryStream(JpegBytes(32)),
                ".png",
                cancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(
                PrivateFileArea.EventImages,
                new MemoryStream(JpegBytes(2 * 1024 * 1024 + 1)),
                ".jpg",
                cancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(
                PrivateFileArea.VenueImages,
                new MemoryStream(Encoding.UTF8.GetBytes("not an image")),
                ".jpg",
                cancellationToken));

            Assert.Single(Directory.GetFiles(Path.Combine(setupRoot, "uploads", "venues")));
            Assert.Single(Directory.GetFiles(Path.Combine(setupRoot, "uploads", "events")));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task DoesNotDeleteAnExistingFileWhenExclusiveCreationFindsACollision()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var collisionId = Guid.NewGuid();
        var (store, basePath, setupRoot) = CreateStore(() => collisionId);

        try
        {
            var originalBytes = JpegBytes(32);
            var relativePath = await store.SaveAsync(
                PrivateFileArea.VenueImages,
                new MemoryStream(originalBytes),
                ".jpg",
                cancellationToken);

            await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(
                PrivateFileArea.VenueImages,
                new MemoryStream(JpegBytes(64)),
                ".jpg",
                cancellationToken));

            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(Resolve(setupRoot, relativePath), cancellationToken));
            Assert.Single(Directory.GetFiles(Path.Combine(setupRoot, "uploads", "venues")));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public async Task StoresAndPromotesOnlyValidPdfAndRejectsTraversal()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, basePath, setupRoot) = CreateStore();
        var outsideFile = Path.Combine(basePath, "outside.jpg");
        await File.WriteAllBytesAsync(outsideFile, JpegBytes(12), cancellationToken);

        try
        {
            var temporaryPdf = store.CreateTemporaryPath(".pdf");
            var pdfBytes = new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-', 1, 2, 3 };
            await File.WriteAllBytesAsync(temporaryPdf, pdfBytes, cancellationToken);

            var certificatePath = await store.PromoteAsync(temporaryPdf, PrivateFileArea.Certificates, cancellationToken);
            Assert.StartsWith("certificates/", certificatePath, StringComparison.Ordinal);
            Assert.False(File.Exists(temporaryPdf));

            await using var certificate = await store.OpenReadAsync(certificatePath, cancellationToken);
            using var copy = new MemoryStream();
            await certificate.CopyToAsync(copy, cancellationToken);
            Assert.Equal(pdfBytes, copy.ToArray());

            var invalidPdf = store.CreateTemporaryPath(".pdf");
            await File.WriteAllBytesAsync(invalidPdf, JpegBytes(16), cancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => store.PromoteAsync(invalidPdf, PrivateFileArea.Certificates, cancellationToken));
            store.Delete(invalidPdf);

            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.OpenReadAsync("uploads/venues/../../outside.jpg", cancellationToken));
            Assert.Throws<InvalidDataException>(() => store.Delete("uploads/venues/../../outside.jpg"));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.PromoteAsync(outsideFile, PrivateFileArea.Certificates, cancellationToken));
            Assert.True(File.Exists(outsideFile));
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Fact]
    public void RejectsAMissingSetupRoot()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "attendta-private-store", Guid.NewGuid().ToString("N"));
        var appRoot = Path.Combine(basePath, "app");
        var webRoot = Path.Combine(appRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);

        try
        {
            var configuration = new ConfigurationBuilder().Build();
            var exception = Assert.Throws<InvalidOperationException>(
                () => new PrivateFileStore(configuration, new TestWebHostEnvironment(appRoot, webRoot)));
            Assert.Contains("must be configured for private file storage", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    [Theory]
    [InlineData("web")]
    [InlineData("content")]
    public void RejectsASetupRootInsideTheWebOrContentRoot(string placement)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "attendta-private-store", Guid.NewGuid().ToString("N"));
        var appRoot = Path.Combine(basePath, "app");
        var webRoot = Path.Combine(appRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Common:SetupRoot"] = placement == "web" ? webRoot : appRoot
                })
                .Build();
            var exception = Assert.Throws<InvalidOperationException>(
                () => new PrivateFileStore(configuration, new TestWebHostEnvironment(appRoot, webRoot)));
            Assert.Contains("outside wwwroot and the application deployment directory", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    private static (IPrivateFileStore Store, string BasePath, string SetupRoot) CreateStore(Func<Guid>? createFileId = null)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "attendta-private-store", Guid.NewGuid().ToString("N"));
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
        return (new PrivateFileStore(configuration, environment, createFileId ?? Guid.NewGuid), basePath, setupRoot);
    }

    private static byte[] JpegBytes(int length)
    {
        var bytes = new byte[length];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static string Resolve(string root, string relativePath) => Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private sealed class TestWebHostEnvironment(string contentRoot, string webRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AttendTa.PrivateFileStoreTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
