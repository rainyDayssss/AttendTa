using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Formatting;
using ASI.Basecode.WebApp.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Certificates;

/// <summary>Renders fixed, self-contained certificate PDFs for the Development tracer and future issuance.</summary>
public sealed class PlaywrightCertificatePdfRenderer : ICertificatePdfRenderer
{
    private const string TemplatePath = "~/Views/Certificates/CertificateTemplate.cshtml";
    private const int MaxRecipientNameLength = 255;
    private const int MaxEventTitleLength = 255;
    private const int MaxCertificateNumberLength = 100;

    private readonly IRazorViewEngine razorViewEngine;
    private readonly ITempDataProvider tempDataProvider;
    private readonly IModelMetadataProvider metadataProvider;
    private readonly IWebHostEnvironment environment;
    private readonly IServiceProvider services;
    private readonly IOptions<CertificateSignatoryOptions> signatoryOptions;
    private readonly string browserChannel;

    public PlaywrightCertificatePdfRenderer(
        IRazorViewEngine razorViewEngine,
        ITempDataProvider tempDataProvider,
        IModelMetadataProvider metadataProvider,
        IWebHostEnvironment environment,
        IServiceProvider services,
        IOptions<CertificateSignatoryOptions> signatoryOptions,
        IConfiguration configuration)
    {
        this.razorViewEngine = razorViewEngine;
        this.tempDataProvider = tempDataProvider;
        this.metadataProvider = metadataProvider;
        this.environment = environment;
        this.services = services;
        this.signatoryOptions = signatoryOptions;
        this.browserChannel = configuration["Certificates:BrowserChannel"];
    }

    public Task<byte[]> RenderSampleAsync(CancellationToken cancellationToken = default) => RenderPdfAsync(
        new CertificatePdfSnapshot(
            "AttendTa Development Sample",
            "Certificate Rendering Integration Check",
            new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc),
            "SAMPLE-DEVELOPMENT-ONLY"),
        isDevelopmentSample: true,
        cancellationToken);

    public Task<byte[]> RenderSnapshotAsync(CertificatePdfSnapshot snapshot, CancellationToken cancellationToken = default) =>
        RenderPdfAsync(snapshot, isDevelopmentSample: false, cancellationToken);

    private async Task<byte[]> RenderPdfAsync(
        CertificatePdfSnapshot snapshot,
        bool isDevelopmentSample,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateText(snapshot.RecipientName, nameof(snapshot.RecipientName), MaxRecipientNameLength);
        ValidateText(snapshot.EventTitle, nameof(snapshot.EventTitle), MaxEventTitleLength);
        ValidateText(snapshot.CertificateNumber, nameof(snapshot.CertificateNumber), MaxCertificateNumberLength);

        // The shared formatter treats unspecified SQL datetime2 values as UTC and rejects local-kind values.
        var eventStartsAtManila = ManilaTimeFormatter.Format(snapshot.EventStartsAt);
        var issuedAtManila = ManilaTimeFormatter.Format(snapshot.IssuedAt);
        var signatoryName = string.Empty;
        var signatoryTitle = string.Empty;
        var signatureDataUri = string.Empty;
        if (!isDevelopmentSample)
        {
            // IOptions validates the approved identity on first official render. The Development
            // tracer can still render its visibly watermarked sample if deployment config is absent.
            var signatory = signatoryOptions.Value;
            signatoryName = signatory.Name;
            signatoryTitle = signatory.Title;
            signatureDataUri = await ReadPngAssetDataUriAsync(signatory.PackagedSignatureAsset, cancellationToken);
        }

        var templateModel = new CertificateTemplateViewModel(
            snapshot.RecipientName,
            snapshot.EventTitle,
            eventStartsAtManila,
            issuedAtManila,
            snapshot.CertificateNumber,
            await ReadAssetDataUriAsync("img/attendta-logo.jpg", "image/jpeg", cancellationToken),
            await ReadAssetDataUriAsync("fonts/cinzel/cinzel-regular.ttf", "font/ttf", cancellationToken),
            await ReadAssetDataUriAsync("fonts/cinzel/cinzel-semibold.ttf", "font/ttf", cancellationToken),
            snapshot.RecipientName.Length > 60 ? "compact" : string.Empty,
            snapshot.EventTitle.Length > 100 ? "compact" : string.Empty,
            snapshot.CertificateNumber.Length > 50 ? "long" : string.Empty,
            isDevelopmentSample,
            signatoryName,
            signatoryTitle,
            signatureDataUri);

        var html = await RenderTemplateAsync(templateModel, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = browserChannel
        });
        await using var context = await browser.NewContextAsync();
        await context.RouteAsync("http://**/*", route => route.AbortAsync("blockedbyclient"));
        await context.RouteAsync("https://**/*", route => route.AbortAsync("blockedbyclient"));
        await using var page = await context.NewPageAsync();
        await page.SetContentAsync(html, new PageSetContentOptions
        {
            WaitUntil = WaitUntilState.Load
        });
        cancellationToken.ThrowIfCancellationRequested();

        var fontsReady = await page.EvaluateAsync<bool>("async () => { const regular = await document.fonts.load('16px Cinzel'); const semibold = await document.fonts.load('600 16px Cinzel'); await document.fonts.ready; return regular.length > 0 && semibold.length > 0 && document.fonts.check('16px Cinzel') && document.fonts.check('600 16px Cinzel'); }");
        if (!fontsReady)
        {
            throw new InvalidOperationException("The locally bundled Cinzel font did not load for certificate rendering.");
        }

        if (!isDevelopmentSample)
        {
            var signatureLoaded = await page.EvaluateAsync<bool>(
                "() => { const image = document.querySelector('.signatory-signature'); return !!image && image.complete && image.naturalWidth > 0 && image.naturalHeight > 0; }");
            if (!signatureLoaded)
            {
                throw new InvalidOperationException("The packaged certificate signatory signature image is invalid.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            Landscape = true,
            PrintBackground = true,
            PreferCSSPageSize = true
        });
    }

    private async Task<string> RenderTemplateAsync(CertificateTemplateViewModel model, CancellationToken cancellationToken)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var viewResult = razorViewEngine.GetView(executingFilePath: null, viewPath: TemplatePath, isMainPage: true);
        if (!viewResult.Success)
        {
            throw new InvalidOperationException("The fixed certificate Razor template could not be located.");
        }

        var viewData = new ViewDataDictionary<CertificateTemplateViewModel>(metadataProvider, actionContext.ModelState)
        {
            Model = model
        };
        var tempData = new TempDataDictionary(httpContext, tempDataProvider);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var viewContext = new ViewContext(actionContext, viewResult.View, viewData, tempData, writer, new HtmlHelperOptions());
        cancellationToken.ThrowIfCancellationRequested();
        await viewResult.View.RenderAsync(viewContext);
        cancellationToken.ThrowIfCancellationRequested();
        return writer.ToString();
    }

    private async Task<string> ReadAssetDataUriAsync(string relativePath, string contentType, CancellationToken cancellationToken)
    {
        var file = environment.WebRootFileProvider.GetFileInfo(relativePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("A required certificate rendering asset is missing.");
        }

        await using var source = file.CreateReadStream();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        return $"data:{contentType};base64,{Convert.ToBase64String(buffer.ToArray())}";
    }

    private async Task<string> ReadPngAssetDataUriAsync(string relativePath, CancellationToken cancellationToken)
    {
        var file = environment.WebRootFileProvider.GetFileInfo(relativePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The packaged certificate signatory signature image is missing.");
        }

        await using var source = file.CreateReadStream();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        ReadOnlySpan<byte> pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 24 || !bytes.AsSpan(0, pngSignature.Length).SequenceEqual(pngSignature)
            || BitConverter.ToUInt32(bytes, 16) == 0 || BitConverter.ToUInt32(bytes, 20) == 0)
        {
            throw new InvalidDataException("The packaged certificate signatory signature image is not a valid PNG.");
        }

        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    private static void ValidateText(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Certificate snapshot text cannot be empty.", parameterName);
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentException($"Certificate snapshot text cannot exceed {maximumLength} characters.", parameterName);
        }
    }
}
