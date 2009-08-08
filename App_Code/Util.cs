using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows;
using Point = System.Windows.Point;

namespace SongPresenter.App_Code
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

        public static Image ToImage(this byte[] data)
        {
            MemoryStream stream = new MemoryStream(data.Length);
            stream.Write(data, 0, data.Length);
            stream.Seek(0L, SeekOrigin.Begin);
            return Image.FromStream(stream);
        }

        public static byte[] ToByteArray(this Image img)
        {
            MemoryStream resized = new MemoryStream();
            img.Save(resized, ImageFormat.Jpeg);
            return resized.ToArray();
        }

        public static Image Resize(this Image image, int? width, int? height, System.Drawing.Color? background)
        {
            if (width == null && height == null || width == 0 && height == 0)
                return image;

            int w = (width == null || width == 0) ? image.Width : width.Value;
            int h = (height == null || height == 0) ? image.Height : height.Value;
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

            if (!background.HasValue)
            {
                w = (int)(image.Width * scale);
                h = (int)(image.Height * scale);
                posx = 0f;
                posy = 0f;
            }

            Image resizedImage = new Bitmap(w, h);
            Graphics g = Graphics.FromImage(resizedImage);
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (background.HasValue)
                g.FillRectangle(new SolidBrush(background.Value), 0, 0, w, h);

            g.DrawImage(image, posx, posy, image.Width * scale, image.Height * scale);

            foreach (PropertyItem item in image.PropertyItems)
                resizedImage.SetPropertyItem(item);

            return resizedImage;
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

            if (span.Hours > 0)
                return sign + ((int)span.TotalHours).ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");

            if (span.Minutes > 0)
                return sign + span.Minutes.ToString() + ":" + span.Seconds.ToString("00");

            return sign + span.Seconds.ToString();
        }
    }
}
