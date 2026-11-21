namespace ImageToIcon.Library.Storage;

internal static class AtomicFile
{
    /// Writes text to a temp file in the same directory and swaps it in with a rename, so a
    /// reader never sees a half-written file.
    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path)!;
        var tmp = Path.Combine(dir, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tmp, content);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            TryDelete(tmp);
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Locked by another process; the caller decides whether that matters.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}
