using Avalonia.Media;
using Presenter.Core.Models;

namespace Presenter.App_Code
{
    /// <summary>Avalonia colour conversion for flags (was Flag.SystemColor in App_Code/Flag.cs).</summary>
    public static class FlagExtensions
    {
        public static Color SystemColor(this Flag flag)
        {
            return Color.TryParse(flag.Colour, out var c) ? c : Colors.Red;
        }
    }
}
