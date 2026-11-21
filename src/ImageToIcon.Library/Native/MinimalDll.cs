using System.Buffers.Binary;
using System.Runtime.Versioning;

namespace ImageToIcon.Library.Native;

/// Creates a valid, resource-only PE32+ DLL with an empty .rsrc section. The Win32 resource
/// updater needs an existing PE file as target, so this is the seed for a brand-new library.
[SupportedOSPlatform("windows")]
internal static class MinimalDll
{
    public static void Create(string path)
    {
        var b = new byte[0x400];
        b[0] = (byte)'M';
        b[1] = (byte)'Z';
        W32(b, 0x3C, 0x80);                    // e_lfanew
        b[0x80] = (byte)'P';
        b[0x81] = (byte)'E';
        W16(b, 0x84, 0x8664);                  // machine: AMD64
        W16(b, 0x86, 1);                       // sections
        W16(b, 0x94, 0xF0);                    // size of optional header
        W16(b, 0x96, 0x2022);                  // DLL | executable | large address aware
        W16(b, 0x98, 0x20B);                   // PE32+
        W32(b, 0x9C, 0x200);                   // size of code
        W32(b, 0xAC, 0x1000);                  // base of code
        W64(b, 0xB0, 0x10000000UL);            // image base
        W32(b, 0xB8, 0x1000);                  // section alignment
        W32(b, 0xBC, 0x200);                   // file alignment
        W16(b, 0xC0, 6);                       // OS version
        W16(b, 0xC8, 6);                       // subsystem version
        W32(b, 0xD0, 0x2000);                  // size of image
        W32(b, 0xD4, 0x200);                   // size of headers
        W16(b, 0xDC, 2);                       // subsystem: GUI
        W16(b, 0xDE, 0x8160);                  // DYNAMIC_BASE | NX_COMPAT | TERMINAL_SERVER_AWARE | HIGH_ENTROPY_VA
        W64(b, 0xE0, 0x100000);
        W64(b, 0xE8, 0x1000);
        W64(b, 0xF0, 0x100000);
        W64(b, 0xF8, 0x1000);
        W32(b, 0x104, 16);                     // data directories
        W32(b, 0x118, 0x1000);                 // resource directory RVA
        W32(b, 0x11C, 0x200);                  // resource directory size

        const int section = 0x188;
        ".rsrc"u8.CopyTo(b.AsSpan(section, 8));
        W32(b, section + 8, 0x200);            // virtual size
        W32(b, section + 12, 0x1000);          // virtual address
        W32(b, section + 16, 0x200);           // raw size
        W32(b, section + 20, 0x200);           // raw pointer
        W32(b, section + 36, 0x40000040);      // initialized data | readable

        File.WriteAllBytes(path, b);
    }

    private static void W16(byte[] b, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o), v);
    private static void W32(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), v);
    private static void W64(byte[] b, int o, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(o), v);
}
