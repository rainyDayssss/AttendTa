using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ASI.Basecode.WebApp.Hotspot;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

// PLAIN ENGLISH: These tests prove the certificate tool creates a real, usable trust chain: the
// server certificate contains the hotspot name/IP, is allowed for HTTPS, has a private key, chains
// to the root, and cannot be opened with the wrong password. They also prove the command writes the
// four files and never prints the password.
public class HotspotCertificateTests
{
    private const string Password = "HotspotTest#2026";

    [Fact]
    public void GeneratedChainHasRequestedSansServerAuthEkuPrivateKeyAndValidity()
    {
        var issuedAt = DateTimeOffset.Now;
        var bundle = HotspotCertificateFactory.Create(
            new[] { "attendta-demo.local", "192.168.137.1" },
            Password,
            issuedAt);

        try
        {
            Assert.True(bundle.RootCertificate.HasPrivateKey);
            Assert.True(bundle.ServerCertificate.HasPrivateKey);

            var rootDays = (bundle.RootCertificate.NotAfter - bundle.RootCertificate.NotBefore).TotalDays;
            Assert.InRange(rootDays, 3650, 3660);

            var serverDays = (bundle.ServerCertificate.NotAfter - bundle.ServerCertificate.NotBefore).TotalDays;
            Assert.InRange(serverDays, 396, 398);

            var sans = HotspotCertificateFactory.DescribeSubjectAlternativeNames(bundle.ServerCertificate);
            Assert.Contains("DNS:attendta-demo.local", sans);
            Assert.Contains("IP:192.168.137.1", sans);

            var ekuExtension = FindExtension(bundle.ServerCertificate, "2.5.29.37");
            Assert.NotNull(ekuExtension);
            var eku = new X509EnhancedKeyUsageExtension(
                new AsnEncodedData(ekuExtension.RawData),
                ekuExtension.Critical);
            Assert.Contains(
                eku.EnhancedKeyUsages.Cast<Oid>(),
                oid => oid.Value == HotspotCertificateFactory.ServerAuthenticationOid);

            // The authority key identifier lets strict clients build the chain to the root.
            Assert.NotNull(FindExtension(bundle.ServerCertificate, "2.5.29.35"));
        }
        finally
        {
            bundle.RootCertificate.Dispose();
            bundle.RootPublicCertificate.Dispose();
            bundle.ServerCertificate.Dispose();
            bundle.ServerPublicCertificate.Dispose();
        }
    }

    [Fact]
    public void ServerCertificateChainsToTheRootCa()
    {
        var bundle = HotspotCertificateFactory.Create(new[] { "192.168.137.1" }, Password);
        try
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(bundle.RootPublicCertificate);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.DisableCertificateDownloads = true;

            var built = chain.Build(bundle.ServerCertificate);

            Assert.True(
                built,
                string.Join("; ", chain.ChainStatus.Select(status => status.StatusInformation)));
        }
        finally
        {
            bundle.RootCertificate.Dispose();
            bundle.RootPublicCertificate.Dispose();
            bundle.ServerCertificate.Dispose();
            bundle.ServerPublicCertificate.Dispose();
        }
    }

    [Fact]
    public void PfxRoundTripRequiresThePassword()
    {
        var bundle = HotspotCertificateFactory.Create(new[] { "192.168.137.1" }, Password);
        try
        {
            var pfx = bundle.ServerCertificate.Export(X509ContentType.Pfx, Password);

            using var loaded = X509CertificateLoader.LoadPkcs12(
                pfx,
                Password,
                X509KeyStorageFlags.Exportable);
            Assert.True(loaded.HasPrivateKey);

            Assert.Throws<CryptographicException>(
                () => X509CertificateLoader.LoadPkcs12(
                    pfx,
                    "wrong-password",
                    X509KeyStorageFlags.Exportable));
        }
        finally
        {
            bundle.RootCertificate.Dispose();
            bundle.RootPublicCertificate.Dispose();
            bundle.ServerCertificate.Dispose();
            bundle.ServerPublicCertificate.Dispose();
        }
    }

    [Fact]
    public void CreateRejectsEmptyHostsAndPassword()
    {
        Assert.Throws<ArgumentException>(
            () => HotspotCertificateFactory.Create(Array.Empty<string>(), Password));
        Assert.Throws<ArgumentException>(
            () => HotspotCertificateFactory.Create(new[] { "attendta-demo.local" }, string.Empty));
        Assert.Throws<ArgumentException>(
            () => HotspotCertificateFactory.Create(new[] { "not a host" }, Password));
    }

    [Fact]
    public async Task CommandWritesFourFilesAndNeverPrintsThePassword()
    {
        var directory = NewTempDirectory();
        try
        {
            var result = await RunCommandAsync(
                new[]
                {
                    HotspotCertificateCommand.Argument,
                    "--host", "192.168.137.1",
                    "--host", "attendta-demo.local",
                    "--out", directory,
                },
                Password);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(4, Directory.GetFiles(directory).Length);
            Assert.Contains("Thumbprint", result.StdOut, StringComparison.Ordinal);
            Assert.Contains("IP:192.168.137.1", result.StdOut, StringComparison.Ordinal);
            Assert.Contains("DNS:attendta-demo.local", result.StdOut, StringComparison.Ordinal);
            Assert.DoesNotContain(Password, result.StdOut + result.StdErr, StringComparison.Ordinal);

            var serverPfx = Path.Combine(directory, HotspotCertificateCommand.ServerPfxFileName);
            if (!OperatingSystem.IsWindows())
            {
                var rootPfx = Path.Combine(directory, HotspotCertificateCommand.RootPfxFileName);
                var forbidden = UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                    UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
                Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(rootPfx) & forbidden);
                Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(serverPfx) & forbidden);
            }
            var collection = X509CertificateLoader.LoadPkcs12CollectionFromFile(
                serverPfx,
                Password,
                X509KeyStorageFlags.Exportable);
            var leaf = collection.Single(certificate => certificate.HasPrivateKey);
            Assert.True(leaf.HasPrivateKey);

            var rootCer = X509CertificateLoader.LoadCertificateFromFile(
                Path.Combine(directory, HotspotCertificateCommand.RootCertificateFileName));
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(rootCer);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            Assert.True(chain.Build(leaf));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task CommandRequiresThePasswordAndWritesNothing()
    {
        var directory = NewTempDirectory();
        try
        {
            var result = await RunCommandAsync(
                new[]
                {
                    HotspotCertificateCommand.Argument,
                    "--host", "192.168.137.1",
                    "--out", directory,
                },
                password: null);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains(
                HotspotCertificateCommand.PasswordEnvironmentVariable,
                result.StdErr,
                StringComparison.Ordinal);
            Assert.False(Directory.Exists(directory) && Directory.GetFiles(directory).Length > 0);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task CommandRefusesToOverwriteAnExistingRoot()
    {
        var directory = NewTempDirectory();
        try
        {
            var arguments = new[]
            {
                HotspotCertificateCommand.Argument,
                "--host", "192.168.137.1",
                "--out", directory,
            };

            var first = await RunCommandAsync(arguments, Password);
            Assert.Equal(0, first.ExitCode);

            var second = await RunCommandAsync(arguments, Password);
            Assert.Equal(1, second.ExitCode);
            Assert.Contains("already contains", second.StdErr, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void FailedOutputDoesNotDeleteAFileReplacedByAnotherProcess()
    {
        var directory = NewTempDirectory();
        var key = HotspotCertificateCommand.PasswordEnvironmentVariable;
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, HotspotCertificateCommand.RootPfxFileName);
            using var output = new ReplacingOutput(path);
            using var error = new StringWriter();

            var result = HotspotCertificateCommand.Run(
                new[] { "--host", "localhost", "--out", directory }, output, error);

            Assert.Equal(1, result);
            Assert.Equal("replacement", File.ReadAllText(path));
            Assert.Contains("Output files may remain", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ConcurrentCommandsDoNotOverwriteOrDeleteTheWinningCertificateSet()
    {
        var directory = NewTempDirectory();
        try
        {
            var arguments = new[] { HotspotCertificateCommand.Argument, "--host", "localhost", "--out", directory };
            var results = await Task.WhenAll(RunCommandAsync(arguments, Password), RunCommandAsync(arguments, Password));

            Assert.Single(results, result => result.ExitCode == 0);
            Assert.Single(results, result => result.ExitCode == 1);
            Assert.Equal(4, Directory.GetFiles(directory).Length);
            using var root = X509CertificateLoader.LoadPkcs12FromFile(
                Path.Combine(directory, HotspotCertificateCommand.RootPfxFileName), Password);
            Assert.True(root.HasPrivateKey);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void FileCreationRefusesAFileCreatedAfterThePreflightCheck()
    {
        var directory = NewTempDirectory();
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, HotspotCertificateCommand.RootPfxFileName);
            File.WriteAllText(path, "existing file");
            var writeNewFile = typeof(HotspotCertificateCommand).GetMethod(
                "WriteNewFile", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(writeNewFile);

            var exception = Assert.Throws<TargetInvocationException>(
                () => writeNewFile.Invoke(null, new object[] { path, new byte[] { 1, 2, 3 }, true }));

            Assert.IsAssignableFrom<IOException>(exception.InnerException);
            Assert.Equal("existing file", File.ReadAllText(path));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private sealed class ReplacingOutput(string path) : StringWriter
    {
        public override void WriteLine(string? value)
        {
            File.WriteAllText(path, "replacement");
            throw new IOException("Simulated output failure");
        }
    }

    [Fact]
    public void CryptographicFailureReportsThatOutputFilesMayRemain()
    {
        var directory = NewTempDirectory();
        var key = HotspotCertificateCommand.PasswordEnvironmentVariable;
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, Guid.NewGuid().ToString("N"));
            using var output = new CryptographicFailureOutput();
            using var error = new StringWriter();

            var result = HotspotCertificateCommand.Run(
                new[] { "--host", "localhost", "--out", directory }, output, error);

            Assert.Equal(1, result);
            Assert.Contains("Output files may remain", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(4, Directory.GetFiles(directory).Length);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
            DeleteDirectory(directory);
        }
    }

    private sealed class CryptographicFailureOutput : StringWriter
    {
        public override void WriteLine(string? value) => throw new CryptographicException("Simulated failure");
    }

    private static X509Extension FindExtension(X509Certificate2 certificate, string oid)
    {
        return certificate.Extensions
            .Cast<X509Extension>()
            .Single(extension => extension.Oid?.Value == oid);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCommandAsync(
        string[] arguments,
        string? password)
    {
        var webAppAssembly = Path.Combine(AppContext.BaseDirectory, "ASI.Basecode.WebApp.dll");
        Assert.True(File.Exists(webAppAssembly), $"The WebApp assembly was not copied to '{webAppAssembly}'.");

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(webAppAssembly);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        if (password is null)
        {
            start.Environment.Remove(HotspotCertificateCommand.PasswordEnvironmentVariable);
        }
        else
        {
            start.Environment[HotspotCertificateCommand.PasswordEnvironmentVariable] = password;
        }

        using var process = Process.Start(start);
        Assert.NotNull(process);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }

        return (process.ExitCode, await stdout, await stderr);
    }

    private static string NewTempDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "attendta-hotspot-cli-" + Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
