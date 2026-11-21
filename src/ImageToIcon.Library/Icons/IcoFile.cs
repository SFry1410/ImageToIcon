using System.Buffers.Binary;

namespace ImageToIcon.Library.Icons;

/// One image inside an .ico file: header fields as stored in ICONDIRENTRY plus the raw payload.
public sealed record IcoFrame(byte Width, byte Height, byte ColorCount, byte Reserved, ushort Planes, ushort BitCount, byte[] Data)
{
    /// Real pixel size; a stored 0 means 256.
    public int PixelWidth => Width == 0 ? 256 : Width;
    public int PixelHeight => Height == 0 ? 256 : Height;
}

/// Parses ICO containers and serialises the matching RT_GROUP_ICON directory. Only the
/// structure is validated; pixel data is passed through untouched.
public static class IcoFile
{
    private const int IconDirSize = 6;
    private const int IconDirEntrySize = 16;
    private const int GroupEntrySize = 14;

    /// Upper bound for the whole .ico input; far above what a 256 px multi-size set needs.
    public const long MaxFileBytes = 64L * 1024 * 1024;

    public static IReadOnlyList<IcoFrame> Parse(byte[] ico)
    {
        if (ico.Length < IconDirSize)
            throw new InvalidDataException("ICO data is too short.");
        if (ico.Length > MaxFileBytes)
            throw new InvalidDataException("ICO data exceeds the size limit.");

        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(0, 2));
        var type = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2, 2));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4, 2));
        if (reserved != 0 || type != 1 || count == 0)
            throw new InvalidDataException("Not a valid icon file.");
        if (IconDirSize + count * IconDirEntrySize > ico.Length)
            throw new InvalidDataException("ICO directory is truncated.");

        var frames = new List<IcoFrame>(count);
        for (var i = 0; i < count; i++)
        {
            var e = ico.AsSpan(IconDirSize + i * IconDirEntrySize, IconDirEntrySize);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(8, 4));
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(12, 4));
            if (size == 0 || offset < IconDirSize || (long)offset + size > ico.Length)
                throw new InvalidDataException("ICO frame points outside the file.");

            var data = new byte[size];
            Array.Copy(ico, offset, data, 0, size);
            frames.Add(new IcoFrame(
                e[0], e[1], e[2], e[3],
                BinaryPrimitives.ReadUInt16LittleEndian(e.Slice(4, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(e.Slice(6, 2)),
                data));
        }

        return frames;
    }

    /// RT_GROUP_ICON payload: GRPICONDIR followed by entries that reference RT_ICON resources
    /// by ID instead of file offset. frameIds must match frames one to one.
    public static byte[] BuildGroup(IReadOnlyList<IcoFrame> frames, IReadOnlyList<ushort> frameIds)
    {
        if (frames.Count != frameIds.Count || frames.Count == 0)
            throw new ArgumentException("Frames and IDs must be non-empty and of equal length.");

        var buffer = new byte[IconDirSize + frames.Count * GroupEntrySize];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(0, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2, 2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4, 2), (ushort)frames.Count);

        for (var i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            var e = buffer.AsSpan(IconDirSize + i * GroupEntrySize, GroupEntrySize);
            e[0] = f.Width;
            e[1] = f.Height;
            e[2] = f.ColorCount;
            e[3] = f.Reserved;
            BinaryPrimitives.WriteUInt16LittleEndian(e.Slice(4, 2), f.Planes);
            BinaryPrimitives.WriteUInt16LittleEndian(e.Slice(6, 2), f.BitCount);
            BinaryPrimitives.WriteUInt32LittleEndian(e.Slice(8, 4), (uint)f.Data.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(e.Slice(12, 2), frameIds[i]);
        }

        return buffer;
    }
}
