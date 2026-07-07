using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Presenter.App_Code
{
    /// <summary>Avalonia-specific helpers (replaces WpfUtil).</summary>
    public static class AvaloniaUtil
    {
        public static T GetAncestorByType<T>(this Visual element) where T : Visual
        {
            if (element == null)
                return default;

            if (element is T t)
                return t;

            return GetAncestorByType<T>(element.GetVisualParent());
        }

        /// <summary>
        /// Gets the bound item for the element at the given point in the list box
        /// (port of Main.GetObjectDataFromPoint).
        /// </summary>
        public static object GetItemAtPoint(this ListBox source, Point point)
        {
            var hit = source.InputHitTest(point) as Visual;
            var container = hit.GetAncestorByType<ListBoxItem>();
            if (container == null)
                return null;
            int idx = source.IndexFromContainer(container);
            return idx < 0 ? null : source.Items[idx];
        }
    }
}
