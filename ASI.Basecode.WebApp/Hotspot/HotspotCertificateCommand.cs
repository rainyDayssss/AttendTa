using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace ASI.Basecode.WebApp.Hotspot;

// PLAIN ENGLISH: This is the `--new-hotspot-cert` command. It takes the hotspot name/IP, creates the
// two certificates, writes four files (two public certificates and two password-protected
// private-key files), and prints only safe details such as thumbprints and names. It never prints
// the password and refuses to overwrite an existing set, so the root private key is not lost.

/// <summary>
/// The <c>--new-hotspot-cert</c> command. Creates the local root CA and server certificate, writes
/// the four files an operator needs and prints only thumbprints, SANs and paths. The password comes
/// from <c>ATTENDTA_CERT_PASSWORD</c> and is never printed.
/// </summary>
public static class HotspotCertificateCommand
{
    /// <summary>The command switch.</summary>
    public const string Argument = "--new-hotspot-cert";

    /// <summary>The environment variable that carries the PFX password.</summary>
    public const string PasswordEnvironmentVariable = "ATTENDTA_CERT_PASSWORD";

    /// <summary>The public root certificate installed on phones.</summary>
    public const string RootCertificateFileName = "attendta-hotspot-root-ca.cer";

    /// <summary>The root CA PFX. It stays on the host and is never committed or copied to a phone.</summary>
    public const string RootPfxFileName = "attendta-hotspot-root-ca.pfx";

    /// <summary>The public server certificate.</summary>
    public const string ServerCertificateFileName = "attendta-hotspot-server.cer";

    /// <summary>The password-protected server PFX Kestrel loads.</summary>
    public const string ServerPfxFileName = "attendta-hotspot-server.pfx";

    /// <summary>The default output directory when <c>--out</c> is omitted.</summary>
    public const string DefaultOutputDirectoryName = "hotspot-certificates";

    /// <summary>
    /// Runs the command. Returns 0 on success, 2 for usage errors and 1 when the files cannot be
    /// written.
    /// </summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var hosts = new List<string>();
        string outputDirectory = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, Argument, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(argument, "--host", StringComparison.Ordinal))
            {
                if (++index >= args.Length)
                {
                    return Usage(error, "--host requires a DNS name or IP address.");
                }

                hosts.Add(args[index]);
                continue;
            }

            if (string.Equals(argument, "--out", StringComparison.Ordinal))
            {
                if (++index >= args.Length)
                {
                    return Usage(error, "--out requires a directory.");
                }

                outputDirectory = args[index];
                continue;
            }

            return Usage(error, $"Unknown argument '{argument}'.");
        }

        if (hosts.Count == 0)
        {
            return Usage(error, "At least one --host is required.");
        }

        var normalizedHosts = new List<string>();
        foreach (var host in hosts)
        {
            var value = host?.Trim() ?? string.Empty;
            if (!HotspotHttpsOptions.IsValidHostName(value))
            {
                return Usage(error, $"'{host}' is not a DNS name or IP address.");
            }

            if (!normalizedHosts.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                normalizedHosts.Add(value);
            }
        }

        var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
        if (string.IsNullOrEmpty(password))
        {
            error.WriteLine($"{PasswordEnvironmentVariable} is required and is never printed.");
            return 2;
        }

        var directory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Environment.CurrentDirectory, DefaultOutputDirectoryName)
                : outputDirectory);

        var fileNames = new[]
        {
            RootCertificateFileName,
            RootPfxFileName,
            ServerCertificateFileName,
            ServerPfxFileName,
        };

        if (fileNames.Any(name => File.Exists(Path.Combine(directory, name))))
        {
            error.WriteLine(
                $"'{directory}' already contains hotspot certificate files. "
                + "Choose a new --out directory so an existing root CA private key is not overwritten.");
            return 1;
        }

        try
        {
            Directory.CreateDirectory(directory);
            var bundle = HotspotCertificateFactory.Create(normalizedHosts, password);
            try
            {
                WriteFiles(bundle, directory, password);
                WriteSummary(bundle, directory, output);
            }
            finally
            {
                bundle.RootCertificate.Dispose();
                bundle.RootPublicCertificate.Dispose();
                bundle.ServerCertificate.Dispose();
                bundle.ServerPublicCertificate.Dispose();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error.WriteLine($"The hotspot certificate files could not be written: {exception.Message}");
            return 1;
        }

        return 0;
    }

    private static void WriteFiles(
        HotspotCertificateBundle bundle,
        string directory,
        string password)
    {
        File.WriteAllBytes(
            Path.Combine(directory, RootCertificateFileName),
            bundle.RootPublicCertificate.Export(X509ContentType.Cert));
        File.WriteAllBytes(
            Path.Combine(directory, RootPfxFileName),
            bundle.RootCertificate.Export(X509ContentType.Pfx, password));
        File.WriteAllBytes(
            Path.Combine(directory, ServerCertificateFileName),
            bundle.ServerPublicCertificate.Export(X509ContentType.Cert));

        // Include the root in the server PFX so Windows can build the chain without the root PFX.
        var chain = new X509Certificate2Collection
        {
            bundle.ServerCertificate,
            bundle.RootPublicCertificate,
        };
        File.WriteAllBytes(
            Path.Combine(directory, ServerPfxFileName),
            chain.Export(X509ContentType.Pfx, password));
    }

    private static void WriteSummary(
        HotspotCertificateBundle bundle,
        string directory,
        TextWriter output)
    {
        var sans = HotspotCertificateFactory.DescribeSubjectAlternativeNames(bundle.ServerCertificate);

        output.WriteLine($"Root CA: {bundle.RootCertificate.Subject}");
        output.WriteLine($"  Thumbprint: {bundle.RootCertificate.Thumbprint}");
        output.WriteLine($"  Public certificate (install on phones): {Path.Combine(directory, RootCertificateFileName)}");
        output.WriteLine($"  Private key PFX (host only, never copy or commit): {Path.Combine(directory, RootPfxFileName)}");
        output.WriteLine($"Server certificate: {bundle.ServerCertificate.Subject}");
        output.WriteLine($"  Thumbprint: {bundle.ServerCertificate.Thumbprint}");
        output.WriteLine($"  SANs: {(sans.Count == 0 ? "(none)" : string.Join(", ", sans))}");
        output.WriteLine($"  Valid: {bundle.ServerCertificate.NotBefore:yyyy-MM-dd} to {bundle.ServerCertificate.NotAfter:yyyy-MM-dd}");
        output.WriteLine($"  Public certificate: {Path.Combine(directory, ServerCertificateFileName)}");
        output.WriteLine($"  PFX: {Path.Combine(directory, ServerPfxFileName)}");
        output.WriteLine(
            $"Password: not printed. Set Hotspot:CertificatePath to the server PFX and "
            + $"Hotspot:CertificatePassword from {PasswordEnvironmentVariable}.");
    }

    private static int Usage(TextWriter error, string reason)
    {
        error.WriteLine(reason);
        error.WriteLine(
            $"Usage: {Argument} --host <dns-or-ip> [--host <dns-or-ip> ...] [--out <directory>]");
        return 2;
    }
}
