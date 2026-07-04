using System.Windows.Media;
using Presenter.Core.Models;

namespace Presenter.App_Code
{
    /// <summary>WPF colour conversion for flags (was Flag.SystemColor in App_Code/Flag.cs).</summary>
    public static class FlagExtensions
    {
        public static Color SystemColor(this Flag flag)
        {
            return (Color)ColorConverter.ConvertFromString(flag.Colour);
        }
    }
}
