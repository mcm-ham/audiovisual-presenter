using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

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
            for (int i = 0; i < source.Count(); i++)
                if (predicate.Invoke(source.ElementAt(i)))
                    return i;
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
    }
}
