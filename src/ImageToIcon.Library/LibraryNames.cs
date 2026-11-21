using System.Text.RegularExpressions;

namespace ImageToIcon.Library;

/// Platform-neutral naming rules for library files and icon names.
public static partial class LibraryNames
{
    [GeneratedRegex(@"[^A-Za-z0-9._ -]", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeCharacters();

    /// File name of a backup or fallback DLL for a given version, e.g. Icons.v0003.dll.
    public static string VersionedName(int version) => $"Icons.v{version:D4}.dll";

    /// Restricts names to a safe, portable set. The name is only an index key; Explorer references use IDs.
    public static string NormalizeName(string name)
    {
        var cleaned = UnsafeCharacters().Replace(name.Trim(), "_");
        cleaned = cleaned.Length > 64 ? cleaned[..64] : cleaned;
        if (cleaned.Length == 0 || cleaned is "." or "..")
            throw new ArgumentException("The icon name is empty or invalid.", nameof(name));
        return cleaned;
    }
}
