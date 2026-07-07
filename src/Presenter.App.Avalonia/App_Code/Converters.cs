using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Presenter.Core;

namespace Presenter.App_Code
{
    public class ScaleValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Util.Parse<double>(value) * Util.Parse<double>(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Red foreground for schedule items whose file is missing (replaces the WPF
    /// DataTrigger on Item.IsFound).
    /// </summary>
    public class FoundForegroundConverter : IValueConverter
    {
        public static readonly FoundForegroundConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is false ? Brushes.Red : AvaloniaProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
