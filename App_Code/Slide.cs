using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PP = Microsoft.Office.Interop.PowerPoint;

namespace Presenter.App_Code
{
    public class Slide
    {
        public Slide(SlideType type, string filename)
        {
            Type = type;
            Filename = filename;

            if (type == SlideType.Image)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    Image = RetrieveImage(filename, Config.ProjectorScreen.WorkingArea.Width, Config.ProjectorScreen.WorkingArea.Height);
                    Preview = RetrieveImage(filename, 333, 250);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (type == SlideType.PowerPoint)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    string path = SlideShow.ExportToImage(PSlide, SlideIndex, "-preview", 333, 250);
                    if (path != "")
                        Preview = new BitmapImage(new Uri(path));
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private BitmapSource RetrieveImage(string filename, int width, int height)
        {
            var photo = BitmapDecoder.Create(new Uri(filename), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None).Frames[0];
            if (photo.Width < width && photo.Height < height)
                return photo;
            double scale = Math.Min(width / photo.Width, height / photo.Height);
            return BitmapFrame.Create(new TransformedBitmap(photo, new ScaleTransform(scale * 96 / photo.DpiX, scale * 96 / photo.DpiY, 0, 0)));
        }

        public string Text { get; set; }
        public string Comment { get; set; }
        public int? JumpIndex { get; set; }
        public SlideType Type { get; set; }
        public string Filename { get; set; }
        public Item ScheduleItem { get; set; }
        public PP.Presentation Presentation { get; set; }
        public BitmapSource Image { get; set; }
        public BitmapSource Preview { get; set; }

        public PP.Slide PSlide
        {
            get
            {
                try { return Presentation.Slides[ItemIndex]; }
                catch (Exception) { return null; }
            }
        }

        /// <summary>
        /// one based index of the slide position in Schedule
        /// </summary>
        public int SlideIndex { get; set; }

        /// <summary>
        /// one based index of slide in ScheduleItem
        /// </summary>
        public int ItemIndex { get; set; }
    }
}
