using System.Diagnostics;
using ASI.Basecode.Services.Security;
using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Exercises the WebApp <c>--hash-password</c> command that feeds the setup script.
/// </summary>
public class PasswordHasherCliTests
{
    private const string Password = "CliSecret#2026";

    [Fact]
    public async Task HashPasswordModePrintsOnlyThePhcHash()
    {
        var webAppAssembly = Path.Combine(AppContext.BaseDirectory, "ASI.Basecode.WebApp.dll");
        Assert.True(File.Exists(webAppAssembly), $"The WebApp assembly was not copied to '{webAppAssembly}'.");

        var startInfo = new ProcessStartInfo("dotnet", $"\"{webAppAssembly}\" --hash-password \"{Password}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var stdout = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, process.ExitCode);

        var hash = stdout.Trim();
        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, stdout, StringComparison.Ordinal);
        Assert.Contains("Argon2id hash generated", stderr, StringComparison.Ordinal);
        Assert.True(new Argon2PasswordHasher().Verify(Password, hash));
    }
}
