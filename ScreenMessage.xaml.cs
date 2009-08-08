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
using SongPresenter.Resources;
using System.Windows.Threading;

namespace SongPresenter
{
    public partial class ScreenMessage : Window
    {
        public ScreenMessage()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MessageValue.Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.IsKeyDown(Key.Enter))
                Show_Click(ShowBtn, null);
        }

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            if (MessageValue.Text == "" && !(TimerEnabled.IsChecked ?? false))
            {
                this.Close();
                return;
            }

            
            StackPanel panel = new StackPanel();
            string initMessage = MessageValue.Text.Trim();
            TextBlock messageLabel = new TextBlock() { Text = initMessage, Foreground = new SolidColorBrush(Config.MessengerFontColour), FontSize = Config.MessengerFontSize, FontFamily = Config.MessengerFontFamily, TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(messageLabel);
            MessageBox = new Window();
            MessageBox.Content = panel;
            MessageBox.MaxWidth = Config.ProjectorScreen.WorkingArea.Width;
            MessageBox.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            MessageBox.WindowStyle = WindowStyle.None;
            MessageBox.SizeToContent = SizeToContent.WidthAndHeight;
            MessageBox.Topmost = true;
            MessageBox.ResizeMode = ResizeMode.NoResize;
            MessageBox.ShowInTaskbar = false;
            MessageBox.Show();
            
            this.Focus(); //return focus to main program and not to message box

            switch (Config.MessengerVerticalPosition)
            {
                case VerticalAlignment.Top:
                    MessageBox.Top = Config.ProjectorScreen.WorkingArea.Top;
                    break;
                case VerticalAlignment.Bottom:
                    MessageBox.Top = Config.ProjectorScreen.WorkingArea.Bottom - MessageBox.ActualHeight;
                    break;
                default:
                    MessageBox.Top = (Config.ProjectorScreen.WorkingArea.Height - MessageBox.ActualHeight) / 2 + Config.ProjectorScreen.WorkingArea.Top;
                    break;
            }

            switch (Config.MessengerHorizontalPosition)
            {
                case HorizontalAlignment.Left:
                    MessageBox.Left = Config.ProjectorScreen.WorkingArea.Left;
                    break;
                case HorizontalAlignment.Right:
                    MessageBox.Left = Config.ProjectorScreen.WorkingArea.Right - MessageBox.ActualWidth;
                    break;
                default:
                    MessageBox.Left = (Config.ProjectorScreen.WorkingArea.Width - MessageBox.ActualWidth) / 2 + Config.ProjectorScreen.WorkingArea.Left;
                    break;
            }

            Point dpi = Util.GetResolution(MessageBox);
            MessageBox.Top /= (dpi.Y / 96);
            MessageBox.Left /= (dpi.X / 96);

            //add timer
            if (TimerEnabled.IsChecked ?? false)
            {
                int elasped = 0;
                int endTime = TimeValue.Text.Replace('.', ':').Contains(':') ? (int)Util.Parse<TimeSpan>(TimeValue.Text.Replace('.', ':')).TotalSeconds : Util.Parse<int>(TimeValue.Text);
                bool countUp = (TimerType.SelectedIndex == 1);
                if (!initMessage.Contains("{0}"))
                    initMessage += " {0}";
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (sen, args) => {
                    elasped++;
                    messageLabel.Text = String.Format(initMessage, TimeSpan.FromSeconds(countUp ? elasped : endTime - elasped).FormatTimeSpan(false));
                    if (elasped >= endTime)
                        timer.Stop();
                    MessageBox.UpdateLayout();
                };
                timer.Start();
                messageLabel.Text = String.Format(initMessage, TimeSpan.FromSeconds(countUp ? elasped : endTime - elasped).FormatTimeSpan(false));
            }

            this.Close();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimeDesc != null)
                TimeDesc.Text = (TimerType.SelectedIndex == 0) ? Labels.ShowMessageAddTimerMiddle1 : Labels.ShowMessageAddTimerMiddle2;
        }

        private void TimerEnabled_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = (TimerEnabled.IsChecked ?? false);
            TimerType.IsEnabled = enabled;
            TimeValue.IsEnabled = enabled;
        }

        public Window MessageBox { get; set; }
    }
}
