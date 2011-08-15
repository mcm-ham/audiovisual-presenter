using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using Point = System.Windows.Point;
using PP = Microsoft.Office.Interop.PowerPoint;

namespace Presenter.App_Code
{
    public static class Util
    {
        public static T Parse<T>(this object value)
        {
            try { return (T)System.ComponentModel.TypeDescriptor.GetConverter(typeof(T)).ConvertFrom(value.ToString()); }
            catch (Exception) { return default(T); }
        }

        public static void ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
        {
            foreach (var elm in source)
                action.Invoke(elm);
        }

        /// <summary>
        /// Makes the first letter of the word upper case and the rest of the letters lower case.
        /// </summary>
        public static string ToFirstUpper(this string value)
        {
            if (String.IsNullOrEmpty(value))
                return String.Empty;

            value = value.Trim();
            StringBuilder res = new StringBuilder(char.ToUpper(value[0]).ToString());

            for (int i = 1; i < value.Length; i++)
            {
                if (value[i] == ' ')
                    res.Append(" " + char.ToUpper(value[++i]));
                else
                    res.Append(char.ToLower(value[i]));
            }

            return res.ToString();
        }

        public static int FindIndex<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            int idx = 0;
            foreach (var item in source)
            {
                if (predicate(item))
                    return idx;
                idx++;
            }
            return -1;
        }

        public static Point GetResolution(Visual visual)
        {
            Point dpi = new Point(96, 96);

            PresentationSource source = PresentationSource.FromVisual(visual);
            if (source == null)
                return dpi;

            MatrixTransform t = new MatrixTransform(source.CompositionTarget.TransformToDevice);

            Point pt1 = new Point(0, 0);
            pt1 = t.Transform(pt1);

            Point pt2 = new Point(96, 96);
            pt2 = t.Transform(pt2);

            dpi.X = pt2.X - pt1.X;
            dpi.Y = pt2.Y - pt1.Y;
            return dpi;
        }

        public static string FormatTimeSpan(this TimeSpan span, bool showSign)
        {
            string sign = String.Empty;
            if (showSign && (span > TimeSpan.Zero))
                sign = "+";

            if ((int)span.TotalHours > 0)
                return sign + ((int)span.TotalHours).ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");

            if (span.Minutes > 0)
                return sign + span.Minutes.ToString() + ":" + span.Seconds.ToString("00");

            return sign + span.Seconds.ToString();
        }

        public static T GetAncestorByType<T>(this DependencyObject element) where T : DependencyObject
        {
            if (element == null)
                return default(T);

            if (element is T)
                return (T)element;

            return GetAncestorByType<T>(VisualTreeHelper.GetParent(element));
        }

        
        public static Image Resize(this Image image, int? width, int? height, int trim = 0)
        {
            if (width == null && height == null)
                return image;

            int w = (width == null) ? image.Width : width.Value + trim;
            int h = (height == null) ? image.Height : height.Value + trim;
            float desiredRatio = (float)w / h;
            float scale, posx, posy;
            float ratio = (float)image.Width / image.Height;

            if (image.Width < w && image.Height < h)
            {
                scale = 1f;
                posy = (h - image.Height) / 2f;
                posx = (w - image.Width) / 2f;
            }
            else if (ratio > desiredRatio)
            {
                scale = (float)w / image.Width;
                posy = (h - (image.Height * scale)) / 2f;
                posx = 0f;
            }
            else
            {
                scale = (float)h / image.Height;
                posx = (w - (image.Width * scale)) / 2f;
                posy = 0f;
            }

            Image resizedImage = new Bitmap(w - trim, h - trim);
            Graphics g = Graphics.FromImage(resizedImage);
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            g.DrawImage(image, posx - trim / 2, posy - trim / 2, image.Width * scale, image.Height * scale);

            return resizedImage;
        }
        
        public static Dictionary<PP.Presentation, PP.SlideShowWindow> SlideShowWindows = new Dictionary<PP.Presentation, PP.SlideShowWindow>();
        /// <summary>
        /// Workaround for PowerPoint 2010 bug where property returns the last slideshowwindow instead of the slideshowwindow belonging to specified presentation.
        /// </summary>
        public static PP.SlideShowWindow SlideShowWindow(this PP.Presentation key)
        {
            PP.SlideShowWindow wnd;
            if (SlideShowWindows.TryGetValue(key, out wnd))
                return wnd;
            return key.SlideShowWindow;
        }

        const int RedShift = 0;
        const int GreenShift = 8;
        const int BlueShift = 16;
        /// <summary>
        /// Translates an Ole color value to a System.Media.Color for WPF usage
        /// </summary>
        /// <param name="oleColor">Ole int32 color value</param>
        /// <returns>System.Media.Color color value</returns>
        public static System.Windows.Media.Color FromOle(this int oleColor)
        {
            return System.Windows.Media.Color.FromRgb(
                (byte)((oleColor >> RedShift) & 0xFF),
                (byte)((oleColor >> GreenShift) & 0xFF),
                (byte)((oleColor >> BlueShift) & 0xFF)
                );
        }

        /// <summary>
        /// Translates the specified System.Media.Color to an Ole color.
        /// </summary>
        /// <param name="wpfColor">System.Media.Color source value</param>
        /// <returns>Ole int32 color value</returns>
        public static int ToOle(System.Windows.Media.Color wpfColor)
        {
            return wpfColor.R << RedShift | wpfColor.G << GreenShift | wpfColor.B << BlueShift;
        } 
    }
}
