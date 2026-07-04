using System.Windows;
using System.Windows.Media;

namespace Presenter.App_Code
{
    /// <summary>WPF-specific helpers from the original App_Code/Util.cs.</summary>
    public static class WpfUtil
    {
        public static Point GetResolution(Visual visual)
        {
            Point dpi = new Point(96, 96);

            PresentationSource source = PresentationSource.FromVisual(visual);
            if (source == null)
                return dpi;

            MatrixTransform t = new MatrixTransform(source.CompositionTarget.TransformToDevice);

            Point pt1 = t.Transform(new Point(0, 0));
            Point pt2 = t.Transform(new Point(96, 96));

            dpi.X = pt2.X - pt1.X;
            dpi.Y = pt2.Y - pt1.Y;
            return dpi;
        }

        public static T GetAncestorByType<T>(this DependencyObject element) where T : DependencyObject
        {
            if (element == null)
                return default;

            if (element is T t)
                return t;

            return GetAncestorByType<T>(VisualTreeHelper.GetParent(element));
        }
    }
}
