using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ASI.Basecode.WebApp.Demo;

internal static class DemoPrivateDirectory
{
    // Create with the DACL in the same operation: never expose a newly created store
    // under inherited parent permissions, or accept a directory created by another process.
    internal static void CreateNew(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException();
        var security = new DirectorySecurity();
        security.SetOwner(user);
        security.SetAccessRuleProtection(true, false);
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
            inheritance, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        var descriptor = security.GetSecurityDescriptorBinaryForm();
        var pinned = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
        try
        {
            var attributes = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = pinned.AddrOfPinnedObject()
            };
            if (!CreateDirectory(path, ref attributes))
                throw new IOException("Could not create the private demo store.", new Win32Exception(Marshal.GetLastWin32Error()));
        }
        finally { pinned.Free(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        internal int Length;
        internal IntPtr SecurityDescriptor;
        internal int InheritHandle;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path, ref SecurityAttributes attributes);
}
