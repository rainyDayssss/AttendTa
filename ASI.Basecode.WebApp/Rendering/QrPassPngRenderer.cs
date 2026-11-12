using System;
using Net.Codecrete.QrCodeGenerator;

namespace ASI.Basecode.WebApp.Rendering;

/// <summary>
/// Turns a pass token into PNG bytes. Rendering lives in the WebApp adapter so Services never
/// depends on an imaging library, and the library used here needs no <c>System.Drawing</c>.
/// </summary>
public interface IQrPassPngRenderer
{
    /// <summary>Encodes only the token as a square PNG at least 168 px wide.</summary>
    byte[] Render(string token);
}

/// <summary>
/// Server-side QR PNG rendering with <c>Net.Codecrete.QrCodeGenerator</c> (MIT, no dependencies,
/// no <c>System.Drawing</c>). The image carries a 4-module quiet zone and is scaled so the result
/// is at least 168 px, which keeps the card's existing size without a blurry upscale.
/// </summary>
public sealed class QrPassPngRenderer : IQrPassPngRenderer
{
    /// <summary>The smallest image the pass card shows.</summary>
    public const int MinimumPixelSize = 168;

    /// <summary>The quiet zone, in modules, that scanners expect around the code.</summary>
    public const int QuietZoneModules = 4;

    public byte[] Render(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("A pass token is required to render a QR image.", nameof(token));
        }

        var qrCode = QrCode.EncodeText(token, QrCode.Ecc.Medium);

        // The encoded size includes the quiet zone the library adds on request.
        var modules = qrCode.Size + (QuietZoneModules * 2);
        var scale = Math.Max(1, (MinimumPixelSize + modules - 1) / modules);

        // The border is a factor of the module size, so 4 modules is a border of 4.
        return qrCode.ToPngBitmap(QuietZoneModules, scale);
    }
}
