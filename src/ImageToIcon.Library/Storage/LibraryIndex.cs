using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImageToIcon.Library.Storage;

/// Describes the icons stored in the active library DLL. Persisted next to the DLL as JSON.
public sealed class LibraryIndex
{
    public const string FileName = "Icons.index.json";
    public const string AliasName = "Icons.dll";

    /// Version of the active DLL. Increases by one on every successful update.
    public int Version { get; set; }

    /// File name of the active DLL: the stable alias, or a versioned file when the alias
    /// could not be replaced (e.g. while Explorer holds it).
    public string Current { get; set; } = AliasName;

    public int NextGroupId { get; set; } = 1;
    public int NextFrameId { get; set; } = 100;

    public Dictionary<string, IconEntry> Icons { get; set; } = new(StringComparer.Ordinal);

    public static LibraryIndex Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, LibraryJsonContext.Default.LibraryIndex)
               ?? throw new InvalidDataException("Index file is empty.");
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, LibraryJsonContext.Default.LibraryIndex);
    }
}

public sealed class IconEntry
{
    public int GroupId { get; set; }
    public List<int> FrameIds { get; set; } = [];
    public List<string> Sizes { get; set; } = [];
    public string Source { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LibraryIndex))]
internal partial class LibraryJsonContext : JsonSerializerContext;
