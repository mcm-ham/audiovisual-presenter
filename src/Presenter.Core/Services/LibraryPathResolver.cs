using Presenter.Core.Settings;

namespace Presenter.Core.Services;

/// <summary>
/// Resolves the configured library path to an absolute directory path ending in a
/// separator, creating it when missing. Ported from Config.LibraryPath: relative paths
/// starting with a special folder name ("My Documents", "My Music", "My Pictures")
/// resolve under that folder.
/// </summary>
public static class LibraryPathResolver
{
    public static string Resolve(PresenterSettings settings)
    {
        try
        {
            //the stored path may use Windows separators (default setting, or a database
            //migrated from the Windows app), which macOS/Linux Path APIs don't parse
            string res = Util.NormalizeSeparators(settings.LibraryPath);
            if (!Path.IsPathRooted(res))
            {
                string[] parts = res.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                Environment.SpecialFolder? folder = Util.Parse<Environment.SpecialFolder?>((parts.FirstOrDefault() ?? "").Replace(" ", ""));
                if (folder is Environment.SpecialFolder.MyDocuments or Environment.SpecialFolder.MyMusic or Environment.SpecialFolder.MyPictures)
                    res = Environment.GetFolderPath(folder.Value) + Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar, parts.Skip(1));
                else
                    res = Path.GetFullPath(res);
            }

            string path = res.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(path);
            return path;
        }
        catch (Exception)
        {
            string fallback = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + Path.DirectorySeparatorChar + "Library" + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
