﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

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
    }
}
