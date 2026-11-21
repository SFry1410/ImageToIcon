using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ImageToIcon.Library.Icons;

namespace ImageToIcon.Library.Native;

/// Applies a batch of resource deletions and writes to a PE file. The batch is only committed
/// when the whole callback succeeds; any exception discards it and leaves the file as it was.
[SupportedOSPlatform("windows")]
internal static class ResourceWriter
{
    public static void Apply(string peFile, Action<Session> work)
    {
        var handle = Kernel32.BeginUpdateResource(peFile, false);
        if (handle == IntPtr.Zero)
            throw new LibraryUpdateException($"Cannot open '{peFile}' for resource update (Win32 error {Marshal.GetLastPInvokeError()}).");

        var committed = false;
        try
        {
            work(new Session(handle));
            if (!Kernel32.EndUpdateResource(handle, false))
                throw new LibraryUpdateException($"Writing resources to '{peFile}' failed (Win32 error {Marshal.GetLastPInvokeError()}).");
            committed = true;
        }
        finally
        {
            if (!committed)
                Kernel32.EndUpdateResource(handle, true);
        }
    }

    internal sealed class Session(IntPtr handle)
    {
        public void Delete(IntPtr type, ushort id)
        {
            Kernel32.UpdateResource(handle, type, (IntPtr)id, 0, IntPtr.Zero, 0);
            // Deleting a missing resource is not an error for the caller; ignore the result.
        }

        public void Write(IntPtr type, ushort id, byte[] data)
        {
            var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                if (!Kernel32.UpdateResource(handle, type, (IntPtr)id, 0, pinned.AddrOfPinnedObject(), (uint)data.Length))
                    throw new LibraryUpdateException($"Writing resource {id} failed (Win32 error {Marshal.GetLastPInvokeError()}).");
            }
            finally
            {
                pinned.Free();
            }
        }

        public static IntPtr IconType => Kernel32.RtIcon;
        public static IntPtr GroupType => Kernel32.RtGroupIcon;
    }
}
