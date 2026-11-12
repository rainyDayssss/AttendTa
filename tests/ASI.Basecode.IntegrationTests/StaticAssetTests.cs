using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Guards the static assets the application shell and the certificate renderer depend on, and
/// keeps the retired Bootstrap/React/slimScroll bundle and legacy theme files out of the web root.
/// </summary>
public sealed class StaticAssetTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly string[] ShellAssets =
    [
        "/css/app.css",
        "/js/site.js",
        "/lib/jquery/dist/jquery.min.js",
        "/lib/jquery-validation/dist/jquery.validate.min.js",
        "/lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js",
        "/img/attendta-logo.jpg",
        "/fonts/outfit-latin.woff2",
        "/favicon.ico"
    ];

    private static readonly string[] CertificateAssets =
    [
        "img/attendta-logo.jpg",
        "img/christian-kyle-ducay-signature.png",
        "fonts/cinzel/cinzel-regular.ttf",
        "fonts/cinzel/cinzel-semibold.ttf",
        "fonts/cinzel/OFL.txt"
    ];

    private static readonly string[] RemovedLegacyPaths =
    [
        "lib/bootstrap/dist/css/bootstrap.min.css",
        "lib/bootstrap/dist/js/bootstrap.bundle.min.js",
        "lib/react/dist/react.production.min.js",
        "lib/react-dom/dist/react-dom.production.min.js",
        "html/assets/js/plugins/jquery-slimscroll/jquery.slimscroll.min.js",
        "css/login.css",
        "css/main.css",
        "css/site.css",
        "css/style.css",
        "fonts/roboto.css",
        "img/asi-logo-invert.svg",
        "img/LiveIT.png",
        "img/profile-logo.png"
    ];

    [Fact]
    public async Task ShellAssetsAreServedFromTheActiveWebRoot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        foreach (var asset in ShellAssets)
        {
            var response = await client.GetAsync(asset, cancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{asset} returned {(int)response.StatusCode}.");
            var contentType = response.Content.Headers.ContentType?.MediaType;
            Assert.False(
                string.Equals(contentType, "text/html", StringComparison.OrdinalIgnoreCase),
                $"{asset} returned an HTML page ({contentType}) instead of the static asset.");
            Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        }
    }

    [Fact]
    public void CertificateAssetsResolveFromTheWebRoot()
    {
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        var fileProvider = factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider;

        foreach (var asset in CertificateAssets)
        {
            Assert.True(fileProvider.GetFileInfo(asset).Exists, $"{asset} is missing from the web root.");
        }
    }

    [Fact]
    public async Task RemovedLegacyAssetsAreNoLongerServed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new AttendTaWebApplicationFactory(fixture.ConnectionString);
        var fileProvider = factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider;
        using var client = factory.CreateClient();

        foreach (var path in RemovedLegacyPaths)
        {
            Assert.False(fileProvider.GetFileInfo(path).Exists, $"{path} is still present in the web root.");

            // Unmatched URLs fall through to the fallback authorization policy, which challenges
            // anonymous callers with the HTML sign-in page. Anything else means the retired file
            // is still being served.
            var response = await client.GetAsync("/" + path, cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var servedAsStaticFile = response.StatusCode == HttpStatusCode.OK
                && !string.Equals(contentType, "text/html", StringComparison.OrdinalIgnoreCase);
            Assert.False(servedAsStaticFile, $"/{path} is still served as a static file ({contentType}).");
        }
    }
}
