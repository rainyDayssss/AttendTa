using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ASI.Basecode.WebApp.Readiness;
using Microsoft.Playwright;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

public class DemoReadinessCoordinatorTests
{
    private const string OfflineNotice = "Offline status not verified; a disconnected-host run requires separate operator evidence.";
    private static readonly string[] CheckNames =
    [
        "runtime SQL identity and audit permissions", "private storage",
        "Playwright Chromium PDF rendering", "localhost login HTTP 200"
    ];

    [Fact]
    public async Task AllChecksPassReturnsZeroButNeverAssertsOfflineEvidence()
    {
        var calls = new List<int>();
        Task Pass(int index) { calls.Add(index); return Task.CompletedTask; }
        var checks = new DemoReadiness.ReadinessChecks(() => Pass(0), () => Pass(1), () => Pass(2), () => Pass(3));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DemoReadiness.RunAsync(checks, output, error);

        Assert.Equal(0, exit);
        Assert.Equal(new[] { 0, 1, 2, 3 }, calls);
        foreach (var name in CheckNames) Assert.Contains($"PASS {name}", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains(OfflineNotice, output.ToString());
        Assert.DoesNotContain("PASS offline", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task AnyFailureReturnsNonzeroRunsRemainingChecksAndRedactsException(int failedIndex)
    {
        const string secret = "sensitive-connection-detail";
        var calls = new List<int>();
        Task Check(int index)
        {
            calls.Add(index);
            return index == failedIndex ? Task.FromException(new InvalidOperationException(secret)) : Task.CompletedTask;
        }
        var checks = new DemoReadiness.ReadinessChecks(() => Check(0), () => Check(1), () => Check(2), () => Check(3));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DemoReadiness.RunAsync(checks, output, error);

        Assert.Equal(1, exit);
        Assert.Equal(new[] { 0, 1, 2, 3 }, calls);
        Assert.Contains($"FAIL {CheckNames[failedIndex]}", error.ToString());
        Assert.DoesNotContain(secret, output.ToString() + error, StringComparison.Ordinal);
        Assert.Contains(OfflineNotice, output.ToString());
        for (var i = 0; i < CheckNames.Length; i++)
            if (i != failedIndex) Assert.Contains($"PASS {CheckNames[i]}", output.ToString());
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(302, false)]
    [InlineData(404, false)]
    [InlineData(500, false)]
    public async Task LoginRequiresExactHttp200WithoutFollowingRedirects(int status, bool expectedSuccess)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var server = RespondAsync(listener, status, timeout.Token);
        var url = $"http://127.0.0.1:{port}/Account/Login";
        if (expectedSuccess) await DemoReadiness.CheckLoginAsync(url);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => DemoReadiness.CheckLoginAsync(url));
        Assert.StartsWith("GET /Account/Login HTTP/1.1", await server, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingChromiumProducesBrowserFailureAndNonzeroProcessExit()
    {
        // Ensure the test's Playwright driver itself is available; only its browser cache is empty.
        using (var playwright = await Playwright.CreateAsync()) Assert.NotNull(playwright.Chromium);
        var browserRoot = Path.Combine(Path.GetTempPath(), "attendta-empty-browser-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(browserRoot);
        try
        {
            var assembly = Path.Combine(AppContext.BaseDirectory, "ASI.Basecode.WebApp.dll");
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(assembly);
            start.ArgumentList.Add("--demo-readiness");
            start.Environment["PLAYWRIGHT_BROWSERS_PATH"] = browserRoot;
            start.Environment.Remove("ConnectionStrings__DefaultConnection");
            start.Environment["ATTENDTA_RUNTIME_SQL_LOGIN"] = "sensitive-connection-detail";
            start.Environment["Common__SetupRoot"] = Path.Combine(browserRoot, "missing-storage");
            start.Environment["ATTENDTA_READINESS_LOGIN_URL"] = "not-a-url";
            using var process = Process.Start(start)!;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var output = await stdout;
            var errors = await stderr;
            Assert.Equal(1, process.ExitCode);
            Assert.Contains("FAIL Playwright Chromium PDF rendering", errors);
            Assert.Contains(OfflineNotice, output);
            Assert.DoesNotContain("sensitive-connection-detail", output + errors, StringComparison.Ordinal);
        }
        finally { Directory.Delete(browserRoot, true); }
    }

    [Fact]
    public async Task StagedChromiumLaunchesAndRendersNonEmptyPdf()
    {
        await DemoReadiness.CheckBrowserAsync();
    }

    [Fact]
    public async Task ChecklistLeavesUnverifiedRowsBlockedWithoutPassOrIssueProof()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "demo-host-readiness-evidence.md");
        var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        Assert.Contains("not proof", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("may say `PASS` only when", text, StringComparison.Ordinal);

        var start = text.IndexOf("## Readiness checks", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var section = text[start..];
        var end = section.IndexOf("\n## ", StringComparison.Ordinal);
        if (end >= 0) section = section[..end];

        var rows = section.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("|", StringComparison.Ordinal))
            .Select(line => line.Split('|'))
            .Where(cells => cells.Length >= 7)
            .Where(cells => !cells[1].Trim().Equals("#", StringComparison.Ordinal))
            .Where(cells => !cells[1].Trim().All(character => character is '-' or ':'))
            .ToList();

        Assert.NotEmpty(rows);
        foreach (var cells in rows)
        {
            Assert.Equal("BLOCKED", cells[4].Trim());
            Assert.False(string.IsNullOrWhiteSpace(cells[5]));
        }
    }

    private static async Task<string> RespondAsync(TcpListener listener, int status, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellationToken))) { }
        var response = $"HTTP/1.1 {status} Test\r\nContent-Length: 0\r\nLocation: /Account/Login\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
        return requestLine!;
    }
}
