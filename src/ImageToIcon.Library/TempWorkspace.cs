using System.Diagnostics;
using System.Globalization;
using ImageToIcon.Library.Storage;

namespace ImageToIcon.Library;

/// A per-run scratch folder under the user's temp directory. Only the files registered through
/// <see cref="FilePath"/> are ever deleted; the folder itself is removed only when empty, so user
/// data can never end up in it by accident.
public sealed class TempWorkspace : IDisposable
{
    public const string Prefix = "ImageToIcon-ws-";
    public const string MarkerName = ".imagetoicon-workspace";

    private readonly List<string> _ownedFiles = [];
    private bool _disposed;

    private TempWorkspace(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempWorkspace Create()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var marker = System.IO.Path.Combine(dir, MarkerName);
        File.WriteAllText(marker, $"{Environment.ProcessId} {DateTime.UtcNow:O}");
        return new TempWorkspace(dir);
    }

    /// Returns a path inside the workspace and registers it as owned. Creation itself is up to the caller.
    public string FilePath(string fileName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (fileName != System.IO.Path.GetFileName(fileName) || fileName.Length == 0 || fileName == MarkerName)
            throw new ArgumentException("Invalid workspace file name.", nameof(fileName));

        var full = System.IO.Path.Combine(Path, fileName);
        _ownedFiles.Add(full);
        return full;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var file in _ownedFiles)
            AtomicFile.TryDelete(file);
        AtomicFile.TryDelete(System.IO.Path.Combine(Path, MarkerName));
        try
        {
            Directory.Delete(Path, recursive: false);
        }
        catch (IOException)
        {
            // Not empty or still in use; leave it for the stale-workspace cleaner.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}

/// Removes workspaces left behind by crashed or killed runs. Only folders that carry our marker,
/// use our prefix, and contain nothing but flat files are touched.
public static class StaleWorkspaceCleaner
{
    public static int Clean(IIconLibraryLog? log = null, TimeSpan? maxAge = null)
    {
        log ??= NullLog.Instance;
        var limit = maxAge ?? TimeSpan.FromHours(24);
        var removed = 0;

        IEnumerable<string> candidates;
        try
        {
            candidates = Directory.EnumerateDirectories(System.IO.Path.GetTempPath(), TempWorkspace.Prefix + "*");
        }
        catch (IOException)
        {
            return 0;
        }

        foreach (var dir in candidates)
        {
            try
            {
                if (IsStale(dir, limit) && RemoveWorkspace(dir))
                    removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Warn($"Could not check temp folder '{dir}': {ex.Message}");
            }
        }

        if (removed > 0)
            log.Info($"Removed {removed} stale temp workspace(s).");
        return removed;
    }

    private static bool IsStale(string dir, TimeSpan limit)
    {
        if ((new DirectoryInfo(dir).Attributes & FileAttributes.ReparsePoint) != 0)
            return false;

        var marker = System.IO.Path.Combine(dir, TempWorkspace.MarkerName);
        if (!File.Exists(marker))
            return false;

        var parts = File.ReadAllText(marker).Split(' ', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            return false;
        if (!DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var created))
            return false;

        return !IsProcessAlive(pid) || DateTime.UtcNow - created > limit;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool RemoveWorkspace(string dir)
    {
        // Subfolders mean something other than us created this; refuse.
        if (Directory.EnumerateDirectories(dir).Any())
            return false;

        foreach (var file in Directory.EnumerateFiles(dir))
            File.Delete(file);
        Directory.Delete(dir, recursive: false);
        return true;
    }
}
