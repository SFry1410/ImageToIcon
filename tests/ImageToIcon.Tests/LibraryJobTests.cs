using ImageToIcon.Library;
using ImageToIcon.Services;
using Xunit;

namespace ImageToIcon.Tests;

/// The elevated helper must refuse anything that is not a genuine workspace. These tests never
/// reach the write step, so they cannot touch the real library in %ProgramData%.
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class LibraryJobTests
{
    [WindowsFact]
    public void Run_RejectsJobOutsideWorkspaceFolder()
    {
        using var scratch = new ScratchDir();
        var job = Path.Combine(scratch.Path, "job.json");
        File.WriteAllText(job, "{}");

        Assert.Equal(1, LibraryJob.Run(job));
    }

    [WindowsFact]
    public void Run_RejectsWorkspaceWithoutMarker()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ImageToIcon-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var job = Path.Combine(dir, "job.json");
            File.WriteAllText(job, "{}");

            Assert.Equal(1, LibraryJob.Run(job));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [WindowsFact]
    public void Run_RejectsIconOutsideItsWorkspace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ImageToIcon-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        using var outside = new ScratchDir();
        try
        {
            // A live marker (this process, now). An old or dead marker would let the parallel stale-workspace
            // cleaner remove the folder before the assertion runs.
            File.WriteAllText(Path.Combine(dir, TempWorkspace.MarkerName), $"{Environment.ProcessId} {DateTime.UtcNow:O}");
            var icon = Path.Combine(outside.Path, "icon.ico");
            File.WriteAllBytes(icon, IcoBuilder.Standard());
            var job = Path.Combine(dir, "job.json");
            File.WriteAllText(job, $$"""
                {"Name":"x","Keep":5,"Source":"t","IcoPath":"{{icon.Replace("\\", "\\\\")}}","ResultPath":"{{Path.Combine(dir, "result.json").Replace("\\", "\\\\")}}"}
                """);

            Assert.Equal(1, LibraryJob.Run(job));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
