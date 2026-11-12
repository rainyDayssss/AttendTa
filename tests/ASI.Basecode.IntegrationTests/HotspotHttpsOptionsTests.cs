using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ASI.Basecode.WebApp.Hotspot;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

// PLAIN ENGLISH: These tests prove the app refuses a bad hotspot setup before it opens a port: a
// missing or wrong certificate, an expired certificate, a certificate for the wrong address, a
// wildcard/loopback bind, or equal ports. One test also starts a real Kestrel server and proves the
// loopback HTTP address and the hotspot HTTPS address both answer.
public class HotspotHttpsOptionsTests
{
    private const string Password = "HotspotTest#2026";
    private static readonly string CertificatePath =
        Path.Combine(Path.GetTempPath(), "attendta-hotspot-options-server.pfx");

    [Fact]
    public void DisabledSectionNeedsNoCertificate()
    {
        var options = Options(values => values["Hotspot:Enabled"] = "false");

        options.Validate();

        Assert.False(options.Enabled);
        Assert.Equal("http://127.0.0.1:5080", options.HttpEndpoint);
    }

    [Fact]
    public void ValidSectionExposesLoopbackHttpAndLanHttps()
    {
        var options = Options();

        options.Validate();

        Assert.True(options.Enabled);
        Assert.Equal("http://127.0.0.1:5080", options.HttpEndpoint);
        Assert.Equal("https://192.168.137.1:5443", options.HttpsEndpoint);
    }

    [Fact]
    public void OmittedPortsUseTheDocumentedDefaults()
    {
        var options = Options(values =>
        {
            values.Remove("Hotspot:HttpsPort");
            values.Remove("Hotspot:HttpPort");
        });

        options.Validate();

        Assert.Equal(5443, options.HttpsPort);
        Assert.Equal(5080, options.HttpPort);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("not-an-ip")]
    public void UnsafeLanAddressIsRefused(string lanAddress)
    {
        var options = Options(values => values["Hotspot:LanAddress"] = lanAddress);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Hotspot:LanAddress", exception.Message);
    }

    [Fact]
    public void MissingCertificatePathIsRefused()
    {
        var options = Options(values => values["Hotspot:CertificatePath"] = string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Hotspot:CertificatePath", exception.Message);
    }

    [Fact]
    public void UnqualifiedCertificatePathIsRefused()
    {
        var options = Options(values => values["Hotspot:CertificatePath"] = "server.pfx");

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Hotspot:CertificatePath", exception.Message);
        Assert.Contains("absolute", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingCertificatePasswordIsRefused()
    {
        var options = Options(values => values["Hotspot:CertificatePassword"] = string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Hotspot:CertificatePassword", exception.Message);
    }

    [Fact]
    public void MissingPublicHostNameIsRefused()
    {
        var options = Options(values => values["Hotspot:PublicHostName"] = string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Hotspot:PublicHostName", exception.Message);
    }

    [Theory]
    [InlineData("https://192.168.137.1")]
    [InlineData("192.168.137.1:5443")]
    [InlineData("*.local")]
    [InlineData("not a host")]
    public void InvalidPublicHostNameIsRefused(string publicHostName)
    {
        var options = Options(values => values["Hotspot:PublicHostName"] = publicHostName);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Hotspot:PublicHostName", exception.Message);
    }

    [Fact]
    public void EqualPortsAreRefused()
    {
        var options = Options(values => values["Hotspot:HttpPort"] = "5443");

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Hotspot:HttpsPort", exception.Message);
        Assert.Contains("different", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    public void OutOfRangeHttpsPortIsRefused(string port)
    {
        var options = Options(values => values["Hotspot:HttpsPort"] = port);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("Hotspot:HttpsPort", exception.Message);
    }

    [Fact]
    public void ValidCertificateLoadsWithPrivateKey()
    {
        var bundle = HotspotCertificateFactory.Create(new[] { "192.168.137.1" }, Password);
        var path = WritePfx(bundle.ServerCertificate, Password);
        try
        {
            var options = Options(values => values["Hotspot:CertificatePath"] = path);

            using var loaded = HotspotHttps.LoadServerCertificate(options);

            Assert.True(loaded.HasPrivateKey);
            Assert.Equal(bundle.ServerCertificate.Thumbprint, loaded.Thumbprint);
        }
        finally
        {
            File.Delete(path);
            bundle.RootCertificate.Dispose();
            bundle.RootPublicCertificate.Dispose();
            bundle.ServerCertificate.Dispose();
            bundle.ServerPublicCertificate.Dispose();
        }
    }

    [Fact]
    public void MissingCertificateFileIsRefused()
    {
        var path = Path.Combine(Path.GetTempPath(), "attendta-missing-" + Guid.NewGuid().ToString("N") + ".pfx");
        var options = Options(values => values["Hotspot:CertificatePath"] = path);

        var exception = Assert.Throws<InvalidOperationException>(
            () => HotspotHttps.LoadServerCertificate(options));

        Assert.Contains("Hotspot:CertificatePath", exception.Message);
    }

    [Fact]
    public void WrongPasswordIsRefusedWithoutPrintingThePassword()
    {
        var bundle = HotspotCertificateFactory.Create(new[] { "192.168.137.1" }, Password);
        var path = WritePfx(bundle.ServerCertificate, Password);
        try
        {
            var options = Options(values =>
            {
                values["Hotspot:CertificatePath"] = path;
                values["Hotspot:CertificatePassword"] = "wrong-password";
            });

            var exception = Assert.Throws<InvalidOperationException>(
                () => HotspotHttps.LoadServerCertificate(options));

            Assert.Contains("Hotspot:CertificatePassword", exception.Message);
            Assert.DoesNotContain("wrong-password", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Password, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
            bundle.RootCertificate.Dispose();
            bundle.RootPublicCertificate.Dispose();
            bundle.ServerCertificate.Dispose();
            bundle.ServerPublicCertificate.Dispose();
        }
    }

    [Fact]
    public void ExpiredCertificateIsRefused()
    {
        var now = DateTimeOffset.Now;
        using var certificate = CreateSelfSignedServerCertificate(
            "192.168.137.1",
            now.AddDays(-400),
            now.AddDays(-10));
        var path = WritePfx(certificate, Password);
        try
        {
            var options = Options(values => values["Hotspot:CertificatePath"] = path);

            var exception = Assert.Throws<InvalidOperationException>(
                () => HotspotHttps.LoadServerCertificate(options));

            Assert.Contains("not currently valid", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SanMismatchNamesTheExpectedHostAndActualSans()
    {
        var bundle = HotspotCertificateFactory.Create(new[] { "attendta-demo.local" }, Password);
        var path = WritePfx(bundle.ServerCertificate, Password);
        try
        {
            var options = Options(values => values["Hotspot:CertificatePath"] = path);

            var exception = Assert.Throws<InvalidOperationException>(
                () => HotspotHttps.LoadServerCertificate(options));

            Assert.Contains("192.168.137.1", exception.Message, StringComparison.Ordinal);
            Assert.Contains("attendta-demo.local", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
            bundle.RootCertificate.Dispose();
            bundle.RootPublicCertificate.Dispose();
            bundle.ServerCertificate.Dispose();
            bundle.ServerPublicCertificate.Dispose();
        }
    }

    [Fact]
    public void CertificateWithoutPrivateKeyIsRefused()
    {
        var now = DateTimeOffset.Now;
        using var certificate = CreateSelfSignedServerCertificate(
            "192.168.137.1",
            now.AddDays(-1),
            now.AddDays(30),
            withPrivateKey: false);
        var path = WritePfx(certificate, Password);
        try
        {
            var options = Options(values => values["Hotspot:CertificatePath"] = path);

            var exception = Assert.Throws<InvalidOperationException>(
                () => HotspotHttps.LoadServerCertificate(options));

            Assert.Contains("private key", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CertificateWithoutServerAuthenticationEkuIsRefused()
    {
        var now = DateTimeOffset.Now;
        using var certificate = CreateSelfSignedServerCertificate(
            "192.168.137.1",
            now.AddDays(-1),
            now.AddDays(30),
            serverAuthenticationEku: false);
        var path = WritePfx(certificate, Password);
        try
        {
            var options = Options(values => values["Hotspot:CertificatePath"] = path);

            var exception = Assert.Throws<InvalidOperationException>(
                () => HotspotHttps.LoadServerCertificate(options));

            Assert.Contains("server authentication", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task HotspotListenersServeLoopbackHttpAndLanHttps()
    {
        var bundle = HotspotCertificateFactory.Create(new[] { "127.0.0.1" }, Password);
        var httpPort = GetFreePort();
        var httpsPort = GetFreePort();
        var options = new HotspotHttpsOptions
        {
            Enabled = true,
            LanAddress = "127.0.0.1",
            HttpsPort = httpsPort,
            HttpPort = httpPort,
            PublicHostName = "127.0.0.1",
            CertificatePath = "unused",
            CertificatePassword = Password,
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(
            kestrel => HotspotHttps.ConfigureListeners(kestrel, options, bundle.ServerCertificate));
        await using var app = builder.Build();
        app.MapGet("/Account/Login", () => Results.Ok("login"));
        await app.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            using var http = new HttpClient();
            var httpResponse = await http.GetAsync(
                $"http://127.0.0.1:{httpPort}/Account/Login",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            };
            using var https = new HttpClient(handler);
            var httpsResponse = await https.GetAsync(
                $"https://127.0.0.1:{httpsPort}/Account/Login",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, httpsResponse.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
            bundle.RootCertificate.Dispose();
            bundle.RootPublicCertificate.Dispose();
            bundle.ServerCertificate.Dispose();
            bundle.ServerPublicCertificate.Dispose();
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static HotspotHttpsOptions Options(Action<Dictionary<string, string>>? mutate = null)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Hotspot:Enabled"] = "true",
            ["Hotspot:LanAddress"] = "192.168.137.1",
            ["Hotspot:HttpsPort"] = "5443",
            ["Hotspot:HttpPort"] = "5080",
            ["Hotspot:PublicHostName"] = "192.168.137.1",
            ["Hotspot:CertificatePath"] = CertificatePath,
            ["Hotspot:CertificatePassword"] = Password,
        };
        mutate?.Invoke(values);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
        return HotspotHttpsOptions.FromConfiguration(configuration);
    }

    private static string WritePfx(X509Certificate2 certificate, string password)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "attendta-hotspot-" + Guid.NewGuid().ToString("N") + ".pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
        return path;
    }

    private static X509Certificate2 CreateSelfSignedServerCertificate(
        string host,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool serverAuthenticationEku = true,
        bool withPrivateKey = true)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={host}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        if (serverAuthenticationEku)
        {
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid(HotspotHttps.ServerAuthenticationOid) },
                    false));
        }

        var san = new SubjectAlternativeNameBuilder();
        if (IPAddress.TryParse(host, out var address))
        {
            san.AddIpAddress(address);
        }
        else
        {
            san.AddDnsName(host);
        }

        request.CertificateExtensions.Add(san.Build());

        var certificate = request.CreateSelfSigned(notBefore, notAfter);
        if (withPrivateKey)
        {
            return certificate;
        }

        using (certificate)
        {
            return X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));
        }
    }
}
