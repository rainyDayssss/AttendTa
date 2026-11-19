using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>
/// Canonical inventory of already-opened backup files. This class does not establish
/// path ownership or create a backup; its caller must securely open each file.
/// </summary>
internal static class DemoBackupManifest
{
    internal sealed record Entry(string Path, long Length, string Sha256);
    private sealed record Document(int Version, Entry[] Files);

    internal static async Task<Entry> MeasureAsync(string relativePath, Stream stream, CancellationToken cancellationToken)
    {
        ValidatePath(relativePath);
        if (!stream.CanRead || !stream.CanSeek)
            throw new InvalidOperationException("A readable, seekable file is required.");
        stream.Position = 0;
        var length = stream.Length;
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        if (stream.Position != length || stream.Length != length)
            throw new IOException("File changed while it was measured.");
        return new Entry(relativePath, length, Convert.ToHexString(hash).ToLowerInvariant());
    }

    internal static byte[] Serialize(IEnumerable<Entry> entries)
    {
        var files = entries.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
        ValidateEntries(files);
        return JsonSerializer.SerializeToUtf8Bytes(new Document(1, files));
    }

    internal static IReadOnlyList<Entry> Parse(byte[] json)
    {
        if (json.Length > 1024 * 1024) throw new InvalidDataException("Manifest is too large.");
        try
        {
            var document = JsonSerializer.Deserialize<Document>(json);
            if (document is null || document.Version != 1 || document.Files is null)
                throw new InvalidDataException("Invalid backup manifest.");
            ValidateEntries(document.Files);
            return document.Files;
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Invalid backup manifest.");
        }
    }

    private static void ValidateEntries(Entry[] entries)
    {
        if (entries.Length == 0 || entries.Length > 10000)
            throw new InvalidDataException("Invalid backup manifest.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry is null) throw new InvalidDataException("Invalid backup manifest.");
            ValidatePath(entry.Path);
            if (!paths.Add(entry.Path) || entry.Length < 0 || entry.Sha256 is null || entry.Sha256.Length != 64 ||
                entry.Sha256.Any(c => !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))))
                throw new InvalidDataException("Invalid backup manifest.");
        }
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 1024 || path[0] == '/' ||
            path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl) ||
            path.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
            throw new InvalidDataException("Invalid backup manifest path.");
    }
}
