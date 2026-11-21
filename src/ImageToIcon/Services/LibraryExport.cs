using ImageToIcon.Library;
using SkiaSharp;

namespace ImageToIcon.Services;

/// Shared entry point for saving frames into the icon library, used by both the CLI and the GUI.
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class LibraryExport
{
    public static LibraryEntryResult SaveFrames(
        IEnumerable<SKBitmap> frames,
        string libraryDirectory,
        string name,
        int keep,
        bool elevate,
        IIconLibraryLog log)
    {
        // The intermediate .ico lives only in a temp workspace and is removed when it goes out of scope.
        using var workspace = TempWorkspace.Create();
        var icoPath = workspace.FilePath("icon.ico");
        using (var stream = File.Create(icoPath))
            IconFactory.Save(frames, stream);

        if (!elevate)
            return IconLibraryExporter.AddOrUpdate(new LibraryOptions(libraryDirectory, keep), name, icoPath, "ImageToIcon", log);

        if (!string.Equals(Path.GetFullPath(libraryDirectory), Path.GetFullPath(LibraryPaths.DefaultDirectory), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Elevated updates are only supported for the default library directory.");

        return LibraryJob.Elevate(workspace, new LibraryJobSpec
        {
            Name = name,
            Keep = keep,
            Source = "ImageToIcon",
            IcoPath = icoPath,
            ResultPath = workspace.FilePath("result.json")
        }, log);
    }
}
