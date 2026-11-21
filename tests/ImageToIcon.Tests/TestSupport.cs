using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Xunit;

namespace ImageToIcon.Tests;

/// Fact that is skipped (not failed) on non-Windows hosts.
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires Windows (kernel32 resource API).";
    }
}

/// Unique scratch directory under the temp folder, removed on dispose.
public sealed class ScratchDir : IDisposable
{
    public ScratchDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "i2i-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Left behind only if a test still holds a handle; harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}

/// Builds ICO containers with placeholder frame payloads. The exporter does not decode frames,
/// so arbitrary bytes are enough to exercise the structure and resource handling.
public static class IcoBuilder
{
    public static byte[] Build(params (byte width, byte height, byte[] data)[] frames)
    {
        var headerSize = 6 + 16 * frames.Length;
        var total = headerSize + frames.Sum(f => f.data.Length);
        var ico = new byte[total];
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(2, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(4, 2), (ushort)frames.Length);

        var offset = headerSize;
        for (var i = 0; i < frames.Length; i++)
        {
            var (w, h, data) = frames[i];
            var e = ico.AsSpan(6 + i * 16, 16);
            e[0] = w;
            e[1] = h;
            BinaryPrimitives.WriteUInt16LittleEndian(e.Slice(4, 2), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(e.Slice(6, 2), 32);
            BinaryPrimitives.WriteUInt32LittleEndian(e.Slice(8, 4), (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(e.Slice(12, 4), (uint)offset);
            data.CopyTo(ico, offset);
            offset += data.Length;
        }

        return ico;
    }

    public static byte[] Standard() => Build(
        (0, 0, [1, 2, 3, 4]),   // 256 px
        (48, 48, [5, 6, 7]),
        (16, 16, [8, 9]));
}

/// Test-only view of resources in a library DLL, independent of the production code path.
public static class ResourceProbe
{
    private static readonly IntPtr RtIcon = 3;
    private static readonly IntPtr RtGroupIcon = 14;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryExW(string file, IntPtr reserved, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr FindResourceW(IntPtr module, IntPtr name, IntPtr type);

    private const uint LoadAsDataFile = 0x2;

    public static bool HasGroup(string dll, int id) => Find(dll, id, RtGroupIcon);

    public static bool HasFrame(string dll, int id) => Find(dll, id, RtIcon);

    private static bool Find(string dll, int id, IntPtr type)
    {
        var module = LoadLibraryExW(dll, IntPtr.Zero, LoadAsDataFile);
        if (module == IntPtr.Zero)
            throw new InvalidOperationException($"Cannot load '{dll}' as data file (Win32 {Marshal.GetLastWin32Error()}).");
        try
        {
            return FindResourceW(module, (IntPtr)id, type) != IntPtr.Zero;
        }
        finally
        {
            FreeLibrary(module);
        }
    }
}
