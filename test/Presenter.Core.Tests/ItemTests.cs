using Presenter.Core.Models;

namespace Presenter.Core.Tests;

public class ItemTests : IDisposable
{
    private readonly string _library;

    public ItemTests()
    {
        _library = Path.Combine(Path.GetTempPath(), "presenter-tests", Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(_library);
    }

    public void Dispose()
    {
        try { Directory.Delete(_library.TrimEnd(Path.DirectorySeparatorChar), recursive: true); } catch { }
    }

    [Fact]
    public void TryResolveFile_ExistingAbsolutePath_Found()
    {
        string file = Path.Combine(_library, "deck.pptx");
        File.WriteAllText(file, "x");
        var item = new Item { Filename = file };

        Assert.True(item.TryResolveFile(_library, out bool changed));
        Assert.False(changed);
    }

    [Fact]
    public void TryResolveFile_RelocatedLibrary_FindsInSubfolder_AndUpdatesFilename()
    {
        // file lives at {library}/Presentations/deck.pptx but item points at an old library location
        Directory.CreateDirectory(Path.Combine(_library, "Presentations"));
        string actual = Path.Combine(_library, "Presentations", "deck.pptx");
        File.WriteAllText(actual, "x");
        var item = new Item { Filename = Path.Combine(Path.GetTempPath(), "presenter-old-library", "Presentations", "deck.pptx") };

        Assert.True(item.TryResolveFile(_library, out bool changed));
        Assert.True(changed);
        Assert.Equal(actual, item.Filename);
    }

    [Fact]
    public void TryResolveFile_MigratedWindowsFilename_FindsInSubfolder_AndUpdatesFilename()
    {
        // a database migrated from the Windows app stores backslash-separated filenames;
        // relocation under the library path must still work on every platform
        Directory.CreateDirectory(Path.Combine(_library, "Presentations"));
        string actual = Path.Combine(_library, "Presentations", "deck.pptx");
        File.WriteAllText(actual, "x");
        var item = new Item { Filename = @"C:\old-library\Presentations\deck.pptx" };

        Assert.True(item.TryResolveFile(_library, out bool changed));
        Assert.True(changed);
        Assert.Equal(actual, item.Filename);
    }

    [Fact]
    public void TryResolveFile_FindsInLibraryRoot_AndUpdatesFilename()
    {
        string actual = Path.Combine(_library, "song.mp3");
        File.WriteAllText(actual, "x");
        // migrated Windows filename: root fallback relies on parsing the name out of it
        var item = new Item { Filename = @"C:\somewhere\else\entirely\song.mp3" };

        Assert.True(item.TryResolveFile(_library, out bool changed));
        Assert.True(changed);
        Assert.Equal(actual, item.Filename);
    }

    [Fact]
    public void TryResolveFile_Missing_ReturnsFalse_AndCaches()
    {
        var item = new Item { Filename = Path.Combine(_library, "missing.pptx") };

        Assert.False(item.TryResolveFile(_library, out _));

        // now create the file — still false because the result is cached...
        File.WriteAllText(Path.Combine(_library, "missing.pptx"), "x");
        Assert.False(item.TryResolveFile(_library, out _));

        // ...until the cache is invalidated
        item.InvalidateFoundCache();
        Assert.True(item.TryResolveFile(_library, out _));
    }

    [Fact]
    public void IsTemplateNone_MatchesNonePot()
    {
        Assert.True(new Item { Filename = Path.Combine("lib", "Templates", "None.pot") }.IsTemplateNone);
        Assert.False(new Item { Filename = Path.Combine("lib", "Templates", "Sky.potx") }.IsTemplateNone);

        // migrated Windows filenames keep their backslash separators
        Assert.True(new Item { Filename = @"C:\lib\Templates\None.pot" }.IsTemplateNone);
        Assert.False(new Item { Filename = @"C:\lib\Templates\Sky.potx" }.IsTemplateNone);
    }

    [Fact]
    public void TryResolveFile_TemplateNone_AlwaysFound()
    {
        var item = new Item { Filename = @"C:\does\not\exist\None.pot" };

        Assert.True(item.TryResolveFile(_library, out _));
    }
}
