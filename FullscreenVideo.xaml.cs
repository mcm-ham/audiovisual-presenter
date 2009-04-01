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
using SongPresenter.App_Code;
using Screen = System.Windows.Forms.Screen;

namespace SongPresenter
{
    public partial class FullscreenVideo : Window
    {
        public FullscreenVideo(MediaPlayer player)
        {
            InitializeComponent();
            VideoDisplay.Player = player;

            Screen projector = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == Config.ProjectorScreen) ?? Screen.PrimaryScreen;
            this.Left = projector.WorkingArea.Left;
            this.Top = projector.WorkingArea.Top;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }

        protected void Close(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
