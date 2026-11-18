using System;
using System.IO;
using ASI.Basecode.WebApp.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ASI.Basecode.WebApp.Configuration;

/// <summary>
/// Resolves and validates <c>Common:SetupRoot</c>. Data Protection keys, logs and private files all
/// live beneath this directory, so it must exist outside the web root and the deployment tree.
/// </summary>
public static class SetupRootPath
{
    /// <summary>The message used when <c>Common:SetupRoot</c> is not configured.</summary>
    public const string MissingMessage =
        "Common:SetupRoot must be configured so Data Protection keys can be stored under its keys folder.";

    /// <summary>The message used when the setup root overlaps the web root or the content root.</summary>
    public const string OverlapMessage =
        "Common:SetupRoot must be outside wwwroot and the application deployment directory.";

    /// <summary>
    /// Resolves <c>Common:SetupRoot</c> to a trimmed full path and rejects a root that is the same
    /// as, inside, or an ancestor of the web root or the content root.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">Hosting environment that defines the web and content roots.</param>
    /// <returns>The trimmed full path of the private setup root.</returns>
    public static string Resolve(IConfiguration configuration, IWebHostEnvironment environment)
        => Resolve(configuration, environment, MissingMessage);

    /// <summary>
    /// Resolves <c>Common:SetupRoot</c>, using <paramref name="missingMessage"/> when it is empty.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">Hosting environment that defines the web and content roots.</param>
    /// <param name="missingMessage">Message used when <c>Common:SetupRoot</c> is empty.</param>
    /// <returns>The trimmed full path of the private setup root.</returns>
    public static string Resolve(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        string missingMessage)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var configuredRoot = configuration.GetSetupRootDirectoryPath();
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException(missingMessage);
        }

        var setupRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot));
        var contentRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(environment.ContentRootPath));
        var webRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(contentRoot, "wwwroot")
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(environment.WebRootPath));

        if (IsSameOrDescendant(setupRoot, webRoot)
            || IsSameOrDescendant(webRoot, setupRoot)
            || IsSameOrDescendant(setupRoot, contentRoot)
            || IsSameOrDescendant(contentRoot, setupRoot))
        {
            throw new InvalidOperationException(OverlapMessage);
        }

        return setupRoot;
    }

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(candidate, root, comparison))
        {
            return true;
        }

        // "C:\" and "/" are their own separator-suffixed prefix; other roots need one appended.
        var prefix = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, comparison);
    }
}
