using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ImageToIcon.Library.Native;

/// Thin P/Invoke layer over the kernel32 resource-update API. Integer resource IDs are passed
/// as MAKEINTRESOURCE values (IntPtr), which is what the Win32 API expects.
[SupportedOSPlatform("windows")]
internal static partial class Kernel32
{
    public const uint LoadLibraryAsDataFile = 0x00000002;

    public static readonly IntPtr RtIcon = 3;
    public static readonly IntPtr RtGroupIcon = 14;

    [LibraryImport("kernel32.dll", EntryPoint = "BeginUpdateResourceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr BeginUpdateResource(string filePath, [MarshalAs(UnmanagedType.Bool)] bool deleteExistingResources);

    /// data == null with size 0 deletes the resource.
    [LibraryImport("kernel32.dll", EntryPoint = "UpdateResourceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UpdateResource(IntPtr handle, IntPtr type, IntPtr name, ushort language, IntPtr data, uint dataSize);

    [LibraryImport("kernel32.dll", EntryPoint = "EndUpdateResourceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EndUpdateResource(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] bool discardChanges);

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr LoadLibraryEx(string filePath, IntPtr reserved, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreeLibrary(IntPtr module);

    [LibraryImport("kernel32.dll", EntryPoint = "FindResourceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
}
