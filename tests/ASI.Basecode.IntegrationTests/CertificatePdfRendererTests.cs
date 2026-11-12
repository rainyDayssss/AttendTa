using ASI.Basecode.Data.Models.Lookups;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class CertificatePdfRendererTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    [Fact]
    public async Task RendererCreatesOfficialA4LandscapePageWithApprovedSignatoryAndNoDevelopmentMarkings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(fixture.ConnectionString, "Development");
        using var renderScope = factory.Services.CreateScope();
        var renderer = renderScope.ServiceProvider.GetRequiredService<ICertificatePdfRenderer>();
        var snapshot = new CertificatePdfSnapshot(
            "Jamie Cruz",
            "Campus Leadership Summit",
            new DateTime(2026, 1, 1, 18, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 3, 15, 0, DateTimeKind.Unspecified),
            "AT-2026-000042");

        var pdf = await renderer.RenderSnapshotAsync(snapshot, cancellationToken);
        var text = await ExtractPdfTextAsync(pdf, cancellationToken);
        var normalizedText = NormalizePdfText(text);
        var info = await ReadPdfInfoAsync(pdf, cancellationToken);
        var fonts = await ReadPdfFontsAsync(pdf, cancellationToken);
        AssertOneLandscapeA4Page(info);
        Assert.Contains("jamiecruz", normalizedText, StringComparison.Ordinal);
        Assert.Contains("campusleadershipsummit", normalizedText, StringComparison.Ordinal);
        Assert.Contains("at-2026-000042", normalizedText, StringComparison.Ordinal);
        Assert.Contains("eventstart", normalizedText, StringComparison.Ordinal);
        Assert.Contains("dateissued", normalizedText, StringComparison.Ordinal);
        Assert.Contains("2026-01-0202:30asia/manila", normalizedText, StringComparison.Ordinal);
        Assert.Contains("2026-01-0211:15asia/manila", normalizedText, StringComparison.Ordinal);
        Assert.Contains("universityofcebu", normalizedText, StringComparison.Ordinal);
        Assert.Contains("christiankyleducay", normalizedText, StringComparison.Ordinal);
        Assert.Contains("organizationpresident", normalizedText, StringComparison.Ordinal);
        Assert.DoesNotContain("developmentsample", normalizedText, StringComparison.Ordinal);
        Assert.DoesNotContain("notanofficialcertificate", normalizedText, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"(?im)^.*Cinzel.*\byes\s+yes\s+yes\b"), fonts);
        Assert.True(Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Subtype\s*/Image", RegexOptions.CultureInvariant).Count >= 2,
            "The official PDF should embed both the AttendTa logo and the approved signature image.");
    }

    [Fact]
    public async Task OfficialRendererRejectsUnapprovedSignatoryConfiguration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(
            fixture.ConnectionString,
            "Development",
            configureSignatoryOptions: options => options.Name = "Unapproved Name");
        using var renderScope = factory.Services.CreateScope();
        var renderer = renderScope.ServiceProvider.GetRequiredService<ICertificatePdfRenderer>();
        var snapshot = new CertificatePdfSnapshot(
            "Jamie Cruz",
            "Campus Leadership Summit",
            new DateTime(2026, 1, 1, 18, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 3, 15, 0, DateTimeKind.Utc),
            "AT-2026-000042");

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => renderer.RenderSnapshotAsync(snapshot, cancellationToken));
        Assert.Contains("approved certificate signatory", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OfficialRendererFailsWhenPackagedSignatureAssetIsMissing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(fixture.ConnectionString, "Development");
        factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider = new NullFileProvider();
        using var renderScope = factory.Services.CreateScope();
        var renderer = renderScope.ServiceProvider.GetRequiredService<ICertificatePdfRenderer>();
        var snapshot = new CertificatePdfSnapshot(
            "Jamie Cruz",
            "Campus Leadership Summit",
            new DateTime(2026, 1, 1, 18, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 3, 15, 0, DateTimeKind.Utc),
            "AT-2026-000042");

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => renderer.RenderSnapshotAsync(snapshot, cancellationToken));
        Assert.Contains("signatory signature image is missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RendererDisplaysMarkupAsTextAndBlocksNetworkRequests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(fixture.ConnectionString, "Development");
        await using var networkProbe = await CertificateNetworkProbe.StartAsync();
        using var renderScope = factory.Services.CreateScope();
        var renderer = renderScope.ServiceProvider.GetRequiredService<ICertificatePdfRenderer>();
        var payload = $"<img src=\"{networkProbe.Url}/probe\" alt=\"untrusted\"> & recipient";
        var snapshot = new CertificatePdfSnapshot(
            payload,
            "Event <script>fetch('" + networkProbe.Url + "/probe')</script>",
            new DateTime(2026, 1, 1, 18, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 3, 15, 0, DateTimeKind.Utc),
            "AT-HTML-0001");

        var pdf = await renderer.RenderSnapshotAsync(snapshot, cancellationToken);
        var text = NormalizePdfText(await ExtractPdfTextAsync(pdf, cancellationToken));

        Assert.Contains("<img", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(NormalizePdfText(networkProbe.Url), text, StringComparison.Ordinal);
        Assert.Contains("<script>", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, networkProbe.RequestCount);
        AssertOneLandscapeA4Page(await ReadPdfInfoAsync(pdf, cancellationToken));
    }

    [Fact]
    public async Task RendererKeepsMaximumLengthSnapshotValuesOnOnePage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(fixture.ConnectionString, "Development");
        using var renderScope = factory.Services.CreateScope();
        var renderer = renderScope.ServiceProvider.GetRequiredService<ICertificatePdfRenderer>();
        var recipient = new string('W', 255);
        var eventTitle = new string('M', 255);
        var certificateNumber = new string('A', 100);
        Assert.Equal(255, recipient.Length);
        Assert.Equal(255, eventTitle.Length);

        var pdf = await renderer.RenderSnapshotAsync(new CertificatePdfSnapshot(
            recipient,
            eventTitle,
            new DateTime(2026, 1, 1, 18, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 3, 15, 0, DateTimeKind.Utc),
            certificateNumber), cancellationToken);
        var text = NormalizePdfText(await ExtractPdfTextRawAsync(pdf, cancellationToken));
        Assert.Contains(NormalizePdfText(recipient), text, StringComparison.Ordinal);
        Assert.Contains(NormalizePdfText(eventTitle), text, StringComparison.Ordinal);
        Assert.Contains(certificateNumber.ToLowerInvariant(), text, StringComparison.Ordinal);
        AssertOneLandscapeA4Page(await ReadPdfInfoAsync(pdf, cancellationToken));
    }

    [Fact]
    public async Task RendererRejectsLocalKindSnapshotTimestamps()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = CreateFactory(fixture.ConnectionString, "Development");
        using var renderScope = factory.Services.CreateScope();
        var renderer = renderScope.ServiceProvider.GetRequiredService<ICertificatePdfRenderer>();
        var snapshot = new CertificatePdfSnapshot(
            "Jamie Cruz",
            "Campus Leadership Summit",
            new DateTime(2026, 1, 1, 18, 30, 0, DateTimeKind.Local),
            new DateTime(2026, 1, 2, 3, 15, 0, DateTimeKind.Utc),
            "AT-2026-000042");

        await Assert.ThrowsAsync<ArgumentException>(() => renderer.RenderSnapshotAsync(snapshot, cancellationToken));
    }

    [Fact]
    public async Task RetiredTracerRouteHasNoEndpointForAnonymousMemberAndManagementCallers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var suffix = Guid.NewGuid().ToString("N");
        var password = MemberEventTestSupport.NewPassword();
        var officerEmail = $"retired-tracer-officer-{suffix}@attendta.test";
        var memberEmail = $"retired-tracer-member-{suffix}@attendta.test";
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, officerEmail, password, RoleCode.Officer, cancellationToken);
        await MemberEventTestSupport.SeedUserAsync(fixture.ConnectionString, memberEmail, password, RoleCode.Member, cancellationToken);

        using var factory = CreateFactory(fixture.ConnectionString, "Development");
        using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var memberClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var managementClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await MemberEventTestSupport.SignInAsync(memberClient, memberEmail, password, cancellationToken);
        await MemberEventTestSupport.SignInAsync(managementClient, officerEmail, password, cancellationToken);

        // No endpoint matches the retired route. The app-wide fallback policy runs before a missing
        // endpoint becomes 404, so an anonymous caller is challenged and redirected to sign-in.
        var anonymousPage = await anonymousClient.GetAsync("/CertificateTracer", cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, anonymousPage.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymousPage), StringComparison.Ordinal);

        var anonymousPost = await anonymousClient.PostAsync(
            "/CertificateTracer/Trace",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["registrationId"] = "1" }),
            cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, anonymousPost.StatusCode);
        Assert.StartsWith("/Account/Login", MemberEventTestSupport.LocationPath(anonymousPost), StringComparison.Ordinal);

        // An authenticated caller satisfies the fallback policy, so routing reports the missing endpoint.
        foreach (var client in new[] { memberClient, managementClient })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/CertificateTracer", cancellationToken)).StatusCode);
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await client.PostAsync(
                    "/CertificateTracer/Trace",
                    new FormUrlEncodedContent(new Dictionary<string, string> { ["registrationId"] = "1" }),
                    cancellationToken)).StatusCode);
        }
    }

    private static AttendTaWebApplicationFactory CreateFactory(
        string connectionString,
        string environmentName,
        Action<CertificateSignatoryOptions>? configureSignatoryOptions = null)
    {
        var factory = new AttendTaWebApplicationFactory(connectionString, environmentName: environmentName)
        {
            ConfigureServicesForTesting = services =>
            {
                if (configureSignatoryOptions is not null)
                {
                    services.PostConfigure(configureSignatoryOptions);
                }
            }
        };
        return factory;
    }

    private static Task<string> ExtractPdfTextAsync(byte[] pdf, CancellationToken cancellationToken) =>
        RunPdfToolAsync("pdftotext", "ATTENDTA_PDFTOTEXT", pdf, ["-layout", "{pdf}", "-"], cancellationToken);

    private static Task<string> ExtractPdfTextRawAsync(byte[] pdf, CancellationToken cancellationToken) =>
        RunPdfToolAsync("pdftotext", "ATTENDTA_PDFTOTEXT", pdf, ["-raw", "{pdf}", "-"], cancellationToken);

    private static Task<string> ReadPdfInfoAsync(byte[] pdf, CancellationToken cancellationToken) =>
        RunPdfToolAsync("pdfinfo", "ATTENDTA_PDFINFO", pdf, ["{pdf}"], cancellationToken);

    private static Task<string> ReadPdfFontsAsync(byte[] pdf, CancellationToken cancellationToken) =>
        RunPdfToolAsync("pdffonts", "ATTENDTA_PDFFONTS", pdf, ["{pdf}"], cancellationToken);

    private static string NormalizePdfText(string text) =>
        Regex.Replace(text, @"\s+", string.Empty, RegexOptions.CultureInvariant).ToLowerInvariant();

    private static async Task<string> RunPdfToolAsync(
        string executableName,
        string executableEnvironmentVariable,
        byte[] pdf,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var configuredPath = Environment.GetEnvironmentVariable(executableEnvironmentVariable);
        var executable = string.IsNullOrWhiteSpace(configuredPath) ? executableName : configuredPath;
        if (!string.IsNullOrWhiteSpace(configuredPath) && !File.Exists(configuredPath))
        {
            throw new InvalidOperationException($"{executableEnvironmentVariable} must point to an existing {executableName} executable.");
        }

        var pdfPath = Path.Combine(Path.GetTempPath(), $"AttendTa-Certificate-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdfPath, pdf, cancellationToken);
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument == "{pdf}" ? pdfPath : argument);
            }

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                throw new InvalidOperationException(
                    $"Certificate PDF inspection requires Poppler's {executableName}; add it to PATH or set {executableEnvironmentVariable}.",
                    exception);
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{executableName} could not inspect the generated certificate PDF: {standardError}");
            }

            return standardOutput;
        }
        finally
        {
            File.Delete(pdfPath);
        }
    }

    private static void AssertOneLandscapeA4Page(string pdfInfo)
    {
        var pages = Regex.Match(pdfInfo, @"(?m)^Pages:\s*(?<count>\d+)", RegexOptions.CultureInvariant);
        Assert.True(pages.Success, "pdfinfo did not report a page count.");
        Assert.Equal("1", pages.Groups["count"].Value);

        var size = Regex.Match(
            pdfInfo,
            @"(?m)^Page size:\s*(?<width>[\d.]+)\s+x\s+(?<height>[\d.]+)\s+pts",
            RegexOptions.CultureInvariant);
        Assert.True(size.Success, "pdfinfo did not report the generated PDF page size.");
        var width = double.Parse(size.Groups["width"].Value, CultureInfo.InvariantCulture);
        var height = double.Parse(size.Groups["height"].Value, CultureInfo.InvariantCulture);
        Assert.True(width > height, $"Expected landscape PDF dimensions but got {width} × {height} points.");
        Assert.InRange(Math.Abs(width - 841.89), 0, 1);
        Assert.InRange(Math.Abs(height - 595.28), 0, 1);
    }

    private sealed class CertificateNetworkProbe : IAsyncDisposable
    {
        private readonly WebApplication application;
        private int requestCount;

        private CertificateNetworkProbe(WebApplication application)
        {
            this.application = application;
        }

        public string Url { get; private set; } = string.Empty;
        public int RequestCount => Volatile.Read(ref requestCount);

        public static async Task<CertificateNetworkProbe> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var application = builder.Build();
            var probe = new CertificateNetworkProbe(application);
            application.MapGet("/probe", (HttpContext context) =>
            {
                Interlocked.Increment(ref probe.requestCount);
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

            await application.StartAsync();
            var addresses = application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            probe.Url = addresses.Addresses.Single(address => address.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
            return probe;
        }

        public async ValueTask DisposeAsync() => await application.DisposeAsync();
    }
}
