namespace Presenter.Core.Models;

/// <summary>
/// A named presentation schedule for a specific date. Ported from App_Code/Schedule.cs;
/// persistence concerns (Save/Load/Delete) live in Presenter.Data, this class keeps the
/// pure collection logic only.
/// </summary>
public class Schedule
{
    public Guid ID { get; set; }
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";

    public ICollection<Item> Items { get; set; } = new List<Item>();

    public string DisplayName => Date.ToString("ddd, d MMM yyyy") + " - " + Name;

    /// <summary>
    /// Adds an item to the schedule (in memory only — the caller is responsible for saving).
    /// </summary>
    /// <returns>True if added, false if the file type is not supported.</returns>
    public bool AddItem(string filename, IReadOnlyCollection<string> supportedFileTypes)
    {
        string ext = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
        if (!supportedFileTypes.Contains(ext))
            return false;

        Items.Add(new Item
        {
            ID = Guid.NewGuid(),
            ScheduleID = ID,
            Filename = filename,
            Ordinal = (short)((Items.Max(i => (short?)i.Ordinal) ?? -1) + 1)
        });
        return true;
    }

    /// <summary>
    /// Removes an item and renumbers the remaining ordinals (in memory only).
    /// </summary>
    public void RemoveItem(Item item)
    {
        Items.Remove(item);
        Renumber();
    }

    public void RemoveItems(IEnumerable<Item> items)
    {
        foreach (var item in items.ToArray())
            Items.Remove(item);
        Renumber();
    }

    /// <summary>
    /// Moves <paramref name="source"/> to the position currently occupied by
    /// <paramref name="dest"/>, or to the end when <paramref name="dest"/> is null.
    /// Renumbers all ordinals. (Port of Schedule.ReOrder.)
    /// </summary>
    public void MoveItem(Item source, Item? dest)
    {
        short idx = 0;
        foreach (var item in Items.OrderBy(i => i.Ordinal).ToArray())
        {
            if (item == source)
                continue;
            if (item == dest)
                source.Ordinal = idx++;
            item.Ordinal = idx++;
        }
        if (dest == null)
            source.Ordinal = idx;
    }

    /// <summary>
    /// Reassigns ordinals 0..n-1 preserving the current order.
    /// </summary>
    public void Renumber()
    {
        short idx = 0;
        foreach (var item in Items.OrderBy(i => i.Ordinal).ToArray())
            item.Ordinal = idx++;
    }
}
