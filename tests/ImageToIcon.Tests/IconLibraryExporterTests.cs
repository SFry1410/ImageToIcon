using System.Text.Json;
using ImageToIcon.Library;
using ImageToIcon.Library.Storage;
using Xunit;

namespace ImageToIcon.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class IconLibraryExporterTests
{
    private static LibraryEntryResult Add(string dir, string name, byte[] ico, int keep = 5)
    {
        // Source icons live next to the library folder, never inside it.
        var icoPath = Path.Combine(Path.GetDirectoryName(dir)!, Guid.NewGuid().ToString("N") + ".ico");
        File.WriteAllBytes(icoPath, ico);
        return IconLibraryExporter.AddOrUpdate(new LibraryOptions(dir, keep), name, icoPath, "test");
    }

    private static LibraryIndex ReadIndex(string dir) => LibraryIndex.Load(Path.Combine(dir, LibraryIndex.FileName));

    [WindowsFact]
    public void FirstUpdate_CreatesLibraryWithIconsAndIndex()
    {
        using var scratch = new ScratchDir();
        var lib = Path.Combine(scratch.Path, "lib");

        var result = Add(lib, "app", IcoBuilder.Standard());

        var dll = Path.Combine(lib, LibraryIndex.AliasName);
        Assert.Equal(dll, result.DllPath);
        Assert.Equal(1, result.GroupId);
        Assert.Equal(0, result.IndexPosition);
        Assert.Equal(1, result.Version);
        Assert.False(result.UsedVersionedFile);
        Assert.True(ResourceProbe.HasGroup(dll, 1));
        Assert.True(ResourceProbe.HasFrame(dll, 100));

        var index = ReadIndex(lib);
        Assert.Equal(LibraryIndex.AliasName, index.Current);
        Assert.Equal(3, index.Icons["app"].FrameIds.Count);
        Assert.Equal(new[] { "256x256", "48x48", "16x16" }, index.Icons["app"].Sizes);
    }

    [WindowsFact]
    public void SameNameTwice_IsIdempotentAndReplacesOldFrames()
    {
        using var scratch = new ScratchDir();
        var lib = Path.Combine(scratch.Path, "lib");

        var first = Add(lib, "app", IcoBuilder.Standard());
        var second = Add(lib, "app", IcoBuilder.Standard());

        Assert.Equal(first.GroupId, second.GroupId);
        Assert.Equal(first.IndexPosition, second.IndexPosition);
        var dll = Path.Combine(lib, LibraryIndex.AliasName);
        Assert.True(ResourceProbe.HasGroup(dll, first.GroupId));
        // Frames from the first write (IDs 100..102) were deleted; the new ones are 103..105.
        Assert.False(ResourceProbe.HasFrame(dll, 100));
        Assert.True(ResourceProbe.HasFrame(dll, 103));

        var index = ReadIndex(lib);
        Assert.Single(index.Icons);
        Assert.Equal(new List<int> { 103, 104, 105 }, index.Icons["app"].FrameIds);
    }

    [WindowsFact]
    public void DifferentNames_GetSeparateGroupsAndPositions()
    {
        using var scratch = new ScratchDir();
        var lib = Path.Combine(scratch.Path, "lib");

        Add(lib, "alpha", IcoBuilder.Standard());
        var beta = Add(lib, "beta", IcoBuilder.Standard());

        var dll = Path.Combine(lib, LibraryIndex.AliasName);
        Assert.Equal(2, beta.GroupId);
        Assert.Equal(1, beta.IndexPosition);
        Assert.True(ResourceProbe.HasGroup(dll, 1));
        Assert.True(ResourceProbe.HasGroup(dll, 2));
    }

    [WindowsFact]
    public void Updates_KeepOnlyConfiguredNumberOfBackups()
    {
        using var scratch = new ScratchDir();
        var lib = Path.Combine(scratch.Path, "lib");

        for (var i = 0; i < 5; i++)
            Add(lib, "app", IcoBuilder.Standard(), keep: 2);

        var backups = Directory.GetFiles(lib, "Icons.v*.dll");
        Assert.True(backups.Length <= 2, $"Expected at most 2 backups, found {backups.Length}.");
        Assert.Equal(5, ReadIndex(lib).Version);
        Assert.True(File.Exists(Path.Combine(lib, LibraryIndex.AliasName)));
    }

    [WindowsFact]
    public void LockedAlias_FallsBackToVersionedFile()
    {
        using var scratch = new ScratchDir();
        var lib = Path.Combine(scratch.Path, "lib");
        Add(lib, "app", IcoBuilder.Standard());

        // Simulate Explorer holding the DLL: others may read it (so copying works) but not rename or
        // delete it, which is what makes the swap fail and the versioned fallback run.
        var alias = Path.Combine(lib, LibraryIndex.AliasName);
        using (new FileStream(alias, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = Add(lib, "other", IcoBuilder.Standard());

            Assert.True(result.UsedVersionedFile);
            Assert.Equal(LibraryNames.VersionedName(2), Path.GetFileName(result.DllPath));
            Assert.True(ResourceProbe.HasGroup(result.DllPath, result.GroupId));
            Assert.Equal(LibraryNames.VersionedName(2), ReadIndex(lib).Current);
        }
    }

    [WindowsFact]
    public void CorruptIndex_IsMovedAsideAndRebuilt()
    {
        using var scratch = new ScratchDir();
        var lib = Path.Combine(scratch.Path, "lib");
        Directory.CreateDirectory(lib);
        File.WriteAllText(Path.Combine(lib, LibraryIndex.FileName), "{ not json");

        var result = Add(lib, "app", IcoBuilder.Standard());

        Assert.Equal(1, result.Version);
        Assert.Single(Directory.GetFiles(lib, "*.corrupt"));
        Assert.Single(ReadIndex(lib).Icons);
    }

    [WindowsFact]
    public void InvalidIcon_ThrowsWithoutTouchingLibrary()
    {
        using var scratch = new ScratchDir();
        var lib = Path.Combine(scratch.Path, "lib");

        Assert.Throws<InvalidDataException>(() => Add(lib, "app", [1, 2, 3, 4]));
        // Parsing happens before anything is created, so the library folder never appears.
        Assert.False(Directory.Exists(lib));
    }

    [WindowsFact]
    public void LeftoverWorkFiles_AreRemovedAfterUpdate()
    {
        using var scratch = new ScratchDir();
        var lib = Path.Combine(scratch.Path, "lib");
        Add(lib, "app", IcoBuilder.Standard());
        Add(lib, "app", IcoBuilder.Standard());

        Assert.Empty(Directory.GetFiles(lib, "*.tmp"));
    }

    [Theory]
    [InlineData("a/b:c*", "a_b_c_")]
    [InlineData("  padded  ", "padded")]
    [InlineData("äöü", "___")]
    public void NormalizeName_ReplacesUnsafeCharacters(string input, string expected)
    {
        Assert.Equal(expected, LibraryNames.NormalizeName(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    public void NormalizeName_RejectsEmptyOrDotNames(string input)
    {
        Assert.Throws<ArgumentException>(() => LibraryNames.NormalizeName(input));
    }

    [Fact]
    public void NormalizeName_TruncatesLongNames()
    {
        Assert.Equal(64, LibraryNames.NormalizeName(new string('x', 200)).Length);
    }

    [Fact]
    public void IndexJson_RoundTrips()
    {
        var index = new LibraryIndex { Version = 7, Current = LibraryIndex.AliasName };
        index.Icons["x"] = new IconEntry { GroupId = 4, FrameIds = [100], Sizes = ["16x16"], Source = "s" };

        var restored = JsonSerializer.Deserialize<LibraryIndex>(index.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(7, restored.Version);
        Assert.Equal(4, restored.Icons["x"].GroupId);
    }
}
