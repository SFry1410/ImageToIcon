using ImageToIcon.Library;
using Xunit;

namespace ImageToIcon.Tests;

public class TempWorkspaceTests
{
    [Fact]
    public void Dispose_RemovesOwnedFilesMarkerAndFolder()
    {
        string path;
        string owned;
        using (var ws = TempWorkspace.Create())
        {
            path = ws.Path;
            owned = ws.FilePath("icon.ico");
            File.WriteAllText(owned, "x");
        }

        Assert.False(File.Exists(owned));
        Assert.False(File.Exists(Path.Combine(path, TempWorkspace.MarkerName)));
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void Dispose_KeepsUnregisteredFiles()
    {
        string path;
        string foreign;
        using (var ws = TempWorkspace.Create())
        {
            path = ws.Path;
            foreign = Path.Combine(path, "user-data.txt");
            File.WriteAllText(foreign, "keep me");
        }

        // The folder is not empty, so it stays, and the foreign file is untouched.
        Assert.True(File.Exists(foreign));
        Directory.Delete(path, recursive: true);
    }

    [Theory]
    [InlineData("..\\evil.ico")]
    [InlineData("sub/evil.ico")]
    [InlineData("")]
    public void FilePath_RejectsPathsOutsideWorkspace(string name)
    {
        using var ws = TempWorkspace.Create();

        Assert.Throws<ArgumentException>(() => ws.FilePath(name));
    }

    [Fact]
    public void FilePath_RejectsMarkerName()
    {
        using var ws = TempWorkspace.Create();

        Assert.Throws<ArgumentException>(() => ws.FilePath(TempWorkspace.MarkerName));
    }
}

public class StaleWorkspaceCleanerTests
{
    [Fact]
    public void Clean_RemovesWorkspaceOfDeadProcess()
    {
        var dir = NewWorkspaceDir();
        File.WriteAllText(Path.Combine(dir, TempWorkspace.MarkerName), $"2147483646 {DateTime.UtcNow:O}");
        File.WriteAllText(Path.Combine(dir, "icon.ico"), "x");

        StaleWorkspaceCleaner.Clean();

        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Clean_KeepsWorkspaceOfRunningProcessWithinAge()
    {
        var dir = NewWorkspaceDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, TempWorkspace.MarkerName), $"{Environment.ProcessId} {DateTime.UtcNow:O}");

            StaleWorkspaceCleaner.Clean();

            Assert.True(Directory.Exists(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Clean_IgnoresFoldersWithoutMarkerOrWithSubfolders()
    {
        var noMarker = NewWorkspaceDir();
        var withSubfolder = NewWorkspaceDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(withSubfolder, "nested"));
            File.WriteAllText(Path.Combine(withSubfolder, TempWorkspace.MarkerName), $"2147483646 {DateTime.UtcNow:O}");

            StaleWorkspaceCleaner.Clean();

            Assert.True(Directory.Exists(noMarker));
            Assert.True(Directory.Exists(withSubfolder));
        }
        finally
        {
            Directory.Delete(noMarker, recursive: true);
            Directory.Delete(withSubfolder, recursive: true);
        }
    }

    private static string NewWorkspaceDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), TempWorkspace.Prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
