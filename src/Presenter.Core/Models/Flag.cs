namespace Presenter.Core.Models;

/// <summary>
/// A colour-coded flag/marker attached to an item. Colour is stored as a colour name
/// string; conversion to a UI colour type happens in the presentation layer.
/// </summary>
public class Flag
{
    public Guid ItemID { get; set; }
    public short Index { get; set; }
    public string Colour { get; set; } = "";

    public Item? Item { get; set; }
}
