using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Presenter.App_Code;
using Screen = System.Windows.Forms.Screen;

namespace Presenter
{
    public partial class FullscreenWindow : Window
    {
        public int HWND
        {
            get { return new System.Windows.Interop.WindowInteropHelper(this).Handle.ToInt32(); }
        }

        public FullscreenWindow()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.ScreenBlankColour);
            Left = Config.ProjectorScreen.WorkingArea.Left;
            Top = Config.ProjectorScreen.WorkingArea.Top;
            Topmost = true;
            Show();
        }

        public void Show(MediaPlayer player)
        {
            VideoDisplay.Player = player;
            VideoPanel.Visibility = Visibility.Visible;
            ImageDisplay.Visibility = Visibility.Collapsed;
            ShowWindow();
        }

        public void Show(BitmapSource image)
        {
            if (Config.ProjectorScreen.WorkingArea.Width > image.PixelWidth && Config.ProjectorScreen.WorkingArea.Height > image.PixelHeight)
                ImageDisplay.Width = image.PixelWidth;
            else
                ImageDisplay.Width = Config.ProjectorScreen.WorkingArea.Width;

            ImageDisplay.Source = image;
            ImageDisplay.Visibility = Visibility.Visible;
            VideoPanel.Visibility = Visibility.Collapsed;
            ShowWindow();
        }

        public void ShowBlank()
        {
            ImageDisplay.Visibility = Visibility.Collapsed;
            VideoPanel.Visibility = Visibility.Collapsed;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }

        protected void Close(object sender, EventArgs e)
        {
            HideWindow();
        }
    }
}
