using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ASI.Basecode.WebApp.Demo;

/// <summary>
/// Opens one regular backup source without following its final path component.
/// This does not validate ancestors, authorize a destination, or create a backup.
/// </summary>
internal static class DemoBackupSourceFile
{
    private const uint GenericRead = 0x80000000;
    private const uint DeleteAccess = 0x00010000;
    private const uint ReadAttributes = 0x80;
    private const uint ShareRead = 1;
    private const uint OpenExisting = 3;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint BackupSemantics = 0x02000000;
    private const uint AttributeDirectory = 0x10;
    private const uint AttributeDevice = 0x40;
    private const uint AttributeReparsePoint = 0x400;
    private const uint FileTypeDisk = 1;

    internal static FileStream Open(string path) => OpenCore(path, false);

    internal static FileStream OpenForDeletion(string path) => OpenCore(path, true);

    private static FileStream OpenCore(string path, bool delete)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("An absolute Windows backup source is required.");

        // Sharing permits readers only: while this handle is open, cooperative Windows
        // opens cannot modify, rename or delete this file.
        var handle = CreateFileW(path, GenericRead | (delete ? DeleteAccess : 0), ShareRead, IntPtr.Zero, OpenExisting,
            OpenReparsePoint | BackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException("Cannot open backup source.", new Win32Exception(error));
        }
        try
        {
            ValidateHandle(handle);
            return new FileStream(handle, FileAccess.Read);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    // SQL Server has the MDF/LDF open, so inspect metadata with sharing rather than
    // attempting to read them. SQL itself backs them up under the offline assumption.
    internal static void CheckMetadata(string path)
    {
        if (!OperatingSystem.IsWindows() || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("An absolute Windows backup source is required.");
        using var handle = CreateFileW(path, ReadAttributes, 7, IntPtr.Zero, OpenExisting,
            OpenReparsePoint | BackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new IOException("Cannot inspect backup source.", new Win32Exception(Marshal.GetLastWin32Error()));
        ValidateHandle(handle);
    }

    private static void ValidateHandle(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info))
            throw new IOException("Cannot inspect backup source.", new Win32Exception(Marshal.GetLastWin32Error()));
        if (GetFileType(handle) != FileTypeDisk ||
            (info.FileAttributes & (AttributeDirectory | AttributeDevice | AttributeReparsePoint)) != 0 ||
            info.NumberOfLinks != 1)
            throw new InvalidOperationException("Backup source is not a single-link regular file.");
        RejectNamedStreams(handle);
    }

    // FileStreamInfo is queried on the already validated handle, never by a second
    // path lookup. Failure or malformed/oversized output is a refusal, not "no ADS".
    private static void RejectNamedStreams(SafeFileHandle handle, bool directory = false)
    {
        const int header = 24;
        for (var size = 4096; size <= 1024 * 1024; size *= 2)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetFileInformationByHandleEx(handle, 7, buffer, (uint)size))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 234 || error == 122) continue;
                    // Directories without data streams may report end-of-file.
                    if (directory && error == 38) return;
                    throw new IOException("Cannot inspect file streams.", new Win32Exception(error));
                }
                var offset = 0;
                while (true)
                {
                    if (offset > size - header) throw new InvalidDataException("Invalid file stream information.");
                    var next = unchecked((uint)Marshal.ReadInt32(buffer, offset));
                    var length = unchecked((uint)Marshal.ReadInt32(buffer, offset + 4));
                    var available = next == 0 ? (uint)(size - offset) : next;
                    if (available < header || available > size - offset || length > available - header || (length & 1) != 0)
                        throw new InvalidDataException("Invalid file stream information.");
                    var name = Marshal.PtrToStringUni(IntPtr.Add(buffer, offset + header), checked((int)length / 2));
                    if (!string.Equals(name, "::$DATA", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("File contains an alternate data stream.");
                    if (next == 0) return;
                    offset = checked(offset + (int)next);
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new InvalidDataException("File stream information is too large to inspect.");
    }

    internal static SafeFileHandle OpenDirectory(string path, bool delete = false)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("An absolute Windows directory is required.");
        DemoRemovalPreview.RejectReparseAncestors(path);
        var handle = CreateFileW(path, ReadAttributes | (delete ? DeleteAccess : 0), delete ? ShareRead : 7,
            IntPtr.Zero, OpenExisting, OpenReparsePoint | BackupSemantics, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException("Cannot open directory for stream inspection.", new Win32Exception(error));
        }
        try
        {
            if (!GetFileInformationByHandle(handle, out var info))
                throw new IOException("Cannot inspect directory.", new Win32Exception(Marshal.GetLastWin32Error()));
            if (GetFileType(handle) != FileTypeDisk || (info.FileAttributes & AttributeDirectory) == 0 ||
                (info.FileAttributes & (AttributeDevice | AttributeReparsePoint)) != 0)
                throw new InvalidOperationException("Source is not a non-reparse directory.");
            RejectNamedStreams(handle, directory: true);
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static void DeleteOpened(FileStream file)
    {
        DeleteOpened(file.SafeFileHandle);
    }

    internal static void DeleteOpened(SafeFileHandle handle)
    {
        // FileDispositionInfo marks this exact file for deletion when its handle closes.
        var disposition = 1;
        if (!SetFileInformationByHandle(handle, 4, ref disposition, 1))
            throw new IOException("Cannot delete validated file.", new Win32Exception(Marshal.GetLastWin32Error()));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        internal uint FileAttributes;
        internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(SafeFileHandle file);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int infoClass, IntPtr info, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle file, int infoClass, ref int info, uint size);
}
