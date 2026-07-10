using System.IO;
using Avalonia.Media.Imaging;
using Presenter.Core.Abstractions;

namespace Presenter.App_Code
{
    /// <summary>Avalonia image loading for engines that cannot reference Avalonia themselves.</summary>
    public class AvaloniaSlideImageLoader : ISlideImageLoader
    {
        public object Load(string filename, int maxWidth, int maxHeight)
        {
            using var stream = File.OpenRead(filename);
            var bitmap = new Bitmap(stream);

            double scale = Math.Min(maxWidth / (double)bitmap.PixelSize.Width, maxHeight / (double)bitmap.PixelSize.Height);
            if (scale >= 1)
                return bitmap;

            var scaled = bitmap.CreateScaledBitmap(new Avalonia.PixelSize(
                Math.Max(1, (int)(bitmap.PixelSize.Width * scale)),
                Math.Max(1, (int)(bitmap.PixelSize.Height * scale))));
            bitmap.Dispose();
            return scaled;
        }
    }
}
