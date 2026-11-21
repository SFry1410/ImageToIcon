using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageToIcon.Library;

namespace ImageToIcon.Services;

/// Work order handed from the normal process to an elevated helper via a file in a TempWorkspace.
public sealed class LibraryJobSpec
{
    public string Name { get; set; } = string.Empty;
    public int Keep { get; set; } = 5;
    public string Source { get; set; } = string.Empty;
    public string IcoPath { get; set; } = string.Empty;
    public string ResultPath { get; set; } = string.Empty;
}

/// Outcome written back by the elevated helper.
public sealed class LibraryJobResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? DllPath { get; set; }
    public int GroupId { get; set; }
    public int IndexPosition { get; set; }
    public int Version { get; set; }
    public bool UsedVersionedFile { get; set; }
}

/// Command-line switch for the elevated helper. Kept outside the Windows-only class so the
/// entry point can check it on every platform.
public static class LibraryJobArgs
{
    public const string Switch = "--library-job=";
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LibraryJobSpec))]
[JsonSerializable(typeof(LibraryJobResult))]
internal partial class LibraryJobJsonContext : JsonSerializerContext;

/// Runs an elevated library update. The helper only accepts files inside a real workspace folder
/// and only writes to the default library directory, so the elevation cannot be used to write
/// arbitrary paths.
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class LibraryJob
{
    public const string Switch = LibraryJobArgs.Switch;

    public static LibraryEntryResult Elevate(TempWorkspace workspace, LibraryJobSpec spec, IIconLibraryLog log)
    {
        var jobPath = workspace.FilePath("job.json");
        File.WriteAllText(jobPath, JsonSerializer.Serialize(spec, LibraryJobJsonContext.Default.LibraryJobSpec));

        var psi = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the executable."))
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = workspace.Path,
            Arguments = $"{Switch}\"{jobPath}\""
        };

        log.Info("Requesting administrator rights to update the icon library...");
        try
        {
            using var process = Process.Start(psi) ?? throw new LibraryUpdateException("The elevated helper could not be started.");
            process.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new LibraryUpdateException("Administrator confirmation was cancelled. The icon library was not changed.", ex);
        }

        if (!File.Exists(spec.ResultPath))
            throw new LibraryUpdateException("The elevated update did not report a result.");

        var result = JsonSerializer.Deserialize(File.ReadAllText(spec.ResultPath), LibraryJobJsonContext.Default.LibraryJobResult)
                     ?? throw new LibraryUpdateException("The elevated update returned an empty result.");
        if (!result.Ok)
            throw new LibraryUpdateException(result.Error ?? "The elevated update failed.");

        return new LibraryEntryResult(result.DllPath!, result.GroupId, result.IndexPosition, result.Version, result.UsedVersionedFile);
    }

    /// Entry point for the elevated process. Returns the process exit code.
    public static int Run(string jobFile)
    {
        var log = new ConsoleLog();
        string? resultPath = null;
        try
        {
            var job = Path.GetFullPath(jobFile);
            var workDir = Path.GetDirectoryName(job) ?? throw new InvalidDataException("Invalid job location.");
            RequireWorkspace(workDir);

            var spec = JsonSerializer.Deserialize(File.ReadAllText(job), LibraryJobJsonContext.Default.LibraryJobSpec)
                       ?? throw new InvalidDataException("Job file is empty.");
            resultPath = RequireInside(spec.ResultPath, workDir);
            var icoPath = RequireInside(spec.IcoPath, workDir);
            if (!File.Exists(icoPath))
                throw new InvalidDataException("The icon file for this job is missing.");
            if (spec.Keep is < 1 or > 100)
                throw new InvalidDataException("Keep must be between 1 and 100.");

            var entry = IconLibraryExporter.AddOrUpdate(new LibraryOptions(LibraryPaths.SystemDirectory, spec.Keep), spec.Name, icoPath, spec.Source, log);
            WriteResult(resultPath, new LibraryJobResult
            {
                Ok = true,
                DllPath = entry.DllPath,
                GroupId = entry.GroupId,
                IndexPosition = entry.IndexPosition,
                Version = entry.Version,
                UsedVersionedFile = entry.UsedVersionedFile
            });
            return 0;
        }
        catch (Exception ex)
        {
            if (resultPath != null)
                WriteResult(resultPath, new LibraryJobResult { Ok = false, Error = ex.Message });
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void RequireWorkspace(string dir)
    {
        var name = Path.GetFileName(dir);
        if (!name.StartsWith(TempWorkspace.Prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Job is not inside a workspace folder.");
        if (!File.Exists(Path.Combine(dir, TempWorkspace.MarkerName)))
            throw new InvalidDataException("Workspace marker is missing.");
        if (!Path.GetFullPath(dir).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Workspace is outside the temp directory.");
    }

    private static string RequireInside(string path, string workDir)
    {
        var full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full), workDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Job references a file outside its workspace.");
        return full;
    }

    private static void WriteResult(string path, LibraryJobResult result)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(result, LibraryJobJsonContext.Default.LibraryJobResult));
    }
}

internal sealed class ConsoleLog : IIconLibraryLog
{
    public void Info(string message) => Console.WriteLine(message);
    public void Warn(string message) => Console.WriteLine("Warning: " + message);
    public void Error(string message) => Console.Error.WriteLine("Error: " + message);
}
