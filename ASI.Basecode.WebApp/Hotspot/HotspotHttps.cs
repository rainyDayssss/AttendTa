using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace ASI.Basecode.WebApp.Hotspot;

// PLAIN ENGLISH: This file does two jobs. First it opens the server certificate and checks that it
// is usable (has a private key, is in date, is allowed for server authentication, and names the
// address phones will type). Then it tells Kestrel to listen on two addresses: plain HTTP on the
// laptop itself (for the existing readiness check) and HTTPS on the hotspot address (for phones).

/// <summary>
/// Validates the hotspot server certificate and configures the two Kestrel listeners: HTTP on
/// <c>127.0.0.1</c> for the readiness check and HTTPS on the hotspot adapter IP for phones.
/// Direct Kestrel TLS is required by AD-16; there is no IIS or reverse proxy.
/// </summary>
public static class HotspotHttps
{
    /// <summary>The server-authentication extended key usage OID.</summary>
    public const string ServerAuthenticationOid = HotspotCertificateFactory.ServerAuthenticationOid;

    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private const string EnhancedKeyUsageOid = "2.5.29.37";

    /// <summary>
    /// Validates <paramref name="options"/>, loads the server certificate and registers the
    /// listeners. Validation and the certificate load happen before the host starts listening, so a
    /// bad configuration refuses to start instead of binding an unusable endpoint.
    /// </summary>
    public static void Apply(IWebHostBuilder webHost, HotspotHttpsOptions options)
    {
        ArgumentNullException.ThrowIfNull(webHost);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();
        var certificate = LoadServerCertificate(options);

        webHost.ConfigureKestrel(kestrel => ConfigureListeners(kestrel, options, certificate));
    }

    /// <summary>
    /// Registers the loopback HTTP and hotspot HTTPS listeners. Split from <see cref="Apply"/> so a
    /// test can bind a loopback-only pair without weakening the production address validation.
    /// </summary>
    internal static void ConfigureListeners(
        KestrelServerOptions kestrel,
        HotspotHttpsOptions options,
        X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(kestrel);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(certificate);

        kestrel.Listen(IPAddress.Loopback, options.HttpPort);
        kestrel.Listen(
            IPAddress.Parse(options.LanAddress),
            options.HttpsPort,
            listen => listen.UseHttps(certificate));
    }

    /// <summary>
    /// Loads the password-protected PFX and requires a private key, current validity, a
    /// server-authentication EKU and a SAN containing <see cref="HotspotHttpsOptions.PublicHostName"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The certificate cannot serve hotspot HTTPS.</exception>
    public static X509Certificate2 LoadServerCertificate(HotspotHttpsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(options.CertificatePath))
        {
            throw new InvalidOperationException(
                $"Hotspot:CertificatePath '{options.CertificatePath}' does not exist.");
        }

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(
                options.CertificatePath,
                options.CertificatePassword,
                X509KeyStorageFlags.Exportable);
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or ArgumentException)
        {
            // Never echo the password or the underlying exception detail.
            throw new InvalidOperationException(
                "The hotspot certificate could not be opened. Check Hotspot:CertificatePath and "
                + "Hotspot:CertificatePassword.");
        }

        try
        {
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    "The hotspot certificate does not contain a private key.");
            }

            var now = DateTime.Now;
            if (now < certificate.NotBefore || now > certificate.NotAfter)
            {
                throw new InvalidOperationException(
                    $"The hotspot certificate is not currently valid "
                    + $"({certificate.NotBefore:yyyy-MM-dd} to {certificate.NotAfter:yyyy-MM-dd}).");
            }

            if (!HasServerAuthenticationEku(certificate))
            {
                throw new InvalidOperationException(
                    $"The hotspot certificate does not allow server authentication "
                    + $"(EKU {ServerAuthenticationOid}).");
            }

            if (!MatchesPublicHostName(certificate, options.PublicHostName))
            {
                var sans = HotspotCertificateFactory.DescribeSubjectAlternativeNames(certificate);
                throw new InvalidOperationException(
                    $"The hotspot certificate SAN does not contain '{options.PublicHostName}'. "
                    + $"Certificate SANs: {(sans.Count == 0 ? "(none)" : string.Join(", ", sans))}.");
            }

            return certificate;
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    private static bool HasServerAuthenticationEku(X509Certificate2 certificate)
    {
        var extension = FindExtension(certificate, EnhancedKeyUsageOid);
        if (extension is null)
        {
            return false;
        }

        var usages = new X509EnhancedKeyUsageExtension(
            new AsnEncodedData(extension.RawData),
            extension.Critical);
        foreach (Oid usage in usages.EnhancedKeyUsages)
        {
            if (string.Equals(usage.Value, ServerAuthenticationOid, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesPublicHostName(X509Certificate2 certificate, string publicHostName)
    {
        var extension = FindExtension(certificate, SubjectAlternativeNameOid);
        if (extension is null)
        {
            return false;
        }

        X509SubjectAlternativeNameExtension san;
        try
        {
            san = new X509SubjectAlternativeNameExtension(
                extension.RawData,
                extension.Critical);
        }
        catch (CryptographicException)
        {
            return false;
        }

        if (IPAddress.TryParse(publicHostName, out var address))
        {
            return san.EnumerateIPAddresses().Any(candidate => candidate.Equals(address));
        }

        return san.EnumerateDnsNames()
            .Any(candidate => string.Equals(candidate, publicHostName, StringComparison.OrdinalIgnoreCase));
    }

    private static X509Extension FindExtension(X509Certificate2 certificate, string oid)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (string.Equals(extension.Oid?.Value, oid, StringComparison.Ordinal))
            {
                return extension;
            }
        }

        return null;
    }
}
