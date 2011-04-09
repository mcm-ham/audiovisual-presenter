using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PP = Microsoft.Office.Interop.PowerPoint;
using Core = Microsoft.Office.Core;

namespace Presenter.App_Code
{
    public class Slide
    {
        public Slide(SlideType type, string filename)
        {
            Type = type;
            Filename = filename;

            /*if (type == SlideType.Image)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    Image = RetrieveImage(filename, Config.ProjectorScreen.WorkingArea.Width, Config.ProjectorScreen.WorkingArea.Height);
                    Preview = RetrieveImage(filename, 333, 250);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (type == SlideType.PowerPoint)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    string path = SlideShow.ExportToImage(this, SlideIndex, "-preview", 333, 250);
                    if (path != "")
                        Preview = new BitmapImage(new Uri(path));
                }), System.Windows.Threading.DispatcherPriority.Background);
            }*/
        }

        /*private BitmapSource RetrieveImage(string filename, int width, int height)
        {
            var photo = BitmapDecoder.Create(new Uri(filename), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None).Frames[0];
            if (photo.Width < width && photo.Height < height)
                return photo;
            double scale = Math.Min(width / photo.Width, height / photo.Height);
            return BitmapFrame.Create(new TransformedBitmap(photo, new ScaleTransform(scale * 96 / photo.DpiX, scale * 96 / photo.DpiY, 0, 0)));
        }*/

        public string Text { get; set; }
        public string Comment { get; set; }
        public int? JumpIndex { get; set; }
        public SlideType Type { get; set; }
        public string Filename { get; set; }
        public Item ScheduleItem { get; set; }

        /// <summary>
        /// Contains the running presentation of the slide through PowerPoint PIA
        /// </summary>
        public PP.Presentation Presentation { get; set; }

        /// <summary>
        /// Contains the process that is running the PowerPoint slide under PPTView
        /// </summary>
        public System.Diagnostics.Process Process { get; set; }

        /// <summary>
        /// If the slide is of Type Image then this property contains the image to be displayed, otherwise will be null
        /// </summary>
        public BitmapSource Image { get; set; }

        /// <summary>
        /// Contains an image preview of what the slide will look like
        /// </summary>
        public BitmapSource Preview { get; set; }

        /// <summary>
        /// The total number of clicks that trigger animation on the slide
        /// </summary>
        public int AnimationCount { get; set; }

        /// <summary>
        /// The total number of animations that have been triggered so far on the slide through clicking
        /// </summary>
        public int CurrentAnimationCount { get; set; }

        public PP.Slide PSlide
        {
            get
            {
                try { return Presentation.Slides[ItemIndex]; }
                catch (Exception) { return null; }
            }
        }

        /// <summary>
        /// Used to store the original setting for powerpoint slide's AdvanceOnTime, to allow toggling of UseSlideTimings during slideshow
        /// </summary>
        public Core.MsoTriState AdvanceOnTime { get; set; }

        /// <summary>
        /// one based index of the slide position in Schedule
        /// </summary>
        public int SlideIndex { get; set; }

        /// <summary>
        /// one based index of slide in ScheduleItem
        /// </summary>
        public int ItemIndex { get; set; }

        public void SendKeys(string keys, bool forImg = false)
        {
            if (!forImg)
                SetTop();

            IntPtr window, parent;

            if (forImg)
            {
                parent = Process.MainWindowHandle;
                IntPtr child1 = User32.FindWindowEx(parent, IntPtr.Zero, "MDIClient", null);
                IntPtr child2 = User32.FindWindowEx(child1, IntPtr.Zero, "mdiClass", null);
                window = User32.FindWindowEx(child2, IntPtr.Zero, "paneClassDC", null);
            }
            else
            {
                parent = User32.FindWindow(Process.Id, null, System.IO.Path.GetFileNameWithoutExtension(ScheduleItem.Filename));
                window = User32.FindWindowEx(parent, IntPtr.Zero, /*"paneClassDC"*/ null, "Slide Show");
            }

            if (window != IntPtr.Zero)
            {
                User32.SendMessage(parent, User32.WM_SETFOCUS, IntPtr.Zero, UIntPtr.Zero);
                foreach (char k in keys.ToCharArray())
                {
                    if (k == '\n')
                    {
                        IntPtr nVirtKey = new IntPtr(User32.VK_RETURN);
                        User32.PostMessage(window, User32.WM_KEYDOWN, nVirtKey, new UIntPtr(0x1C0001));
                        System.Threading.Thread.Sleep(100);
                        User32.PostMessage(window, User32.WM_KEYUP, nVirtKey, new UIntPtr(0xC01C0001));
                    }
                    else
                    {
                        IntPtr nVirtKey = new IntPtr((uint)k);
                        User32.PostMessage(window, User32.WM_KEYDOWN, nVirtKey, new UIntPtr(0x4D0001));
                        System.Threading.Thread.Sleep(100);
                        User32.PostMessage(window, User32.WM_KEYUP, nVirtKey, new UIntPtr(0xC04D0001));
                    }
                }
            }
            else
            {
                User32.SetForegroundWindow(Process.MainWindowHandle);
                System.Windows.Forms.SendKeys.SendWait(keys);
                System.Threading.Thread.Sleep(100);
                User32.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle);
            }
        }

        public void SetTop()
        {
            User32.SetWindowPos(Presentation.SlideShowWindow().HWND, User32.HWND_TOP, Config.ProjectorScreen.WorkingArea.Left, Config.ProjectorScreen.WorkingArea.Top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
        }
    }
}
