using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Demo;

internal static class DemoCleanReinstall
{
    internal static void EnsureKnownLegacyHintsClear()
    {
        EnsureKnownLegacyPathClear(Path.GetTempPath());
        var installerAncestors = GetInstallerAncestors();
        foreach (var name in new[] { "AttendTa-Demo", "AttendTa-Demo-Standalone", "ASI.Basecode.WebApp" })
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                foreach (var process in processes)
                    if (process.Id != Environment.ProcessId &&
                        !(installerAncestors.TryGetValue(process.Id, out var identity) &&
                          process.StartTime.ToUniversalTime() == identity.StartTime &&
                          string.Equals(CanonicalImage(process), identity.Image, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("A possible demo application process is running.");
            }
            finally { foreach (var process in processes) process.Dispose(); }
        }
    }

    private static Dictionary<int, (DateTime StartTime, string Image)> GetInstallerAncestors()
    {
        var allowed = new Dictionary<int, (DateTime StartTime, string Image)>();
        using var current = Process.GetCurrentProcess();
        var parentId = ParentProcessId(current);
        if (parentId <= 0) return allowed;
        using var parent = Process.GetProcessById(parentId);
        if (!string.Equals(parent.ProcessName, "AttendTa-Demo", StringComparison.OrdinalIgnoreCase))
            return allowed;
        // Parent PIDs may be recycled after the original parent exits.
        if (parent.StartTime.ToUniversalTime() > current.StartTime.ToUniversalTime())
            throw new InvalidOperationException("Cannot verify installer launcher ancestry.");
        var parentImage = CanonicalImage(parent);
        var expected = CanonicalPath(Path.Combine(AppContext.BaseDirectory, "AttendTa-Demo.exe"));
        if (!string.Equals(parentImage, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot verify installer launcher.");
        var parentStart = parent.StartTime.ToUniversalTime();
        allowed.Add(parentId, (parentStart, parentImage));

        var grandparentId = ParentProcessId(parent);
        if (grandparentId <= 0) return allowed;
        using var grandparent = Process.GetProcessById(grandparentId);
        if (!string.Equals(grandparent.ProcessName, "AttendTa-Demo-Standalone", StringComparison.OrdinalIgnoreCase))
            return allowed;
        var grandparentStart = grandparent.StartTime.ToUniversalTime();
        if (grandparentStart > parentStart)
            throw new InvalidOperationException("Cannot verify standalone launcher ancestry.");
        var image = CanonicalImage(grandparent);
        if (!string.Equals(Path.GetFileName(image), "AttendTa-Demo-Standalone.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot verify standalone launcher image.");
        allowed.Add(grandparentId, (grandparentStart, image));
        return allowed;
    }

    private static string CanonicalImage(Process process) =>
        CanonicalPath(process.MainModule?.FileName ?? throw new InvalidOperationException("Cannot inspect process image."));

    private static string CanonicalPath(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("Cannot verify executable path.");
        path = Path.GetFullPath(path);
        DemoRemovalPreview.RejectReparseAncestors(path);
        var buffer = new StringBuilder(32768);
        var length = GetLongPathName(path, buffer, buffer.Capacity);
        if (length == 0 || length >= buffer.Capacity)
            throw new InvalidOperationException("Cannot resolve executable path.");
        return buffer.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string path, StringBuilder buffer, int capacity);

    private static int ParentProcessId(Process process)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var status = NtQueryInformationProcess(process.Handle, 0, out var info,
            Marshal.SizeOf<ProcessBasicInformation>(), out _);
        if (status != 0) throw new InvalidOperationException("Cannot verify installer process ancestry.");
        return checked((int)info.InheritedFromUniqueProcessId);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2a;
        internal IntPtr Reserved2b;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int informationClass,
        out ProcessBasicInformation information, int informationLength, out int returnLength);

    internal static void EnsureKnownLegacyPathClear(string temporaryRoot)
    {
        var path = Path.Combine(temporaryRoot, "AttendTa", "app");
        try
        {
            _ = File.GetAttributes(path);
            throw new InvalidOperationException("Known legacy application path exists.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }

    internal static async Task RunAsync(bool confirmed, string root, string deployment, string instance,
        string credentialFile, string backupRoot, string journalRoot, IReadOnlyList<string> preview,
        CancellationToken ct, Action checkLegacy = null, Func<Task> afterUninstallForTest = null,
        Func<Task> afterVersionMarkerForTest = null)
    {
        if (!confirmed) throw new OperationCanceledException("Reinstall declined.");
        var stage = "input validation";
        try
        {
            checkLegacy ??= EnsureKnownLegacyHintsClear;
            checkLegacy();
            await DemoRemovalPreview.ValidateInputsAsync(root, deployment, instance, credentialFile, ct);
            await DemoSetup.LoadAssetsAsync(deployment, ct);
            using var credentialStream = DemoBackupSourceFile.Open(credentialFile);
            using var reader = new StreamReader(credentialStream, Encoding.UTF8, true, 4096, leaveOpen: true);
            var input = new char[16385];
            IReadOnlyDictionary<string, string> credentials;
            try
            {
                var count = 0;
                int read;
                while (count < input.Length && (read = await reader.ReadAsync(input.AsMemory(count), ct)) > 0)
                    count += read;
                if (count == input.Length) throw new InvalidDataException("Credential file is too large.");
                credentials = DemoCredentials.Parse(new string(input, 0, count));
            }
            finally { Array.Clear(input); }
            stage = "guarded uninstall";
            await DemoGuardedUninstall.RunAsync(true, root, deployment, instance, credentialFile, backupRoot,
                journalRoot, preview, ct, beforeRemoval: () => { checkLegacy(); return Task.CompletedTask; },
                preserveCredentials: credentials);
            stage = "after uninstall";
            if (afterUninstallForTest is not null) await afterUninstallForTest();
            checkLegacy();
            stage = "fresh setup";
            await DemoSetup.RunAsync(credentials, root, deployment, ct, instance, afterVersionMarkerForTest);
        }
        catch (Exception)
        {
            throw new DemoReinstallException(stage);
        }
    }
}

internal sealed class DemoReinstallException(string stage) : Exception
{
    internal string Stage { get; } = stage;
}
