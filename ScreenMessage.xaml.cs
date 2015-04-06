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
using Presenter.Resources;
using System.Windows.Threading;

namespace Presenter
{
    public partial class ScreenMessage : Window
    {
        public ScreenMessage()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            for (double i = 20; i < 60; i += 2)
                FontSizeList.Items.Add(i.ToString());
            FontSizeList.SelectedValue = Config.MessengerFontSize.ToString();

            foreach (FontFamily fontFamily in Fonts.SystemFontFamilies)
                FontFamilyList.Items.Add(fontFamily.Source);
            FontFamilyList.SelectedValue = Config.MessengerFontFamily.Source;

            foreach (var color in typeof(Colors).GetProperties())
                FontColorList.Items.Add(color.Name);
            FontColorList.SelectedValue = Config.MessengerFontColourName;

            foreach (string align in Enum.GetNames(typeof(HorizontalAlignment)))
                if (align != "Stretch")
                    HorLocation.Items.Add(align);
            HorLocation.SelectedValue = Config.MessengerHorizontalPosition.ToString();

            foreach (string align in Enum.GetNames(typeof(VerticalAlignment)))
                if (align != "Stretch")
                    VerLocation.Items.Add(align);
            VerLocation.SelectedValue = Config.MessengerVerticalPosition.ToString();
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
            double size = Util.Parse<double>(FontSizeList.SelectedValue);
            FontFamily family = new FontFamily(FontFamilyList.SelectedValue.ToString());
            HorizontalAlignment posx = Util.Parse<HorizontalAlignment>(HorLocation.SelectedValue);
            VerticalAlignment posy = Util.Parse<VerticalAlignment>(VerLocation.SelectedValue);

            Config.SaveMessengerFont(size, family, (FontColorList.SelectedValue ?? "").ToString());
            Config.SaveMessengerLocation(posy, posx);

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
            MessageBox.MaxWidth = Config.ProjectorScreen.Bounds.Width;
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
                    MessageBox.Top = Config.ProjectorScreen.Bounds.Top + Config.MessengerMargin.Top;
                    break;
                case VerticalAlignment.Bottom:
                    MessageBox.Top = Config.ProjectorScreen.Bounds.Bottom - MessageBox.ActualHeight - Config.MessengerMargin.Bottom;
                    break;
                default:
                    MessageBox.Top = (Config.ProjectorScreen.Bounds.Height - MessageBox.ActualHeight) / 2 + Config.ProjectorScreen.Bounds.Top;
                    break;
            }

            switch (Config.MessengerHorizontalPosition)
            {
                case HorizontalAlignment.Left:
                    MessageBox.Left = Config.ProjectorScreen.Bounds.Left + Config.MessengerMargin.Left;
                    break;
                case HorizontalAlignment.Right:
                    MessageBox.Left = Config.ProjectorScreen.Bounds.Right - MessageBox.ActualWidth - Config.MessengerMargin.Right;
                    break;
                default:
                    MessageBox.Left = (Config.ProjectorScreen.Bounds.Width - MessageBox.ActualWidth) / 2 + Config.ProjectorScreen.Bounds.Left;
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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
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
