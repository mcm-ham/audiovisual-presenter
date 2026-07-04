namespace Presenter.Core.Models;

/// <summary>
/// Usage statistics for a media item across schedules (for the usage report).
/// </summary>
public class ItemUsage
{
    public int Count { get; set; }
    public string Name { get; set; } = "";
    public string[] Presentations { get; set; } = [];
}
