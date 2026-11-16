using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace ASI.Basecode.WebApp.Certificates;

/// <summary>
/// Shares one Playwright driver and one headless Chromium browser across certificate renders.
/// The provider is a singleton: the browser starts on first use, is replaced after it disconnects,
/// and at most two renders use it at the same time. Disposal closes the browser and the driver
/// through either the asynchronous or the synchronous host shutdown path.
/// </summary>
public sealed class PlaywrightBrowserProvider : IAsyncDisposable, IDisposable
{
    private const int MaxConcurrentRenders = 2;

    private readonly SemaphoreSlim renderSlots = new(MaxConcurrentRenders, MaxConcurrentRenders);
    private readonly SemaphoreSlim browserGate = new(1, 1);
    private readonly string browserChannel;

    private IPlaywright playwright;
    private IBrowser browser;
    private int disposed;

    public PlaywrightBrowserProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        browserChannel = configuration["Certificates:BrowserChannel"];
    }

    /// <summary>
    /// Runs <paramref name="work"/> with the shared browser after ensuring it is connected. A caller
    /// waiting for a render slot can be cancelled; failures from <paramref name="work"/> propagate.
    /// </summary>
    public async Task<T> WithBrowserAsync<T>(
        Func<IBrowser, Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

        await renderSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            var connected = await GetBrowserAsync(cancellationToken).ConfigureAwait(false);
            return await work(connected).ConfigureAwait(false);
        }
        finally
        {
            renderSlots.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        // Wait for every in-flight render to release its slot before closing the browser under it.
        for (var slot = 0; slot < MaxConcurrentRenders; slot++)
        {
            await renderSlots.WaitAsync().ConfigureAwait(false);
        }

        try
        {
            await browserGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await CloseBrowserAsync().ConfigureAwait(false);
                playwright?.Dispose();
                playwright = null;
            }
            finally
            {
                browserGate.Release();
            }
        }
        finally
        {
            for (var slot = 0; slot < MaxConcurrentRenders; slot++)
            {
                renderSlots.Release();
            }
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (browser is { IsConnected: true })
        {
            return browser;
        }

        await browserGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (browser is { IsConnected: true })
            {
                return browser;
            }

            await CloseBrowserAsync().ConfigureAwait(false);
            playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Channel = browserChannel
            }).ConfigureAwait(false);
            return browser;
        }
        finally
        {
            browserGate.Release();
        }
    }

    private async Task CloseBrowserAsync()
    {
        if (browser is null)
        {
            return;
        }

        var closing = browser;
        browser = null;
        try
        {
            await closing.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A browser that already disconnected or crashed has nothing left to close, and a
            // disposal failure must not block launching its replacement.
        }
    }
}
