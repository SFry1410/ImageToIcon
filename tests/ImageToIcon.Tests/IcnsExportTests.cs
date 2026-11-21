using System.Buffers.Binary;
using ImageToIcon.Services;
using SkiaSharp;
using Xunit;

namespace ImageToIcon.Tests;

public class IcnsExportTests
{
    [Fact]
    public void SaveIcns_WritesContainerWithCorrectHeader()
    {
        using var bitmap = new SKBitmap(256, 256);
        using var stream = new MemoryStream();

        IconFactory.SaveIcns([bitmap], stream);

        var bytes = stream.ToArray();
        Assert.Equal("icns", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(bytes.Length, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(4, 4)));
    }

    [Fact]
    public void SaveIcns_RejectsSizesWithoutIcnsType()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var stream = new MemoryStream();

        Assert.Throws<InvalidOperationException>(() => IconFactory.SaveIcns([bitmap], stream));
    }
}
