using Presenter.Core.Services;
using Presenter.Core.Settings;

namespace Presenter.Core.Tests;

public class LibraryPathResolverTests
{
    [Fact]
    public void Resolve_WindowsStyleDefault_ResolvesUnderMyDocumentsOnAnyPlatform()
    {
        //the default setting (and settings migrated from the Windows app) uses
        //backslashes; it must still resolve under My Documents on macOS/Linux
        var settings = new PresenterSettings { LibraryPath = @"My Documents\Library\" };

        string result = LibraryPathResolver.Resolve(settings);

        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Library") + Path.DirectorySeparatorChar;
        Assert.Equal(expected, result);
        Assert.True(Directory.Exists(result));
    }

    [Fact]
    public void Resolve_AbsolutePath_ReturnedWithTrailingSeparator()
    {
        string dir = Path.Combine(Path.GetTempPath(), "presenter-test-lib-" + Guid.NewGuid().ToString("N"));
        var settings = new PresenterSettings { LibraryPath = dir };

        try
        {
            Assert.Equal(dir + Path.DirectorySeparatorChar, LibraryPathResolver.Resolve(settings));
        }
        finally
        {
            Directory.Delete(dir);
        }
    }
}
