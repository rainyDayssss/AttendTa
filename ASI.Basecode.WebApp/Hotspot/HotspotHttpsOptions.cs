using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace ASI.Basecode.WebApp.Hotspot;

// PLAIN ENGLISH: This file reads the "Hotspot" settings from appsettings or environment variables.
// It checks that the address is the laptop's hotspot address (not "all addresses" and not the
// laptop itself) and that the certificate file, password and public name are present. If anything
// is wrong, the app stops before it opens a network port, so a bad setup cannot silently expose
// the app or serve a certificate the phones will reject.

/// <summary>
/// Binds and validates the <c>Hotspot</c> configuration section. The section is disabled in every
/// committed <c>appsettings*.json</c>; the demo host sets it in the process environment.
/// </summary>
/// <remarks>
/// Hotspot mode keeps HTTP on <c>127.0.0.1</c> for the readiness check and binds HTTPS only on the
/// hotspot adapter IP. A wildcard or loopback HTTPS bind is refused so the app is not exposed on
/// another network. The certificate SAN must name <see cref="PublicHostName"/> because hotspot DNS
/// is unreliable and HSTS does not apply to IP addresses.
/// </remarks>
public sealed class HotspotHttpsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Hotspot";

    /// <summary>The default HTTPS port for the hotspot listener.</summary>
    public const int DefaultHttpsPort = 5443;

    /// <summary>The default HTTP loopback port for the readiness check.</summary>
    public const int DefaultHttpPort = 5080;

    /// <summary>Turns the hotspot listeners on. Off in every committed settings file.</summary>
    public bool Enabled { get; set; }

    /// <summary>The hotspot adapter IP address, for example <c>192.168.137.1</c>.</summary>
    public string LanAddress { get; set; } = string.Empty;

    /// <summary>The HTTPS port bound on <see cref="LanAddress"/>.</summary>
    public int HttpsPort { get; set; } = DefaultHttpsPort;

    /// <summary>The HTTP port bound on <c>127.0.0.1</c> for the readiness check.</summary>
    public int HttpPort { get; set; } = DefaultHttpPort;

    /// <summary>The DNS name or IP address the server certificate must name.</summary>
    public string PublicHostName { get; set; } = string.Empty;

    /// <summary>The absolute path of the password-protected server PFX.</summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>The server PFX password. Never logged, printed or committed.</summary>
    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>The loopback HTTP endpoint used by the readiness check.</summary>
    public string HttpEndpoint => $"http://127.0.0.1:{HttpPort}";

    /// <summary>The HTTPS endpoint phones open over the hotspot.</summary>
    public string HttpsEndpoint => $"https://{FormatHost(PublicHostName)}:{HttpsPort}";

    /// <summary>
    /// Binds the section without validating it, so callers can read <see cref="Enabled"/> cheaply.
    /// Call <see cref="Validate"/> before the listeners are configured.
    /// </summary>
    public static HotspotHttpsOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        return new HotspotHttpsOptions
        {
            Enabled = section.GetValue<bool>(nameof(Enabled)),
            LanAddress = section[nameof(LanAddress)]?.Trim() ?? string.Empty,
            HttpsPort = section.GetValue<int?>(nameof(HttpsPort)) ?? DefaultHttpsPort,
            HttpPort = section.GetValue<int?>(nameof(HttpPort)) ?? DefaultHttpPort,
            PublicHostName = section[nameof(PublicHostName)]?.Trim() ?? string.Empty,
            CertificatePath = section[nameof(CertificatePath)]?.Trim() ?? string.Empty,
            CertificatePassword = section[nameof(CertificatePassword)] ?? string.Empty,
        };
    }

    /// <summary>
    /// Validates the enabled section. Returns immediately when the section is off. Every message
    /// names the configuration key and never echoes the certificate password.
    /// </summary>
    /// <exception cref="InvalidOperationException">The section cannot be used.</exception>
    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        ValidateLanAddress();
        ValidatePorts();
        ValidatePublicHostName();
        ValidateCertificate();
    }

    /// <summary>
    /// True when <paramref name="value"/> is an IP literal or a DNS host name without a scheme,
    /// port, path or wildcard.
    /// </summary>
    public static bool IsValidHostName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 253)
        {
            return false;
        }

        if (value.Contains('/') || value.Contains('\\') || value.Contains('*') || value.Contains(' '))
        {
            return false;
        }

        if (IPAddress.TryParse(value, out _))
        {
            return true;
        }

        // A colon is only legal inside an IPv6 literal.
        if (value.Contains(':'))
        {
            return false;
        }

        return Uri.CheckHostName(value) == UriHostNameType.Dns;
    }

    private void ValidateLanAddress()
    {
        if (string.IsNullOrWhiteSpace(LanAddress))
        {
            throw new InvalidOperationException(
                "Hotspot:LanAddress is required when Hotspot:Enabled is true.");
        }

        if (!IPAddress.TryParse(LanAddress, out var address))
        {
            throw new InvalidOperationException(
                "Hotspot:LanAddress must be the hotspot adapter IP address.");
        }

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback)
            || IPAddress.IsLoopback(address))
        {
            throw new InvalidOperationException(
                "Hotspot:LanAddress must be the hotspot adapter IP address; wildcard and loopback "
                + "binds are refused.");
        }
    }

    private void ValidatePorts()
    {
        if (HttpsPort is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "Hotspot:HttpsPort must be between 1 and 65535.");
        }

        if (HttpPort is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "Hotspot:HttpPort must be between 1 and 65535.");
        }

        if (HttpsPort == HttpPort)
        {
            throw new InvalidOperationException(
                "Hotspot:HttpsPort and Hotspot:HttpPort must be different.");
        }
    }

    private void ValidatePublicHostName()
    {
        if (string.IsNullOrWhiteSpace(PublicHostName))
        {
            throw new InvalidOperationException(
                "Hotspot:PublicHostName is required when Hotspot:Enabled is true.");
        }

        if (!IsValidHostName(PublicHostName))
        {
            throw new InvalidOperationException(
                "Hotspot:PublicHostName must be a DNS name or IP address without a scheme, port "
                + "or path.");
        }
    }

    private void ValidateCertificate()
    {
        if (string.IsNullOrWhiteSpace(CertificatePath))
        {
            throw new InvalidOperationException(
                "Hotspot:CertificatePath is required when Hotspot:Enabled is true.");
        }

        if (!Path.IsPathFullyQualified(CertificatePath))
        {
            throw new InvalidOperationException(
                "Hotspot:CertificatePath must be an absolute path.");
        }

        if (string.IsNullOrEmpty(CertificatePassword))
        {
            throw new InvalidOperationException(
                "Hotspot:CertificatePassword is required when Hotspot:Enabled is true.");
        }
    }

    private static string FormatHost(string host)
    {
        if (IPAddress.TryParse(host, out var address)
            && address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return $"[{host}]";
        }

        return host;
    }
}
