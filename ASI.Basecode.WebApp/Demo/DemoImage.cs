using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace ASI.Basecode.WebApp.Demo;

internal static class DemoImage
{
    internal static void ValidatePng(byte[] bytes)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) throw new PlatformNotSupportedException();
        if (bytes.Length is < 8 or > 2097152) throw new InvalidDataException();
        try
        {
            byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
            if (!bytes.AsSpan(0, 8).SequenceEqual(signature)) throw new InvalidDataException();
            var offset = 8;
            var first = true;
            var hasData = false;
            var ended = false;
            while (offset <= bytes.Length - 12)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
                if (length > bytes.Length - offset - 12) throw new InvalidDataException();
                var chunk = bytes.AsSpan(offset + 4, checked((int)length + 4));
                var type = chunk[..4];
                if (first && (!type.SequenceEqual("IHDR"u8) || length != 13)) throw new InvalidDataException();
                if (!first && type.SequenceEqual("IHDR"u8)) throw new InvalidDataException();
                if (type.SequenceEqual("IDAT"u8)) hasData = true;
                uint crc = 0xffffffff;
                foreach (var value in chunk)
                {
                    crc ^= value;
                    for (var bit = 0; bit < 8; bit++)
                        crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
                }
                if (~crc != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + (int)length, 4)))
                    throw new InvalidDataException();
                offset += checked((int)length + 12);
                first = false;
                if (type.SequenceEqual("IEND"u8))
                {
                    if (length != 0 || !hasData || offset != bytes.Length) throw new InvalidDataException();
                    ended = true;
                    break;
                }
            }
            if (!ended) throw new InvalidDataException();
            using var stream = new MemoryStream(bytes, writable: false);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            if (image.RawFormat.Guid != ImageFormat.Png.Guid || image.Width != 1200 || image.Height != 750)
                throw new InvalidDataException();
            // Force decoding all pixels rather than accepting only a readable header.
            using var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.DrawImageUnscaled(image, 0, 0);
        }
        catch (Exception e) when (e is ArgumentException or ExternalException or OutOfMemoryException)
        {
            throw new InvalidDataException("Invalid demo PNG.", e);
        }
    }
}
