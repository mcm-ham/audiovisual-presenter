using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Presenter.App_Code;
using Presenter.Resources;

//icon from http://www.iconspedia.com/search/projector/
namespace Presenter
{
    public partial class Main : Window
    {
        DispatcherTimer timer = new DispatcherTimer();

        public Main()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);
            
            ScheduleList.IsEnabled = false;
            BindLocationList();
            timer.Tick += new EventHandler(timer_Tick);
        }

        #region menu
        protected void Planner_Click(object sender, RoutedEventArgs e)
        {
            OpenDialog dialog = new OpenDialog((sender as MenuItem).Name == "MenuItemNew");
            dialog.Owner = this;
            dialog.ShowDialog();

            if (dialog.SelectedSchedule != null)
            {
                SelectedSchedule = dialog.SelectedSchedule;
                ScheduleName.Text = SelectedSchedule.DisplayName;
                ScheduleList.IsEnabled = true;
                BindScheduleList();

                if (Presentation != null)
                    Presentation.Stop();
                SlideShow.RemoveOldPres();
            }
        }

        protected void Options_Click(object sender, RoutedEventArgs e)
        {
            OptionsDialog dialog = new OptionsDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        protected void About_Click(object sender, RoutedEventArgs e)
        {
            AboutDialog dialog = new AboutDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }
        #endregion

        #region library
        public void BindLocationList()
        {
            LocationList.SelectedIndex = 0;

            if (Directory.Exists(Config.LibraryPath))
                LocationList.ItemsSource = Directory.GetDirectories(Config.LibraryPath).Select(p => p.Substring(p.LastIndexOf('\\') + 1));
            else
                LocationList.ItemsSource = new string[] { };

            BindFileList();
        }

        protected void BindFileList()
        {
            if (!Directory.Exists(Config.LibraryPath + LocationList.SelectedValue))
            {
                FileList.ItemsSource = new string[] { };
                return;
            }

            FileList.ItemsSource = from file in Directory.GetFiles(Config.LibraryPath + LocationList.SelectedValue, "*" + SearchTerms.Text.Replace(" ", "*") + "*")
                                   where Config.SupportedFileTypes.Any(t => file.ToLower().EndsWith("." + t))
                                   select file.Substring(file.LastIndexOf('\\') + 1);
        }

        protected void SearchTerms_TextChanged(object sender, TextChangedEventArgs e)
        {
            BindFileList();
        }

        protected void LocationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BindFileList();
        }

        protected void FileSelected()
        {
            if (SelectedSchedule == null)
            {
                Planner_Click(MenuItemNew, null);
                if (SelectedSchedule == null)
                    return;
            }

            bool running = (Presentation != null && Presentation.IsRunning);
            SelectedSchedule.AddItem(Config.LibraryPath + LocationList.SelectedValue + "\\" + FileList.SelectedValue, !running);
            BindScheduleList();

            if (running)
            {
                Presentation.AddSlides(SelectedSchedule.Items.Last());
            }
        }

        protected void OpenFile2(object sender, RoutedEventArgs e)
        {
            if (LocationList.SelectedIndex == -1)
                return;

            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = Config.LibraryPath + LocationList.SelectedValue + "\\" + FileList.SelectedValue;
            p.Start();
        }

        protected void FileList_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                FileSelected();
        }

        protected void AddSelected(object sender, EventArgs e)
        {
            FileSelected();
        }

        protected void RefreshLocations(object sender, EventArgs e)
        {
            object value = LocationList.SelectedValue;
            BindLocationList();
            try { LocationList.SelectedValue = value; }
            catch (Exception) { }
            BindFileList();
        }
        #endregion

        #region dragdrop
        bool _dragging = false;
        private void DropDrop_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_dragging)
            {
                _dragging = true;
                ListBox parent = (ListBox)sender;
                object data = GetObjectDataFromPoint(parent, e.GetPosition(parent));
                if (data != null && ScheduleList.IsEnabled)
                    DragDrop.DoDragDrop(parent, data, DragDropEffects.Move);
            }
            else if (e.LeftButton == MouseButtonState.Released && _dragging)
                _dragging = false;
        }

        private void DropDrop_DropHandler(object sender, DragEventArgs e)
        {
            ListBox parent = (ListBox)sender;
            int added = 0;

            if (parent.Name == "ScheduleList" || parent.Name == "LiveList")
            {
                string data = (string)e.Data.GetData(typeof(string));
                if (!String.IsNullOrEmpty(data)) //adding new
                {
                    SelectedSchedule.AddItem(Config.LibraryPath + LocationList.SelectedValue + "\\" + data);
                    added++;
                }
                else if (e.Data.GetFormats().Contains("FileDrop")) //add new from explorer
                {
                    string[] files = (string[])e.Data.GetData("FileDrop");

                    if (files.Length == 1 && Directory.Exists(files[0]))
                        files = Directory.GetFiles(files[0]);

                    foreach (string file in files)
                    {
                        SelectedSchedule.AddItem(file);
                        added++;
                    }
                }
                else if (parent.Name != "LiveList") //reordering
                {
                    Item source = (Item)e.Data.GetData(typeof(Item));
                    Item dest = GetObjectDataFromPoint(parent, e.GetPosition(parent)) as Item;
                    if (source != null)
                        SelectedSchedule.ReOrder(source, dest);
                }

                if (parent.Name == "LiveList")
                    SelectedSchedule.Items.Skip(SelectedSchedule.Items.Count - added).ForEach(i => Presentation.AddSlides(i));
            }
            else //removing
            {
                Item data = (Item)e.Data.GetData(typeof(Item));
                if (data != null)
                    SelectedSchedule.RemoveItem(data);
            }
            BindScheduleList();
        }

        //gets the object for the element selected (from the point) in the listbox (source)
        private static object GetObjectDataFromPoint(ListBox source, Point point)
        {
            UIElement element = source.InputHitTest(point) as UIElement;

            while (element != source && element != null)
            {
                if (element is ListBoxItem)
                    return source.ItemContainerGenerator.ItemFromContainer(element);

                element = VisualTreeHelper.GetParent(element) as UIElement;
            }

            return null;
        }
        #endregion

        #region order_of_worship
        protected void BindScheduleList()
        {
            ScheduleList.ItemsSource = SelectedSchedule.Items.OrderBy(i => i.Ordinal);
        }

        protected void OpenFile(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedIndex == -1)
                return;

            //check that file exists and if not show friendly message box
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = ((Item)ScheduleList.SelectedItem).Filename;
            p.Start();
        }

        protected void RemoveFile(object sender, RoutedEventArgs e)
        {
            int idx = ScheduleList.SelectedIndex;
            foreach (Item item in ScheduleList.SelectedItems)
                SelectedSchedule.RemoveItem(item);
            BindScheduleList();
            idx = Math.Min(idx, ScheduleList.Items.Count - 1);
            ScheduleList.SelectedIndex = idx;
        }

        private void ScheduleList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                OpenFile(null, null);
        }

        protected void ScheduleList_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && ScheduleList.SelectedItem != null)
                RemoveFile(null, null);
        }

        private void Up_Click(object sender, EventArgs e)
        {
            if (Presentation != null && Presentation.IsRunning)
            {
                Presentation.Previous();
                return;
            }

            Item item = ScheduleList.SelectedItem as Item;
            if (item == null || item.Ordinal == 0)
                return;

            item.Ordinal--;
            (ScheduleList.Items.GetItemAt(item.Ordinal) as Item).Ordinal++;
            SelectedSchedule.Save();

            BindScheduleList();
        }

        private void Down_Click(object sender, EventArgs e)
        {
            if (Presentation != null && Presentation.IsRunning)
            {
                Presentation.Next();
                return;
            }

            Item item = ScheduleList.SelectedItem as Item;
            if (item == null || item.Ordinal == ScheduleList.Items.Count - 1)
                return;

            item.Ordinal++;
            (ScheduleList.Items.GetItemAt(item.Ordinal) as Item).Ordinal--;
            SelectedSchedule.Save();

            BindScheduleList();
        }

        private void ScheduleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool enabled = (ScheduleList.SelectedIndex != -1);
            PrevBtn.IsEnabled = enabled;
            NextBtn.IsEnabled = enabled;
        }
        #endregion

        #region session
        protected void Start_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSchedule == null)
            {
                Planner_Click(MenuItemNew, null);
                if (SelectedSchedule == null)
                    return;
            }

            if (SelectedSchedule.Items.Count == 0)
            {
                MessageBox.Show(Labels.MainNoAddedItems, "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StartBtn.Visibility = Visibility.Hidden;
            StopBtn.Visibility = Visibility.Visible;
            Interval.Visibility = Visibility.Visible;
            TimerBtn.Visibility = Visibility.Visible;
            Expander1.Visibility = Visibility.Visible;
            ScheduleList.SetValue(ListBox.VisibilityProperty, Visibility.Hidden);
            LiveList.SetValue(ListView.VisibilityProperty, Visibility.Visible);
            LiveList.SelectedIndex = -1;
            PrevBtn.Content = Labels.MainBtnPrev;
            NextBtn.Content = Labels.MainBtnNext;
            RefreshBtn.Visibility = Visibility.Hidden;
            LocationList.Margin = new Thickness(81, 62, 17, 0);

            double widthIncrease = col1.ActualWidth - 220;
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(220, GridUnitType.Pixel));
            ((GridView)LiveList.View).Columns[0].Width = 35;
            ((GridView)LiveList.View).Columns[1].Width = (LiveList.ActualWidth + widthIncrease - 80) * 0.77;
            ((GridView)LiveList.View).Columns[2].Width = (LiveList.ActualWidth + widthIncrease - 80) * 0.18;
            ((GridView)LiveList.View).Columns[3].Width = 50;

            if (Presentation == null)
            {
                Presentation = new SlideShow();
                Presentation.SlideIndexChanged += new EventHandler<SlideShowEventArgs>(Presentation_SlideIndexChanged);
                Presentation.SlideShowEnd += new EventHandler(Presentation_SlideShowEnd);
                Presentation.SlideAdded += new EventHandler<SlideAddedEventArgs>(Presentation_SlideAdded);
            }

            Action prepare = new Action(() => Presentation.Start(SelectedSchedule.Items.OrderBy(i => i.Ordinal)));
            prepare.BeginInvoke(null, null);
        }

        protected void Stop_Click(object sender, RoutedEventArgs e)
        {
            StartBtn.Visibility = Visibility.Visible;
            StopBtn.Visibility = Visibility.Hidden;
            Interval.Visibility = Visibility.Hidden;
            TimerBtn.Visibility = Visibility.Hidden;
            Expander1.Visibility = Visibility.Hidden;
            LibraryGrid.Visibility = Visibility.Visible;
            Expander1.Content = "<";
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength((this.ActualWidth - 20) / 2, GridUnitType.Pixel));
            ScheduleList.SetValue(ListBox.VisibilityProperty, Visibility.Visible);
            LiveList.SetValue(ListView.VisibilityProperty, Visibility.Hidden);
            PrevBtn.Content = Labels.MainBtnMoveUp;
            NextBtn.Content = Labels.MainBtnMoveDown;
            RefreshBtn.Visibility = Visibility.Visible;
            LocationList.Margin = new Thickness(81, 62, 80, 0);
            HideMedia();
            if (Presentation != null)
                Presentation.Stop();
        }

        void Presentation_SlideAdded(object sender, SlideAddedEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => { if (e.NewSlide != null) LiveList.Items.Add(e.NewSlide); else LiveList.Items.Clear(); }));
        }

        protected void Presentation_SlideShowEnd(object sender, EventArgs e)
        {
            if (Presentation != null && Presentation.IsRunning)
                Dispatcher.Invoke(new Action(() => Stop_Click(null, null)));
        }

        protected void Presentation_SlideIndexChanged(object sender, SlideShowEventArgs e)
        {
            int idx = e.NewIndex - 1;
            
            if (e.OldIndex != e.NewIndex)
                LiveList.SelectedIndex = idx;

            if (Presentation.Slides.Length > idx && idx >= 0 && Presentation.Slides[idx].Type != SlideType.PowerPoint)
                ShowMedia(Presentation.Slides[idx].Filename);
            else
                HideMedia();
        }

        protected void TimerStart_Click(object sender, RoutedEventArgs e)
        {
            if (TimerBtn.Content.ToString() != "Timer End")
            {
                TimerBtn.Content = "Timer End";
                int interval = Util.Parse<int?>(Interval.Text) ?? 8;
                timer.Interval = TimeSpan.FromSeconds(interval);
                timer.Start();
            }
            else
            {
                TimerBtn.Content = "Timer";
                timer.Stop();
            }
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if (Presentation == null || !Presentation.IsRunning)
                return;

            int? jump = Presentation.Slides[LiveList.SelectedIndex].JumpIndex;
            if (jump.HasValue && jump > 0 && jump <= Presentation.Slides.Length)
                LiveList.SelectedIndex = jump.Value - 1;
            else if (LiveList.Items.Count > LiveList.SelectedIndex)
                LiveList.SelectedIndex++;
        }

        private void Interval_TextChanged(object sender, TextChangedEventArgs e)
        {
            int interval = Util.Parse<int?>(Interval.Text) ?? 8;
            timer.Interval = TimeSpan.FromSeconds(interval);
        }

        protected void LiveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //The timer is necessary to allow the selection event to complete first, otherwise if changing slide
            //takes a while (e.g. animation delay) and the mouse is moved it will automatically select
            //new index even though the mouse wasn't clicked.
            Timer tim = new Timer((sen) =>
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    Presentation.GoTo(LiveList.SelectedItem as Slide);
                    Presentation_SlideIndexChanged(Presentation, new SlideShowEventArgs(LiveList.SelectedIndex + 1, LiveList.SelectedIndex + 1));
                }));
            }, null, 100, Timeout.Infinite);
        }

        private void Expander1_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            int newWidth;

            if (btn.Content.ToString() == "<")
            {
                newWidth = 20;
                btn.Content = ">";
                LibraryGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                newWidth = 220;
                btn.Content = "<";
                LibraryGrid.Visibility = Visibility.Visible;
            }

            double widthIncrease = col1.ActualWidth - newWidth;
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(newWidth, GridUnitType.Pixel));
            ((GridView)LiveList.View).Columns[0].Width = 35;
            ((GridView)LiveList.View).Columns[1].Width = (LiveList.ActualWidth + widthIncrease - 80) * 0.77;
            ((GridView)LiveList.View).Columns[2].Width = (LiveList.ActualWidth + widthIncrease - 80) * 0.18;
            ((GridView)LiveList.View).Columns[3].Width = 50;
        }
        #endregion

        #region remote_ctrl
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            bool ctrlr = (e.KeyboardDevice.IsKeyDown(Key.R) && (e.KeyboardDevice.IsKeyDown(Key.RightCtrl) || e.KeyboardDevice.IsKeyDown(Key.LeftCtrl)));
            bool esc = e.Key == Key.Escape;

            if (ctrlr && RemotePanel.Visibility == Visibility.Hidden)
                RemoteMode_Click(null, null);
            else if ((ctrlr || esc) && RemotePanel.Visibility == Visibility.Visible)
            {
                RemotePanel.Visibility = Visibility.Hidden;
                ReleaseCursor();
            }
        }

        private void RemoteMode_Click(object sender, RoutedEventArgs e)
        {
            if (Presentation == null || !Presentation.IsRunning)
            {
                MessageBox.Show(Labels.MainRemoteNotStart, "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RemotePanel.Visibility = Visibility.Visible;

            Point abs = Mouse.GetPosition(this);
            Point remote = Mouse.GetPosition(RemotePanel);
            abs.X -= remote.X;
            abs.Y -= remote.Y;
            abs = PointToScreen(abs);
            CaptureCursor((int)abs.X, (int)abs.Y, (int)RemotePanel.Width, (int)RemotePanel.Height);
        }

        Rect BoundRect;
        Rect OldRect;

        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        static extern bool ClipCursor(ref Rect lpRect);
        [DllImport("user32.dll")]
        static extern bool GetClipCursor(ref Rect lpRect);

        private void ReleaseCursor()
        {
            ClipCursor(ref OldRect);
        }

        private void CaptureCursor(int x, int y, int w, int h)
        {
            GetClipCursor(ref OldRect);
            BoundRect = new Rect() { Left = x, Top = y, Right = x + w, Bottom = y + h };
            ClipCursor(ref BoundRect);
        }
        #endregion

        #region video_player
        DispatcherTimer mediaPosTimer;
        bool _timeDragging = false;
        FullscreenVideo fullscreen;
        protected void ShowMedia(string path)
        {
            HideMedia();
            VideoPanel.Visibility = Visibility.Visible;
            mediaPosTimer = new DispatcherTimer();
            mediaPosTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            mediaPosTimer.Tick += new EventHandler(mediaPosTimer_Tick);
            VideoPlayer.Open(new Uri(path, UriKind.Absolute));
            PlayMedia(null, null);
        }

        protected void HideMedia()
        {
            if (VideoPanel.Visibility != Visibility.Hidden)
            {
                LiveList.Margin = new Thickness(12, 96, 12, 46);
                VideoPanel.Visibility = Visibility.Hidden;
                VideoDisplay.Visibility = Visibility.Hidden;
                StopMedia(null, null);
                VideoPlayer.Close();
                mediaPosTimer = null;

                if (fullscreen != null)
                {
                    fullscreen.Close();
                    fullscreen = null;
                }
            }
        }

        protected void PlayMedia(object sender, EventArgs args)
        {
            VideoPlayer.Play();
            VideoPlayer.Volume = (double)volumeSlider.Value;
            PlayPauseBtn.Content = "Pause";
            PlayPauseBtn.Click += new RoutedEventHandler(PauseMedia);
            mediaPosTimer.Start();
        }

        protected void PauseMedia(object sender, EventArgs args)
        {
            VideoPlayer.Pause();
            PlayPauseBtn.Content = "Play";
            PlayPauseBtn.Click += new RoutedEventHandler(PlayMedia);
            mediaPosTimer.Stop();
        }

        protected void StopMedia(object sender, EventArgs args)
        {
            VideoPlayer.Stop();
            PlayPauseBtn.Content = "Play";
            PlayPauseBtn.Click += new RoutedEventHandler(PlayMedia);
            mediaPosTimer.Stop();
        }

        protected void ChangeMediaVolume(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            VideoPlayer.Volume = (double)volumeSlider.Value;
        }

        protected void Element_MediaOpened(object sender, EventArgs e)
        {
            timelineSlider.Maximum = VideoPlayer.NaturalDuration.TimeSpan.TotalMilliseconds;

            if (VideoPlayer.HasVideo)
            {
                if (fullscreen == null)
                    fullscreen = new FullscreenVideo(VideoPlayer);

                VideoDisplay.Visibility = Visibility.Visible;
                LiveList.Margin = new Thickness(12, 96, 12, 346);
                VideoPanel.Height = 280;

                double ratio = Math.Min(250d / VideoPlayer.NaturalVideoHeight, 450d / VideoPlayer.NaturalVideoWidth);
                VideoDisplay.Height = VideoPlayer.NaturalVideoHeight * ratio;
                VideoDisplay.Width = VideoPlayer.NaturalVideoWidth * ratio;

                fullscreen.Show();

                ratio = Math.Min(fullscreen.ActualHeight / VideoPlayer.NaturalVideoHeight, fullscreen.ActualWidth / VideoPlayer.NaturalVideoWidth);
                fullscreen.VideoPanel.Height = VideoPlayer.NaturalVideoHeight * ratio;
                fullscreen.VideoPanel.Width = VideoPlayer.NaturalVideoWidth * ratio;
            }
            else
            {
                VideoDisplay.Height = 0;
                VideoPanel.Height = 30;
                LiveList.Margin = new Thickness(12, 96, 12, 86);
            }
        }

        protected void SeekToMediaPosition(object sender, EventArgs args)
        {
            VideoPlayer.Position = new TimeSpan(0, 0, 0, 0, (int)timelineSlider.Value);
            _timeDragging = false;
        }

        private void timelineSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _timeDragging = true;
        }

        protected void mediaPosTimer_Tick(object sender, EventArgs e)
        {
            if (!_timeDragging)
                timelineSlider.Value = (double)VideoPlayer.Position.TotalMilliseconds;
        }
        #endregion

        protected void Main_Closed(object sender, EventArgs e)
        {
            if (fullscreen != null)
                fullscreen.Close();

            if (Presentation != null)
            {
                Presentation.Stop();
                Presentation.Quit();
            }

            Environment.Exit(0);
        }

        //properties
        protected Schedule SelectedSchedule { get; set; }
        protected SlideShow Presentation { get; set; }
    }
}
