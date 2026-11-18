using ASI.Basecode.WebApp.Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public sealed class PlaywrightBrowserProviderTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static PlaywrightBrowserProvider CreateProvider() => new(new ConfigurationBuilder().Build());

    [Fact]
    public async Task TwoConcurrentJobsShareBrowserAndIsolateContextsWhileThirdCanBeCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var firstHasCookie = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<IBrowser> Job(IBrowser browser, bool setCookie)
        {
            await using var context = await browser.NewContextAsync();
            if (setCookie)
            {
                await context.AddCookiesAsync([new Cookie { Name = "private", Value = "first", Url = "https://example.invalid/" }]);
                firstHasCookie.TrySetResult();
            }
            else
            {
                await firstHasCookie.Task.WaitAsync(Timeout, ct);
                Assert.Empty(await context.CookiesAsync());
            }

            if (Interlocked.Increment(ref started) == 2) bothStarted.TrySetResult();
            await release.Task.WaitAsync(Timeout, ct);
            return browser;
        }

        var first = provider.WithBrowserAsync(browser => Job(browser, true), ct);
        var second = provider.WithBrowserAsync(browser => Job(browser, false), ct);
        try
        {
            await bothStarted.Task.WaitAsync(Timeout, ct);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var thirdStarted = false;
            var third = provider.WithBrowserAsync(browser =>
            {
                thirdStarted = true;
                return Task.FromResult(browser);
            }, cancellation.Token);
            Assert.False(third.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => third);
            Assert.False(thirdStarted);
        }
        finally
        {
            release.TrySetResult();
        }

        var browsers = await Task.WhenAll(first, second).WaitAsync(Timeout, ct);
        Assert.Same(browsers[0], browsers[1]);
    }

    [Fact]
    public async Task ShutdownWaitsForActiveJobAndRejectsNewWork()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = CreateProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = provider.WithBrowserAsync(async browser =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(Timeout, ct);
            Assert.True(browser.IsConnected);
            return true;
        }, ct);
        try
        {
            await started.Task.WaitAsync(Timeout, ct);
            var shutdown = provider.DisposeAsync().AsTask();
            Assert.False(shutdown.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.WithBrowserAsync(browser => Task.FromResult(true), ct));
            release.TrySetResult();
            Assert.True(await work.WaitAsync(Timeout, ct));
            await shutdown.WaitAsync(Timeout, ct);
        }
        finally
        {
            release.TrySetResult();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisconnectedBrowserIsReplacedOnNextJob()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        var original = await provider.WithBrowserAsync(browser => Task.FromResult(browser), ct);
        await original.CloseAsync();
        Assert.False(original.IsConnected);

        var replacement = await provider.WithBrowserAsync(browser => Task.FromResult(browser), ct);
        Assert.NotSame(original, replacement);
        Assert.True(replacement.IsConnected);
    }
}
