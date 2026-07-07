using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Presenter.App_Code;
using Presenter.Core;
using Presenter.Resources;

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
            FontSizeList.SelectedItem = Config.MessengerFontSize.ToString();

            foreach (FontFamily fontFamily in FontManager.Current.SystemFonts.OrderBy(f => f.Name))
                FontFamilyList.Items.Add(fontFamily.Name);
            FontFamilyList.SelectedItem = Config.MessengerFontFamily.Name;

            foreach (var color in typeof(Colors).GetProperties())
                FontColorList.Items.Add(color.Name);
            FontColorList.SelectedItem = Config.MessengerFontColourName;

            foreach (string align in Enum.GetNames(typeof(HorizontalAlignment)))
                if (align != "Stretch")
                    HorLocation.Items.Add(align);
            HorLocation.SelectedItem = Config.MessengerHorizontalPosition.ToString();

            foreach (string align in Enum.GetNames(typeof(VerticalAlignment)))
                if (align != "Stretch")
                    VerLocation.Items.Add(align);
            VerLocation.SelectedItem = Config.MessengerVerticalPosition.ToString();

            Opened += (s, e) => MessageValue.Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Show_Click(ShowBtn, null);
        }

        private void Show_Click(object sender, RoutedEventArgs e)
        {
            double size = Util.Parse<double>(FontSizeList.SelectedItem);
            FontFamily family = new FontFamily(FontFamilyList.SelectedItem?.ToString() ?? "Arial");
            HorizontalAlignment posx = Util.Parse<HorizontalAlignment>(HorLocation.SelectedItem);
            VerticalAlignment posy = Util.Parse<VerticalAlignment>(VerLocation.SelectedItem);

            Config.SaveMessengerFont(size, family, (FontColorList.SelectedItem ?? "").ToString());
            Config.SaveMessengerLocation(posy, posx);

            if (string.IsNullOrEmpty(MessageValue.Text) && !(TimerEnabled.IsChecked ?? false))
            {
                this.Close();
                return;
            }

            StackPanel panel = new StackPanel();
            string initMessage = (MessageValue.Text ?? "").Trim();
            TextBlock messageLabel = new TextBlock() { Text = initMessage, Foreground = new SolidColorBrush(Config.MessengerFontColour), FontSize = Config.MessengerFontSize, FontFamily = Config.MessengerFontFamily, TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(messageLabel);
            MessageBox = new Window();
            MessageBox.Content = panel;
            MessageBox.MaxWidth = Config.ProjectorScreen.Bounds.Width;
            MessageBox.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            MessageBox.SystemDecorations = SystemDecorations.None;
            MessageBox.SizeToContent = SizeToContent.WidthAndHeight;
            MessageBox.Topmost = true;
            MessageBox.CanResize = false;
            MessageBox.ShowInTaskbar = false;

            //position once the size is known (needs a layout pass after Show)
            MessageBox.Opened += (s, args) => Dispatcher.UIThread.Post(() => PositionMessageBox(), DispatcherPriority.Background);
            MessageBox.Show();

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
                timer.Tick += (sen, args) =>
                {
                    elasped++;
                    messageLabel.Text = String.Format(initMessage, TimeSpan.FromSeconds(countUp ? elasped : endTime - elasped).FormatTimeSpan(false));
                    if (elasped >= endTime)
                        timer.Stop();
                    PositionMessageBox();
                };
                timer.Start();
                messageLabel.Text = String.Format(initMessage, TimeSpan.FromSeconds(countUp ? elasped : endTime - elasped).FormatTimeSpan(false));
            }

            this.Close();
        }

        /// <summary>
        /// Places the message window on the projector screen according to the
        /// configured alignment (window position is in physical pixels).
        /// </summary>
        private void PositionMessageBox()
        {
            if (MessageBox == null)
                return;

            var bounds = Config.ProjectorScreen.Bounds;
            double scale = MessageBox.RenderScaling;
            double width = MessageBox.Bounds.Width * scale;
            double height = MessageBox.Bounds.Height * scale;
            var margin = Config.MessengerMargin;

            double top = Config.MessengerVerticalPosition switch
            {
                VerticalAlignment.Top => bounds.Y + margin.Top,
                VerticalAlignment.Bottom => bounds.Y + bounds.Height - height - margin.Bottom,
                _ => (bounds.Height - height) / 2 + bounds.Y,
            };

            double left = Config.MessengerHorizontalPosition switch
            {
                HorizontalAlignment.Left => bounds.X + margin.Left,
                HorizontalAlignment.Right => bounds.X + bounds.Width - width - margin.Right,
                _ => (bounds.Width - width) / 2 + bounds.X,
            };

            MessageBox.Position = new PixelPoint((int)left, (int)top);
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
