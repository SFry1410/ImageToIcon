using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using ImageToIcon.Library.Icons;
using ImageToIcon.Library.Native;
using ImageToIcon.Library.Storage;

namespace ImageToIcon.Library;

public sealed record LibraryOptions(string Directory, int Keep = 5);

public sealed record LibraryEntryResult(
    string DllPath,
    int GroupId,
    int IndexPosition,
    int Version,
    bool UsedVersionedFile)
{
    /// Explorer reference by resource ID. Stable across removals; preferred.
    public string ReferenceById => $"{DllPath},-{GroupId}";

    /// Explorer reference by position (0-based, ascending group ID). Only valid while no
    /// icons are removed from the library.
    public string ReferenceByIndex => $"{DllPath},{IndexPosition}";
}

/// Writes multi-size icons into a resource-only library DLL. Updates never patch the active file
/// in place: a copy is modified, verified, and swapped in. See README for the on-disk layout.
[SupportedOSPlatform("windows")]
public static partial class IconLibraryExporter
{
    private const string MutexName = @"Global\ImageToIconLibrary";
    private static readonly TimeSpan MutexTimeout = TimeSpan.FromSeconds(30);

    [GeneratedRegex(@"^Icons\.v(\d{4})\.dll$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionedNamePattern();

    public static LibraryEntryResult AddOrUpdate(LibraryOptions options, string name, string icoPath, string source, IIconLibraryLog? log = null)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The icon library is only available on Windows.");

        log ??= NullLog.Instance;
        var iconName = LibraryNames.NormalizeName(name);
        var frames = IcoFile.Parse(File.ReadAllBytes(icoPath));

        var dir = options.Directory;
        if (string.IsNullOrWhiteSpace(dir))
            throw new ArgumentException("A library directory is required.", nameof(options));
        dir = Path.GetFullPath(dir);
        LibraryPaths.EnsureDirectory(dir, log);

        using var mutex = new Mutex(false, MutexName);
        if (!mutex.WaitOne(MutexTimeout))
            throw new LibraryUpdateException("Another icon library update is still running. Try again in a moment.");

        try
        {
            return Update(dir, options.Keep, iconName, frames, source, log);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new LibraryAccessDeniedException(dir, ex);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static LibraryEntryResult Update(string dir, int keep, string name, IReadOnlyList<IcoFrame> frames, string source, IIconLibraryLog log)
    {
        var indexPath = Path.Combine(dir, LibraryIndex.FileName);
        var index = LoadIndexOrStart(indexPath, log);

        var activePath = Path.Combine(dir, index.Current);
        var workPath = Path.Combine(dir, $"Icons.new.{Guid.NewGuid():N}.tmp");
        try
        {
            if (File.Exists(activePath))
                File.Copy(activePath, workPath);
            else
                MinimalDll.Create(workPath);

            var entry = index.Icons.GetValueOrDefault(name);
            var groupId = entry?.GroupId ?? index.NextGroupId++;
            var frameIds = new List<ushort>(frames.Count);
            var nextFrameId = index.NextFrameId;

            ResourceWriter.Apply(workPath, session =>
            {
                if (entry != null)
                {
                    foreach (var old in entry.FrameIds)
                        session.Delete(ResourceWriter.Session.IconType, (ushort)old);
                    session.Delete(ResourceWriter.Session.GroupType, (ushort)entry.GroupId);
                }

                foreach (var frame in frames)
                {
                    var id = NextId(ref nextFrameId);
                    frameIds.Add(id);
                    session.Write(ResourceWriter.Session.IconType, id, frame.Data);
                }

                session.Write(ResourceWriter.Session.GroupType, (ushort)groupId, IcoFile.BuildGroup(frames, frameIds));
            });

            VerifyResources(workPath, groupId, frameIds);
            index.NextFrameId = nextFrameId;

            var newVersion = index.Version + 1;
            var (current, fallback) = Activate(dir, workPath, index, newVersion, log);

            index.Version = newVersion;
            index.Current = current;
            index.Icons[name] = new IconEntry
            {
                GroupId = groupId,
                FrameIds = frameIds.Select(i => (int)i).ToList(),
                Sizes = frames.Select(f => $"{f.PixelWidth}x{f.PixelHeight}").ToList(),
                Source = source,
                UpdatedUtc = DateTime.UtcNow
            };
            AtomicFile.WriteAllText(indexPath, index.ToJson());

            PruneVersions(dir, index, keep, log);

            var activeFull = Path.Combine(dir, current);
            var position = index.Icons.Values.OrderBy(e => e.GroupId).ToList().FindIndex(e => e.GroupId == groupId);
            log.Info($"Icon '{name}' written to {activeFull} (group {groupId}, version {newVersion}).");

            return new LibraryEntryResult(activeFull, groupId, position, newVersion, fallback);
        }
        finally
        {
            AtomicFile.TryDelete(workPath);
        }
    }

    private static LibraryIndex LoadIndexOrStart(string indexPath, IIconLibraryLog log)
    {
        if (!File.Exists(indexPath))
            return new LibraryIndex();

        try
        {
            return LibraryIndex.Load(indexPath);
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException)
        {
            // Keep the broken file for inspection and start a fresh index. The DLL resources
            // remain; orphaned icons from the old index are simply no longer referenced.
            var backup = indexPath + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".corrupt";
            File.Move(indexPath, backup, overwrite: true);
            log.Warn($"Index was unreadable ({ex.Message}); moved to '{backup}' and starting a new index.");
            return new LibraryIndex();
        }
    }

    /// Swaps the new DLL in. Preferred: move the active alias aside and put the new file under the
    /// alias name. If Windows refuses (file locked by another process), the new file is kept under
    /// a versioned name and the index points at it instead.
    private static (string current, bool fallback) Activate(string dir, string newFile, LibraryIndex index, int newVersion, IIconLibraryLog log)
    {
        var aliasPath = Path.Combine(dir, LibraryIndex.AliasName);
        try
        {
            if (File.Exists(aliasPath))
            {
                var backup = Path.Combine(dir, LibraryNames.VersionedName(index.Version));
                File.Move(aliasPath, backup, overwrite: true);
            }

            File.Move(newFile, aliasPath, overwrite: false);
            return (LibraryIndex.AliasName, false);
        }
        catch (IOException ex)
        {
            log.Warn($"Could not replace {LibraryIndex.AliasName} ({ex.Message}); using a versioned file instead.");
        }
        catch (UnauthorizedAccessException ex)
        {
            log.Warn($"Could not replace {LibraryIndex.AliasName} ({ex.Message}); using a versioned file instead.");
        }

        var versionedName = LibraryNames.VersionedName(newVersion);
        File.Move(newFile, Path.Combine(dir, versionedName), overwrite: true);
        return (versionedName, true);
    }

    /// Keeps the newest `keep` backups, never the active file. Locked files are skipped and
    /// retried on the next run.
    private static void PruneVersions(string dir, LibraryIndex index, int keep, IIconLibraryLog log)
    {
        var backups = Directory.EnumerateFiles(dir)
                               .Select(Path.GetFileName)
                               .Where(n => n != null && n != index.Current && VersionedNamePattern().IsMatch(n))
                               .OrderByDescending(n => int.Parse(VersionedNamePattern().Match(n!).Groups[1].ValueSpan))
                               .ToList();

        foreach (var stale in backups.Skip(Math.Max(keep, 0)))
        {
            var path = Path.Combine(dir, stale!);
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                log.Warn($"Skipped removing '{stale}' because it is in use; it will be retried on the next update.");
            }
            catch (UnauthorizedAccessException)
            {
                log.Warn($"Skipped removing '{stale}' because access was denied.");
            }
        }
    }

    private static void VerifyResources(string path, int groupId, IReadOnlyList<ushort> frameIds)
    {
        var module = Kernel32.LoadLibraryEx(path, IntPtr.Zero, Kernel32.LoadLibraryAsDataFile);
        if (module == IntPtr.Zero)
            throw new LibraryUpdateException($"The updated library cannot be loaded (Win32 error {Marshal.GetLastPInvokeError()}).");
        try
        {
            if (Kernel32.FindResource(module, (IntPtr)groupId, Kernel32.RtGroupIcon) == IntPtr.Zero)
                throw new LibraryUpdateException("Verification failed: icon group is missing from the updated library.");
            foreach (var id in frameIds)
            {
                if (Kernel32.FindResource(module, (IntPtr)id, Kernel32.RtIcon) == IntPtr.Zero)
                    throw new LibraryUpdateException($"Verification failed: icon frame {id} is missing.");
            }
        }
        finally
        {
            Kernel32.FreeLibrary(module);
        }
    }

    private static ushort NextId(ref int next)
    {
        if (next > ushort.MaxValue)
            throw new LibraryUpdateException("The icon library has run out of resource IDs. Create a new library.");
        return (ushort)next++;
    }

}
