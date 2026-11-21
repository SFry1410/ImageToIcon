using System.Buffers.Binary;
using ImageToIcon.Library.Icons;
using Xunit;

namespace ImageToIcon.Tests;

public class IcoFileTests
{
    [Fact]
    public void Parse_ReadsFramesAndMapsZeroToTwoFiftySix()
    {
        var frames = IcoFile.Parse(IcoBuilder.Standard());

        Assert.Equal(3, frames.Count);
        Assert.Equal(256, frames[0].PixelWidth);
        Assert.Equal(48, frames[1].PixelWidth);
        Assert.Equal(16, frames[2].PixelHeight);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, frames[0].Data);
    }

    [Fact]
    public void Parse_RejectsBadHeader()
    {
        var ico = IcoBuilder.Standard();
        ico[2] = 2; // type must be 1 (icon)

        Assert.Throws<InvalidDataException>(() => IcoFile.Parse(ico));
    }

    [Fact]
    public void Parse_RejectsFrameOutsideFile()
    {
        var ico = IcoBuilder.Standard();
        BinaryPrimitives.WriteUInt32LittleEndian(ico.AsSpan(6 + 8, 4), 0xFFFF);

        Assert.Throws<InvalidDataException>(() => IcoFile.Parse(ico));
    }

    [Fact]
    public void Parse_RejectsTruncatedInput()
    {
        Assert.Throws<InvalidDataException>(() => IcoFile.Parse([0, 0, 1]));
    }

    [Fact]
    public void BuildGroup_UsesIdsInsteadOfOffsets()
    {
        var frames = IcoFile.Parse(IcoBuilder.Standard());
        var group = IcoFile.BuildGroup(frames, [100, 101, 102]);

        Assert.Equal(6 + 14 * 3, group.Length);
        Assert.Equal(3, BinaryPrimitives.ReadUInt16LittleEndian(group.AsSpan(4, 2)));
        // Entry 1 (48 px): bytes-in-res at +8, resource ID at +12.
        var entry = group.AsSpan(6 + 14, 14);
        Assert.Equal(48, entry[0]);
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(8, 4)));
        Assert.Equal(101, BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(12, 2)));
    }

    [Fact]
    public void BuildGroup_RejectsMismatchedIds()
    {
        var frames = IcoFile.Parse(IcoBuilder.Standard());

        Assert.Throws<ArgumentException>(() => IcoFile.BuildGroup(frames, [100]));
    }
}
