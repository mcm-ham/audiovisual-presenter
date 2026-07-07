namespace Presenter.Core.Models;

/// <summary>
/// A media file (PowerPoint, video, audio, image or template) within a schedule.
/// Ported from App_Code/Item.cs; database persistence lives in Presenter.Data.
/// </summary>
public class Item
{
    public Guid ID { get; set; }
    public Guid ScheduleID { get; set; }
    public string Filename { get; set; } = "";
    public short Ordinal { get; set; }

    public Schedule? Schedule { get; set; }
    public ICollection<Flag> Flags { get; set; } = new List<Flag>();

    public string Name => Util.GetFileName(Filename);

    /// <summary>
    /// True when this item is the sentinel "no template" entry (None.pot).
    /// </summary>
    public bool IsTemplateNone => Util.GetFileName(Filename).ToLowerInvariant() == "none.pot";

    private bool? _found;

    /// <summary>
    /// Hook used by the app layer to supply library-path resolution (and persist fixed
    /// filenames) for the <see cref="IsFound"/> binding property. When unset, IsFound
    /// falls back to a plain file-existence check.
    /// </summary>
    public static Func<Item, bool>? FoundResolver { get; set; }

    /// <summary>
    /// Binding-friendly wrapper over <see cref="TryResolveFile"/> (the schedule list
    /// XAML binds a red-foreground trigger to this).
    /// </summary>
    public bool IsFound => FoundResolver?.Invoke(this) ?? (File.Exists(Filename) || IsTemplateNone);

    /// <summary>
    /// Checks whether the file exists on disk, falling back to searching under the library
    /// path in case the library has been moved (port of Item.IsFound). When the file is
    /// found at a relocated path, <see cref="Filename"/> is updated and
    /// <paramref name="filenameChanged"/> is set so the caller can persist the fix.
    /// The result is cached; use <see cref="InvalidateFoundCache"/> to re-check.
    /// </summary>
    public bool TryResolveFile(string libraryPath, out bool filenameChanged)
    {
        filenameChanged = false;

        if (_found.HasValue)
            return _found.Value;

        if (File.Exists(Filename) || IsTemplateNone)
        {
            _found = true;
            return true;
        }

        // check for file under library (in sub-folder) in case library path has been moved;
        // stored filenames may use either separator when migrated from the Windows app
        int lastSep = Filename.LastIndexOfAny(Util.DirectorySeparators);
        if (lastSep > 0)
        {
            int prevSep = Filename.LastIndexOfAny(Util.DirectorySeparators, lastSep - 1);
            string newpath = libraryPath + Util.NormalizeSeparators(Filename.Substring(prevSep + 1));
            if (File.Exists(newpath))
            {
                Filename = newpath;
                filenameChanged = true;
                _found = true;
                return true;
            }
        }

        // check for file under library root
        string rootPath = libraryPath + Name;
        if (File.Exists(rootPath))
        {
            Filename = rootPath;
            filenameChanged = true;
            _found = true;
            return true;
        }

        _found = false;
        return false;
    }

    public void InvalidateFoundCache() => _found = null;
}
