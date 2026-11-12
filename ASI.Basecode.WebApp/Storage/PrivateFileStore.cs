using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Storage;

/// <summary>Server-local private storage rooted at Common:SetupRoot.</summary>
public sealed class PrivateFileStore : IPrivateFileStore
{
    private const long MaxImageSizeBytes = 2L * 1024 * 1024;
    private readonly string setupRoot;
    private readonly string temporaryRoot;
    private readonly Func<Guid> createFileId;

    public PrivateFileStore(IConfiguration configuration, IWebHostEnvironment environment)
        : this(configuration, environment, Guid.NewGuid)
    {
    }

    internal PrivateFileStore(IConfiguration configuration, IWebHostEnvironment environment, Func<Guid> createFileId)
    {
        this.createFileId = createFileId ?? throw new ArgumentNullException(nameof(createFileId));
        var configuredRoot = configuration.GetSetupRootDirectoryPath();
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException("Common:SetupRoot must be configured for private file storage.");
        }

        setupRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        if (PathsOverlap(setupRoot, webRoot) || PathsOverlap(setupRoot, contentRoot))
        {
            throw new InvalidOperationException("Common:SetupRoot must be outside wwwroot and the application deployment directory.");
        }

        temporaryRoot = Path.Combine(setupRoot, "temporary");
    }

    public async Task<string> SaveAsync(PrivateFileArea area, Stream content, string extension, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
        {
            throw new InvalidDataException("The supplied file stream cannot be read.");
        }

        if (area == PrivateFileArea.Temporary)
        {
            var tempPath = CreateTemporaryPath(extension);
            try
            {
                await using var output = new FileStream(tempPath, FileMode.Open, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                await CopyValidatedAsync(content, output, extension, area, cancellationToken);
                return tempPath;
            }
            catch
            {
                TryDeletePhysicalFile(tempPath);
                throw;
            }
        }

        var areaDirectory = GetPermanentAreaPath(area);
        var normalizedExtension = NormalizeExtension(extension, area);
        var relativeKey = $"{GetRelativeArea(area)}/{createFileId():N}{normalizedExtension}";
        var fullPath = ResolveRelativePath(relativeKey, area);
        Directory.CreateDirectory(areaDirectory);
        EnsureNoReparsePoints(fullPath);

        var createdByThisCall = false;
        try
        {
            await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            createdByThisCall = true;
            await CopyValidatedAsync(content, output, normalizedExtension, area, cancellationToken);
            return relativeKey;
        }
        catch
        {
            if (createdByThisCall)
            {
                TryDeletePhysicalFile(fullPath);
            }

            throw;
        }
    }

    public string CreateTemporaryPath(string extension)
    {
        var normalizedExtension = NormalizeExtension(extension, PrivateFileArea.Temporary);
        Directory.CreateDirectory(temporaryRoot);
        EnsureNoReparsePoints(temporaryRoot);
        var fullPath = Path.Combine(temporaryRoot, $"{createFileId():N}{normalizedExtension}");
        using (new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
        }

        return fullPath;
    }

    public async Task<string> PromoteAsync(string temporaryPath, PrivateFileArea area, CancellationToken cancellationToken = default)
    {
        if (area == PrivateFileArea.Temporary)
        {
            throw new InvalidDataException("A temporary file cannot be promoted into the temporary area.");
        }

        var sourcePath = ResolveGeneratedTemporaryPath(temporaryPath);
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var normalizedExtension = NormalizeExtension(extension, area);
        var relativeKey = $"{GetRelativeArea(area)}/{createFileId():N}{normalizedExtension}";
        var destinationPath = ResolveRelativePath(relativeKey, area);
        Directory.CreateDirectory(GetPermanentAreaPath(area));

        cancellationToken.ThrowIfCancellationRequested();
        await using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await ValidateContentAsync(input, normalizedExtension, area, cancellationToken);
        }

        File.Move(sourcePath, destinationPath, overwrite: false);
        return relativeKey;
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var area = ParseRelativeArea(relativePath);
        if (area == PrivateFileArea.Temporary)
        {
            throw new InvalidDataException("Temporary files cannot be opened by a stored relative key.");
        }

        var fullPath = ResolveRelativePath(relativePath, area);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public void Delete(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        if (Path.IsPathRooted(relativePath))
        {
            var temporaryPath = ResolveGeneratedTemporaryPath(relativePath);
            TryDeletePhysicalFile(temporaryPath);
            return;
        }

        var area = ParseRelativeArea(relativePath);
        if (area == PrivateFileArea.Temporary)
        {
            throw new InvalidDataException("Temporary files must be deleted by their generated physical path.");
        }

        TryDeletePhysicalFile(ResolveRelativePath(relativePath, area));
    }

    private async Task CopyValidatedAsync(Stream source, Stream destination, string extension, PrivateFileArea area, CancellationToken cancellationToken)
    {
        var normalizedExtension = NormalizeExtension(extension, area);
        var headerLength = GetSignatureLength(area);
        var header = new byte[headerLength];
        var bytesRead = await ReadHeaderAsync(source, header, cancellationToken);
        ValidateSignature(header.AsSpan(0, bytesRead), normalizedExtension, area);

        long totalBytes = bytesRead;
        EnforceMaximumSize(totalBytes, area);
        if (bytesRead > 0)
        {
            await destination.WriteAsync(header.AsMemory(0, bytesRead), cancellationToken);
        }

        var buffer = new byte[81920];
        while (true)
        {
            var count = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
            {
                break;
            }

            totalBytes += count;
            EnforceMaximumSize(totalBytes, area);
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
    }

    private async Task ValidateContentAsync(Stream source, string extension, PrivateFileArea area, CancellationToken cancellationToken)
    {
        var header = new byte[GetSignatureLength(area)];
        var bytesRead = await ReadHeaderAsync(source, header, cancellationToken);
        ValidateSignature(header.AsSpan(0, bytesRead), extension, area);

        long totalBytes = bytesRead;
        EnforceMaximumSize(totalBytes, area);
        var buffer = new byte[81920];
        while (true)
        {
            var count = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
            {
                break;
            }

            totalBytes += count;
            EnforceMaximumSize(totalBytes, area);
        }
    }

    private static async Task<int> ReadHeaderAsync(Stream source, byte[] header, CancellationToken cancellationToken)
    {
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var count = await source.ReadAsync(header.AsMemory(bytesRead, header.Length - bytesRead), cancellationToken);
            if (count == 0)
            {
                break;
            }

            bytesRead += count;
        }

        return bytesRead;
    }

    private static void ValidateSignature(ReadOnlySpan<byte> header, string extension, PrivateFileArea area)
    {
        if (area is PrivateFileArea.VenueImages or PrivateFileArea.EventImages)
        {
            var isPng = header.Length == 8
                && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
            var isJpeg = header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            if ((extension == ".png" && !isPng) || ((extension == ".jpg" || extension == ".jpeg") && !isJpeg))
            {
                throw new InvalidDataException("The uploaded file content does not match a supported image format.");
            }

            return;
        }

        if (area == PrivateFileArea.Certificates
            && (header.Length < 5 || header[0] != (byte)'%' || header[1] != (byte)'P' || header[2] != (byte)'D' || header[3] != (byte)'F' || header[4] != (byte)'-'))
        {
            throw new InvalidDataException("Certificate files must be PDFs.");
        }
    }

    private static void EnforceMaximumSize(long length, PrivateFileArea area)
    {
        if ((area is PrivateFileArea.VenueImages or PrivateFileArea.EventImages) && length > MaxImageSizeBytes)
        {
            throw new InvalidDataException("Venue and event images must be 2 MiB or smaller.");
        }
    }

    private string ResolveRelativePath(string relativePath, PrivateFileArea expectedArea)
    {
        var actualArea = ParseRelativeArea(relativePath);
        if (actualArea != expectedArea)
        {
            throw new InvalidDataException("The file key does not match the requested private area.");
        }

        var segments = ValidateRelativeSegments(relativePath);
        NormalizeExtension(Path.GetExtension(segments[^1]), expectedArea);
        var fullPath = Path.GetFullPath(Path.Combine(setupRoot, Path.Combine(segments)));
        if (!IsSameOrDescendant(fullPath, Path.Combine(setupRoot, GetRelativeArea(expectedArea))))
        {
            throw new InvalidDataException("The file key is outside its private storage area.");
        }

        EnsureNoReparsePoints(fullPath);

        return fullPath;
    }

    private string ResolveGeneratedTemporaryPath(string temporaryPath)
    {
        if (string.IsNullOrWhiteSpace(temporaryPath) || !Path.IsPathRooted(temporaryPath))
        {
            throw new InvalidDataException("A generated private temporary path is required.");
        }

        var fullPath = Path.GetFullPath(temporaryPath);
        if (!IsSameOrDescendant(fullPath, temporaryRoot)
            || string.Equals(fullPath, temporaryRoot, GetPathComparison())
            || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(fullPath), "N", out _))
        {
            throw new InvalidDataException("The temporary file path is unsafe.");
        }

        NormalizeExtension(Path.GetExtension(fullPath), PrivateFileArea.Temporary);
        EnsureNoReparsePoints(fullPath);

        return fullPath;
    }

    private static string[] ValidateRelativeSegments(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\')
            || relativePath.Contains(':'))
        {
            throw new InvalidDataException("Stored private file keys must be safe relative paths using '/'.");
        }

        var segments = relativePath.Split('/');
        if (segments.Length < 2)
        {
            throw new InvalidDataException("The stored private file key is incomplete.");
        }

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
            {
                throw new InvalidDataException("The stored private file key contains an unsafe path segment.");
            }
        }

        return segments;
    }

    private static PrivateFileArea ParseRelativeArea(string relativePath)
    {
        var segments = ValidateRelativeSegments(relativePath);
        var area = (segments[0], segments[1]) switch
        {
            ("uploads", "venues") => PrivateFileArea.VenueImages,
            ("uploads", "events") => PrivateFileArea.EventImages,
            ("certificates", _) => PrivateFileArea.Certificates,
            ("temporary", _) => PrivateFileArea.Temporary,
            _ => throw new InvalidDataException("The stored key is not in a supported private area.")
        };

        var expectedSegmentCount = area is PrivateFileArea.VenueImages or PrivateFileArea.EventImages ? 3 : 2;
        if (segments.Length != expectedSegmentCount)
        {
            throw new InvalidDataException("The stored private file key has an invalid path structure.");
        }

        return area;
    }

    private string GetPermanentAreaPath(PrivateFileArea area) => Path.Combine(setupRoot, GetRelativeArea(area).Replace('/', Path.DirectorySeparatorChar));

    private static string GetRelativeArea(PrivateFileArea area) => area switch
    {
        PrivateFileArea.VenueImages => "uploads/venues",
        PrivateFileArea.EventImages => "uploads/events",
        PrivateFileArea.Certificates => "certificates",
        PrivateFileArea.Temporary => "temporary",
        _ => throw new InvalidDataException("The requested private area is not supported.")
    };

    private static string NormalizeExtension(string extension, PrivateFileArea area)
    {
        var normalized = extension?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized) || !normalized.StartsWith(".", StringComparison.Ordinal))
        {
            throw new InvalidDataException("A supported file extension is required.");
        }

        var isImage = normalized is ".jpg" or ".jpeg" or ".png";
        var valid = area switch
        {
            PrivateFileArea.VenueImages or PrivateFileArea.EventImages => isImage,
            PrivateFileArea.Certificates => normalized == ".pdf",
            PrivateFileArea.Temporary => isImage || normalized == ".pdf",
            _ => false
        };

        if (!valid)
        {
            throw new InvalidDataException(area is PrivateFileArea.VenueImages or PrivateFileArea.EventImages
                ? "Venue and event images must use a JPEG or PNG extension."
                : area == PrivateFileArea.Certificates
                    ? "Certificate files must use a PDF extension."
                    : "Temporary files must use a JPEG, PNG or PDF extension.");
        }

        return normalized;
    }

    private static int GetSignatureLength(PrivateFileArea area) => area switch
    {
        PrivateFileArea.VenueImages or PrivateFileArea.EventImages => 8,
        PrivateFileArea.Certificates => 5,
        _ => 0
    };

    private static bool PathsOverlap(string first, string second) => IsSameOrDescendant(first, second) || IsSameOrDescendant(second, first);

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        var comparison = GetPathComparison();
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        return string.Equals(normalizedCandidate, normalizedRoot, comparison)
            || normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static StringComparison GetPathComparison() => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private void EnsureNoReparsePoints(string fullPath)
    {
        var current = Path.GetFullPath(fullPath);
        while (IsSameOrDescendant(current, setupRoot))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Private storage paths must not contain symbolic links or reparse points.");
            }

            if (string.Equals(current, setupRoot, GetPathComparison()))
            {
                break;
            }

            current = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(current))
            {
                break;
            }
        }
    }

    private static void TryDeletePhysicalFile(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
