namespace ImageToIcon.Services;

/// Upper bounds applied before any decoder allocates pixel buffers, so a tiny file with a
/// forged header cannot force a multi-gigabyte allocation.
public static class DecodeLimits
{
    /// 64 Mpx ≈ 256 MB of BGRA. Far above any realistic icon source.
    public const long MaxPixels = 64L * 1024 * 1024;

    public static bool PixelCountAllowed(long width, long height)
    {
        return width > 0 && height > 0 && width * height <= MaxPixels;
    }
}
