using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LibVLCSharp.Shared;
using Presenter.App_Code;

namespace Presenter
{
    /// <summary>
    /// Borderless output window covering the projector screen: shows a blank colour,
    /// an image slide, or the libvlc video output (WPF showed the shared MediaPlayer
    /// through a VideoDrawing; here the LibVLCSharp VideoView hosts the picture).
    /// </summary>
    public partial class FullscreenWindow : Window
    {
        public IntPtr HWND => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

        public FullscreenWindow()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.ScreenBlankColour);

            var screen = Config.ProjectorScreen;
            if (screen != null)
            {
                Position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
                Width = screen.Bounds.Width / screen.Scaling;
                Height = screen.Bounds.Height / screen.Scaling;
            }

            Topmost = true;
            Show();
        }

        public void ShowVideo(MediaPlayer player)
        {
            if (VideoDisplay.MediaPlayer != player)
                VideoDisplay.MediaPlayer = player;
            VideoDisplay.IsVisible = true;
            ImageDisplay.IsVisible = false;
            ShowWindow();
        }

        public void Show(Bitmap image)
        {
            var screen = Config.ProjectorScreen;
            //show at natural size when the image is smaller than the screen, otherwise fit
            if (screen != null && screen.Bounds.Width > image.PixelSize.Width && screen.Bounds.Height > image.PixelSize.Height)
                ImageDisplay.Width = image.PixelSize.Width / screen.Scaling;
            else
                ImageDisplay.Width = Width;

            ImageDisplay.Source = image;
            ImageDisplay.IsVisible = true;
            VideoDisplay.IsVisible = false;
            ShowWindow();
        }

        public void ShowBlank()
        {
            ImageDisplay.IsVisible = false;
            VideoDisplay.IsVisible = false;
            ShowWindow();
        }

        public void ShowWindow()
        {
            this.Show();
        }

        public void HideWindow()
        {
            this.Hide();
        }

        protected void Close_Click(object sender, RoutedEventArgs e)
        {
            HideWindow();
        }
    }
}
