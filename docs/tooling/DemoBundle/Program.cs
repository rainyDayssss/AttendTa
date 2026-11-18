using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

const string resourceName = "AttendTa.Demo.Package.zip";
if (args.Length > 1 || (args.Length == 1 && args[0] is not ("--check-bundle" or "--extract-only")))
{
    Console.Error.WriteLine("Unsupported argument.");
    return 2;
}

try
{
    using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
        ?? throw new InvalidDataException("Embedded package is missing.");
    using var archive = new ZipArchive(source, ZipArchiveMode.Read);
    if (archive.Entries.Count is 0 or > 20000)
        throw new InvalidDataException("Embedded package has an invalid entry count.");
    long total = 0;
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in archive.Entries)
    {
        var name = entry.FullName.Replace('\\', '/');
        var segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var unixType = (entry.ExternalAttributes >> 16) & 0xf000;
        if (name.StartsWith('/') || name.Contains(':') || segments.Length == 0
            || segments.Any(segment => segment is "." or "..")
            || unixType == 0xa000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0
            || !names.Add(name.TrimEnd('/')))
            throw new InvalidDataException("Embedded package contains an unsafe entry.");
        total = checked(total + entry.Length);
        if (total > 2L * 1024 * 1024 * 1024)
            throw new InvalidDataException("Embedded package is too large.");
    }
    foreach (var required in new[] { "AttendTa-Demo.exe", "ASI.Basecode.WebApp.exe",
                 "demo/attendta-mvp-schema.sql", "demo/venue-1.png", "demo/event-1.png", "demo/ASSET-LICENSE.md" })
        if (!names.Contains(required))
            throw new InvalidDataException("Embedded package is incomplete.");
    if (args is ["--check-bundle"])
    {
        Console.WriteLine("Embedded package structure is valid; no files were extracted.");
        return 0;
    }

    var temp = new DirectoryInfo(Path.GetFullPath(Path.GetTempPath()));
    for (var parent = temp; parent is not null; parent = parent.Parent)
        if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Temporary directory contains a reparse point.");
    var destination = Path.Combine(temp.FullName, "AttendTaDemo-bundle-" + Guid.NewGuid().ToString("N"));
    if (Directory.Exists(destination) || File.Exists(destination))
        throw new IOException("Extraction directory already exists.");
    Directory.CreateDirectory(destination);
    // ExtractToDirectory refuses traversal and existing files. Never delete a partial or
    // completed extraction automatically; it may still be in use by a running app.
    archive.ExtractToDirectory(destination);
    Console.WriteLine($"Application files extracted to {destination}");
    if (args is ["--extract-only"]) return 0;

    var launcher = Path.Combine(destination, "AttendTa-Demo.exe");
    using var child = Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = destination, UseShellExecute = false })
        ?? throw new InvalidOperationException("Unable to start the launcher.");
    await child.WaitForExitAsync();
    Console.WriteLine("The extracted application files remain in the location above; remove them only after the app has stopped.");
    return child.ExitCode;
}
catch (Exception)
{
    Console.Error.WriteLine("The demo bundle could not be safely opened or started. No database reset was performed.");
    if (!Console.IsInputRedirected) { Console.WriteLine("Press Enter to close."); Console.ReadLine(); }
    return 1;
}
