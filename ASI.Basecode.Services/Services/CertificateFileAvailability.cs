using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.Services.Services;

/// <summary>Checks retained documents without loading their bytes or changing issuance state.</summary>
internal static class CertificateFileAvailability
{
    public static async Task<bool> IsAvailableAsync(IPrivateFileStore files, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("certificates/", StringComparison.Ordinal)) return false;
        try
        {
            await using var stream = await files.OpenReadAsync(path, cancellationToken);
            if (!stream.CanRead) return false;
            var header = new byte[5];
            await stream.ReadExactlyAsync(header, cancellationToken);
            return header.AsSpan().SequenceEqual("%PDF-"u8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            return false;
        }
    }
}
