using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ASI.Basecode.WebApp.Hotspot;

// PLAIN ENGLISH: This file creates the two certificates the demo needs. The "root" certificate is
// the one you install once on each phone; it acts like a trusted stamp. The "server" certificate is
// the one the app shows to the phone; it contains the hotspot IP/name so the phone can match it.
// Both are created locally, so the hotspot does not need the internet or a public certificate
// authority.

/// <summary>
/// The root CA and server certificate created by <see cref="HotspotCertificateFactory"/>.
/// </summary>
public sealed record HotspotCertificateBundle(
    X509Certificate2 RootCertificate,
    X509Certificate2 RootPublicCertificate,
    X509Certificate2 ServerCertificate,
    X509Certificate2 ServerPublicCertificate);

/// <summary>
/// Creates the offline trust chain for hotspot HTTPS: a long-lived local root CA and a short-lived
/// server certificate whose SANs name the hotspot URL host. The root private key stays on the host;
/// only the public root certificate goes to phones.
/// </summary>
public static class HotspotCertificateFactory
{
    /// <summary>The root CA validity in years.</summary>
    public const int RootValidityYears = 10;

    /// <summary>
    /// The server certificate validity in days. Apple platforms reject server certificates whose
    /// lifetime exceeds 398 days, so 397 leaves a day of margin.
    /// </summary>
    public const int ServerValidityDays = 397;

    /// <summary>The server-authentication extended key usage OID.</summary>
    public const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    private const string RootSubject = "CN=AttendTa Hotspot Root CA, O=AttendTa";

    /// <summary>
    /// Creates the root CA and a server certificate for <paramref name="hosts"/>.
    /// </summary>
    /// <param name="hosts">DNS names and IP addresses placed in the server certificate SAN.</param>
    /// <param name="password">The password that protects both exported PFX files.</param>
    /// <param name="issuedAt">The issue time, for deterministic tests. Defaults to now.</param>
    public static HotspotCertificateBundle Create(
        IReadOnlyList<string> hosts,
        string password,
        DateTimeOffset? issuedAt = null)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        if (hosts.Count == 0)
        {
            throw new ArgumentException("At least one host is required.", nameof(hosts));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("A certificate password is required.", nameof(password));
        }

        var normalizedHosts = hosts
            .Select(host => host?.Trim() ?? string.Empty)
            .Where(host => host.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedHosts.Count == 0)
        {
            throw new ArgumentException("At least one host is required.", nameof(hosts));
        }

        foreach (var host in normalizedHosts)
        {
            if (!HotspotHttpsOptions.IsValidHostName(host))
            {
                throw new ArgumentException(
                    $"'{host}' is not a DNS name or IP address.", nameof(hosts));
            }
        }

        var start = (issuedAt ?? DateTimeOffset.Now).ToLocalTime();
        var rootNotBefore = start.AddMinutes(-5);
        var rootNotAfter = start.AddYears(RootValidityYears);
        var serverNotBefore = start.AddMinutes(-5);
        var serverNotAfter = start.AddDays(ServerValidityDays);

        var rootCertificate = CreateRootCertificate(rootNotBefore, rootNotAfter, password);
        var rootPublicCertificate = X509CertificateLoader.LoadCertificate(
            rootCertificate.Export(X509ContentType.Cert));

        var serverCertificate = CreateServerCertificate(
            normalizedHosts,
            rootCertificate,
            serverNotBefore,
            serverNotAfter,
            password);
        var serverPublicCertificate = X509CertificateLoader.LoadCertificate(
            serverCertificate.Export(X509ContentType.Cert));

        return new HotspotCertificateBundle(
            rootCertificate,
            rootPublicCertificate,
            serverCertificate,
            serverPublicCertificate);
    }

    /// <summary>
    /// Describes the certificate SAN entries as <c>DNS:name</c> and <c>IP:address</c> strings.
    /// </summary>
    public static IReadOnlyList<string> DescribeSubjectAlternativeNames(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var extension = FindExtension(certificate, "2.5.29.17");
        if (extension is null)
        {
            return Array.Empty<string>();
        }

        var san = new X509SubjectAlternativeNameExtension(extension.RawData, extension.Critical);
        return san.EnumerateDnsNames()
            .Select(name => "DNS:" + name)
            .Concat(san.EnumerateIPAddresses().Select(address => "IP:" + address))
            .ToList();
    }

    private static X509Certificate2 CreateRootCertificate(
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        string password)
    {
        using var key = RSA.Create(4096);
        var request = new CertificateRequest(
            new X500DistinguishedName(RootSubject),
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
                critical: true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        using var selfSigned = request.CreateSelfSigned(notBefore, notAfter);
        return X509CertificateLoader.LoadPkcs12(
            selfSigned.Export(X509ContentType.Pfx, password),
            password,
            X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 CreateServerCertificate(
        IReadOnlyList<string> hosts,
        X509Certificate2 issuer,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        string password)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={hosts[0]}, O=AttendTa"),
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid(ServerAuthenticationOid) },
                critical: false));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                issuer,
                includeKeyIdentifier: true,
                includeIssuerAndSerial: false));

        var san = new SubjectAlternativeNameBuilder();
        foreach (var host in hosts)
        {
            if (IPAddress.TryParse(host, out var address))
            {
                san.AddIpAddress(address);
            }
            else
            {
                san.AddDnsName(host);
            }
        }

        request.CertificateExtensions.Add(san.Build());

        var serialNumber = RandomNumberGenerator.GetBytes(16);
        serialNumber[0] &= 0x7F;
        if (serialNumber.All(value => value == 0))
        {
            serialNumber[^1] = 1;
        }

        using var issued = request.Create(issuer, notBefore, notAfter, serialNumber);
        using var withKey = issued.CopyWithPrivateKey(key);
        return X509CertificateLoader.LoadPkcs12(
            withKey.Export(X509ContentType.Pfx, password),
            password,
            X509KeyStorageFlags.Exportable);
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
