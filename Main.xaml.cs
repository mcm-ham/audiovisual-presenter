using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Presenter.App_Code;
using Presenter.Resources;

namespace Presenter
{
    public partial class Main : Window
    {
        DispatcherTimer timer = new DispatcherTimer();
        DispatcherTimer searchDelay = new DispatcherTimer();

        public Main()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            ScheduleList.IsEnabled = false;
            BindLocationList();

            timer.Tick += new EventHandler(timer_Tick);
            searchDelay.Tick += new EventHandler(searchDelay_Tick);
            searchDelay.Interval = TimeSpan.FromMilliseconds(300);
            VideoPlayer.MediaEnded += new EventHandler(VideoPlayer_MediaEnded);

            if (Environment.OSVersion.Version.Major < 6)
                LiveList.ItemContainerStyle.Triggers.Add(GetLiveListStyle());
        }

        #region menu
        protected void Planner_Click(object sender, RoutedEventArgs e)
        {
            OpenDialog dialog = new OpenDialog();
            dialog.Owner = this;
            dialog.ScheduleDeleted += new EventHandler<OpenDialog.DeletedScheduleArgs>(dialog_ScheduleDeleted);
            dialog.ShowDialog();

            if (dialog.SelectedSchedule != null)
            {
                SelectedSchedule = dialog.SelectedSchedule;
                ScheduleName.Text = SelectedSchedule.DisplayName;
                ScheduleList.IsEnabled = true;
                BindScheduleList();

                if (Presentation != null)
                    Presentation.Stop();
            }
        }

        protected void dialog_ScheduleDeleted(object sender, OpenDialog.DeletedScheduleArgs e)
        {
            if (SelectedSchedule != null && SelectedSchedule.ID == e.DeletedScheduleID)
            {
                SelectedSchedule = null;
                ScheduleName.Text = "";
                ScheduleList.IsEnabled = false;
                ScheduleList.ItemsSource = new Item[] { };

                if (Presentation != null)
                    Stop_Click(null, null);
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

        protected void ReportsList_Click(object sender, RoutedEventArgs e)
        {
            ReportsListDialog dialog = new ReportsListDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        protected void ReportsUsage_Click(object sender, RoutedEventArgs e)
        {
            ReportsUsageDialog dialog = new ReportsUsageDialog();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        protected void Window_KeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (e.KeyboardDevice.IsKeyDown(Key.RightCtrl) || e.KeyboardDevice.IsKeyDown(Key.LeftCtrl));
            bool ctrlr = (e.KeyboardDevice.IsKeyDown(Key.R) && ctrl);
            bool ctrlo = (e.KeyboardDevice.IsKeyDown(Key.O) && ctrl);
            bool ctrlm = (e.KeyboardDevice.IsKeyDown(Key.M) && ctrl);
            bool ctrlA = ((e.KeyboardDevice.IsKeyDown(Key.Add) || e.KeyboardDevice.IsKeyDown(Key.OemPlus)) && ctrl);
            bool ctrlS = ((e.KeyboardDevice.IsKeyDown(Key.Subtract) || e.KeyboardDevice.IsKeyDown(Key.OemMinus)) && ctrl);
            bool esc = e.Key == Key.Escape;

            //remote control
            if (ctrlr && RemotePanel.Visibility == Visibility.Hidden)
                RemoteMode_Click(null, null);
            else if ((ctrlr || esc) && RemotePanel.Visibility == Visibility.Visible)
            {
                RemotePanel.Visibility = Visibility.Hidden;
                ReleaseCursor();
            }

            //messenger
            if (ctrlm)
                ShowMessage(null, null);

            //options
            if (ctrlo)
                Options_Click(null, null);

            //enlarge font
            if (ctrlA)
                Config.FontSize++;

            //subtract font
            if (ctrlS)
                Config.FontSize--;
        }
        #endregion

        #region library
        public void BindLocationList()
        {
            LocationList.Items.Clear();

            if (Directory.Exists(Config.LibraryPath))
            {
                IEnumerable<string> paths = Directory.GetDirectories(Config.LibraryPath).Select(p => Path.GetFileName(p));
                if (Directory.GetFiles(Config.LibraryPath).Any(f => Config.SupportedFileTypes.Contains(Path.GetExtension(f).ToLower().TrimStart('.'))))
                    paths = new[] { Labels.MainRootDirName }.Union(paths);
                foreach (string path in paths)
                    LocationList.Items.Add(path);
                LocationList.SelectedValue = Config.SelectedLibrary;
                if (LocationList.SelectedIndex == -1)
                    LocationList.SelectedIndex = 0;
            }

            BindFileList();
        }

        protected string GetSelectedPath()
        {
            if (LocationList.SelectedValue as string == Labels.MainRootDirName)
                return Config.LibraryPath.TrimEnd('\\');
            return Config.LibraryPath + LocationList.SelectedValue;
        }

        protected void BindFileList()
        {
            if (!Directory.Exists(GetSelectedPath()))
            {
                FileList.ItemsSource = new string[] { };
                return;
            }

            //use timer to delay searching for files until user has finished typing, makes gui more responsive
            searchDelay.Stop(); //reset timer
            searchDelay.Start();
        }

        protected void searchDelay_Tick(object sender, EventArgs e)
        {
            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(GetSelectedPath(), "*" + SearchTerms.Text.Replace(" ", "*") + "*").Select(f => Path.GetFileName(f)).OrderBy(n => n));
            if (Directory.GetFiles(GetSelectedPath(), "*.pot").Any() && "none".Contains(SearchTerms.Text.ToLower()))
                files.Add("None.pot");

            if (SearchTerms.Text != "")
            {
                try
                {
                    using (var connection = new System.Data.OleDb.OleDbConnection("Provider=Search.CollatorDSO;Extended Properties=\"Application=Windows\""))
                    {
                        connection.Open();
                        var command = new System.Data.OleDb.OleDbCommand("SELECT System.FileName FROM SystemIndex WHERE contains(System.Search.Contents, '\"" + SearchTerms.Text.Replace("\"", "*") + "*\"') AND SCOPE='file:" + GetSelectedPath() + "'", connection);
                        var reader = command.ExecuteReader();
                        while (reader.Read())
                            files.Add(reader.GetString(0));
                    }
                }
                catch (Exception) { } //windows search 4 not installed
            }

            FileList.ItemsSource = files.Where(f => Config.SupportedFileTypes.Contains(Path.GetExtension(f).TrimStart('.').ToLower())).Distinct();

            if (FileList.Items.Count > 0)
                FileList.ScrollIntoView(FileList.Items[0]);

            searchDelay.Stop();
        }

        protected void SearchTerms_TextChanged(object sender, TextChangedEventArgs e)
        {
            BindFileList();
        }

        protected void LocationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LocationList.SelectedValue != null)
                Config.SelectedLibrary = LocationList.SelectedValue.ToString();
            BindFileList();
        }

        protected void FileSelected()
        {
            if (SelectedSchedule == null)
            {
                Planner_Click(null, null);
                if (SelectedSchedule == null)
                    return;
            }

            foreach (string file in FileList.SelectedItems)
                SelectedSchedule.AddItem(GetSelectedPath() + "\\" + file);
            SelectedSchedule.Save();
            BindScheduleList();

            if (Presentation != null && Presentation.IsRunning)
            {
                Slide s = LiveList.SelectedItem as Slide;
                int hwnd = (s.Type == SlideType.PowerPoint) ? s.Presentation.SlideShowWindow().HWND : fullscreen.HWND;
                User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, Config.ProjectorScreen.WorkingArea.Left, Config.ProjectorScreen.WorkingArea.Top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
                var num = Presentation.Slides.Length;
                Presentation.AddSlides(SelectedSchedule.Items.OrderBy(i => i.Ordinal).Last());
                LiveList.ScrollIntoView(LiveList.Items[LiveList.Items.Count - 1]);
                User32.SetWindowPos(hwnd, User32.HWND_NOTOPMOST, Config.ProjectorScreen.WorkingArea.Left, Config.ProjectorScreen.WorkingArea.Top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);

                var worker = new System.ComponentModel.BackgroundWorker();
                worker.DoWork += (sen, ev) =>
                {
                    Presentation.Slides.Skip(num).Where(sl => sl.Type == SlideType.PowerPoint).ForEach(sl =>
                    {
                        string path = SlideShow.ExportToImage(sl, sl.SlideIndex, "-preview", 333, 250);
                        if (path != "")
                            Dispatcher.BeginInvoke(new Action(() => { sl.Preview = new BitmapImage(new Uri(path)); }));
                    });
                };
                worker.RunWorkerAsync();
            }
        }

        protected void OpenFile2(object sender, RoutedEventArgs e)
        {
            if (LocationList.SelectedIndex == -1)
                return;

            string filename = GetSelectedPath() + "\\" + FileList.SelectedValue;
            if (File.Exists(filename))
                System.Diagnostics.Process.Start(filename);
        }

        protected void OpenLocation2(object sender, RoutedEventArgs e)
        {
            if (LocationList.SelectedIndex == -1)
                return;

            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = GetSelectedPath();
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }

        protected void FileList_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                FileSelected();
            else if (e.Key == Key.Delete)
                DeleteFile(null, null);
        }

        protected void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool enabled = (FileList.SelectedIndex != -1);
            AddBtn.IsEnabled = enabled;
        }

        protected void AddSelected(object sender, EventArgs e)
        {
            FileSelected();
        }

        protected void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //checks if listboxitem is clicked, otherwise something like the scrollbar was clicked so don't add selected file
            ListBoxItem item = ItemsControl.ContainerFromElement(FileList, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
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

        protected void DeleteFile(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(Labels.MainContextDeleteConfirm, "", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                foreach (string file in FileList.SelectedItems.Cast<string>())
                    File.Delete(GetSelectedPath() + "\\" + file);

                BindFileList();
            }
        }

        protected void LibraryToolTipOpening(object sender, ToolTipEventArgs e)
        {
            //only show tooltip when presentation is running since the library pane will be narrow
            e.Handled = (Presentation == null || !Presentation.IsRunning);
        }
        #endregion

        #region dragdrop
        bool _dragging = false;
        private void DragDrop_MouseMove(object sender, MouseEventArgs e)
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

        private void DragDrop_DropHandler(object sender, DragEventArgs e)
        {
            ListBox parent = (ListBox)sender;
            int added = 0;

            if (parent.Name == "ScheduleList" || parent.Name == "LiveList")
            {
                string data = (string)e.Data.GetData(typeof(string));
                if (!String.IsNullOrEmpty(data)) //adding new from filelist
                {
                    SelectedSchedule.AddItem(GetSelectedPath() + "\\" + data);
                    SelectedSchedule.Save();
                    added++;

                    //support insertion of presentation where mouse cursor is when dragging from filelist
                    Item source = SelectedSchedule.Items.Last();
                    Item dest = GetObjectDataFromPoint(parent, e.GetPosition(parent)) as Item;
                    SelectedSchedule.ReOrder(source, dest);
                }
                else if (e.Data.GetFormats().Contains("FileDrop")) //add new from explorer
                {
                    string[] files = (string[])e.Data.GetData("FileDrop");

                    //exapnd directories to include all files within
                    files = files.Union(files.Where(f => Directory.Exists(f)).SelectMany(d => Directory.GetFiles(d))).ToArray();

                    //filter out invalid files
                    files = files.Where(f => Config.SupportedFileTypes.Contains(Path.GetExtension(f).ToLower().TrimStart('.'))).ToArray();

                    foreach (string file in files)
                    {
                        int res = SelectedSchedule.AddItem(file);
                        if (res == 0) //successful
                            added++;
                    }
                    SelectedSchedule.Save();
                }
                else if (parent.Name != "LiveList") //reordering
                {
                    Item source = (Item)e.Data.GetData(typeof(Item));
                    Item dest = GetObjectDataFromPoint(parent, e.GetPosition(parent)) as Item;
                    if (source != null)
                        SelectedSchedule.ReOrder(source, dest);
                }

                if (parent.Name == "LiveList")
                    SelectedSchedule.Items.OrderBy(i => i.Ordinal).Skip(SelectedSchedule.Items.Count - added).ForEach(i => Presentation.AddSlides(i));

                BindScheduleList();
            }
            else //if (parent.Name == "FileList")
            {
                Item data = (Item)e.Data.GetData(typeof(Item));
                if (data != null) //removing item from schedule
                {
                    SelectedSchedule.RemoveItem(data);
                    BindScheduleList();
                }
                else if (e.Data.GetFormats().Contains("FileDrop")) //add files from explorer to library
                {
                    string[] files = (string[])e.Data.GetData("FileDrop");

                    //exapnd directories to include all files within
                    files = files.Union(files.Where(f => Directory.Exists(f)).SelectMany(d => Directory.GetFiles(d))).ToArray();

                    //filter out invalid files
                    files = files.Where(f => Config.SupportedFileTypes.Contains(Path.GetExtension(f).ToLower().TrimStart('.'))).ToArray();

                    foreach (string file in files)
                        File.Copy(file, GetSelectedPath() + "\\" + Path.GetFileName(file));

                    BindFileList();
                }
            }
        }

        //gets the object for the element selected (from the point) in the listbox (source)
        private static object GetObjectDataFromPoint(ListBox source, Point point)
        {
            ListBoxItem item = (source.InputHitTest(point) as UIElement).GetAncestorByType<ListBoxItem>();
            return (item == null) ? null : source.ItemContainerGenerator.ItemFromContainer(item);
        }
        #endregion

        #region order_of_presentions
        protected void BindScheduleList()
        {
            ScheduleList.ItemsSource = SelectedSchedule.Items.OrderBy(i => i.Ordinal);
        }

        protected void OpenFile(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedIndex == -1)
                return;

            Item item = ScheduleList.SelectedItem as Item;

            if (item.IsTemplateNone)
                return;

            if (!item.IsFound)
            {
                MessageBox.Show(Labels.MainMessageFileNotFound, "", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            System.Diagnostics.Process.Start(item.Filename);
        }

        protected void OpenLocation(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedIndex == -1)
                return;

            string path = System.IO.Path.GetDirectoryName((ScheduleList.SelectedItem as Item).Filename);
            if (!Directory.Exists(path))
            {
                MessageBox.Show(Labels.MainMessageFolderNotFound, "", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = path;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }

        protected void RemoveFile(object sender, RoutedEventArgs e)
        {
            int idx = ScheduleList.SelectedIndex;
            SelectedSchedule.RemoveItems(ScheduleList.SelectedItems.Cast<Item>());
            BindScheduleList();
            idx = Math.Min(idx, ScheduleList.Items.Count - 1);
            ScheduleList.SelectedIndex = idx;
        }

        protected void DuplicateFile(object sender, RoutedEventArgs e)
        {
            var selected = ScheduleList.SelectedItem;
            foreach (Item item in ScheduleList.SelectedItems)
            {
                SelectedSchedule.AddItem(item.Filename);
                SelectedSchedule.ReOrder(SelectedSchedule.Items.Last(), item);
            }
            BindScheduleList();
            ScheduleList.SelectedItem = selected;
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
                Presentation.Previous(LiveList.SelectedItem as Slide);
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
                Presentation.Next(LiveList.SelectedItem as Slide);
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
            RemoveBtn.IsEnabled = enabled;
        }
        #endregion

        #region session
        BuildProgress progress = null;
        protected void Start_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSchedule == null)
            {
                Planner_Click(null, null);
                if (SelectedSchedule == null)
                    return;
            }

            if (SelectedSchedule.Items.Count == 0)
            {
                MessageBox.Show(Labels.MainNoAddedItems, "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (System.Windows.Forms.Screen.AllScreens.Length == 1)
                MessageBox.Show(Labels.AppRequiresExtendedDesktop, "", MessageBoxButton.OK, MessageBoxImage.Exclamation);

            if (Config.UseNonPrimaryScreen && Config.ProjectorScreen.DeviceName == Config.PrimaryScreen.DeviceName && System.Windows.Forms.Screen.AllScreens.Length > 1)
                Config.ProjectorScreen = System.Windows.Forms.Screen.AllScreens.First(s => !s.Primary);

            fullscreen = new FullscreenWindow();
            StartBtn.Visibility = Visibility.Collapsed;
            StopBtn.Visibility = Visibility.Visible;
            Interval.Visibility = Visibility.Visible;
            UseSlideTimings.Visibility = Visibility.Visible;
            TimerBtn.Visibility = Visibility.Visible;
            Expander1.Visibility = Visibility.Visible;
            ScheduleList.SetValue(ListBox.VisibilityProperty, Visibility.Hidden); //needs to be hidden and not collapsed because FileList binds to it
            PreviewPanel.Visibility = Visibility.Visible;
            LiveList.SetValue(ListView.VisibilityProperty, Visibility.Visible);
            LiveList.SelectedIndex = 0;
            PrevBtn.Content = Labels.MainBtnPrev;
            NextBtn.Content = Labels.MainBtnNext;
            RefreshBtn.Visibility = Visibility.Hidden;
            RemoveBtn.Visibility = Visibility.Hidden;
            LocationList.Margin = new Thickness(81, 94, 17, 0);
            PrevBtn.IsEnabled = true;
            NextBtn.IsEnabled = true;
            Interval.Text = Config.TimerInterval.ToString();
            UseSlideTimings.IsChecked = Config.UseSlideTimings;
            SetPreview(PreviewImage, null); //set preview to blank slide, otherwise intitally it will be white
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(220, GridUnitType.Pixel));

            SetPreviewPosition();
            Config.instance.SlidePreviewBottomChanged += new EventHandler(instance_SlidePreviewBottomChanged);

            if (Presentation == null)
            {
                Presentation = new SlideShow();
                Presentation.SlideIndexChanged += new EventHandler<SlideShowEventArgs>(Presentation_SlideIndexChanged);
                Presentation.SlideShowEnd += new EventHandler(Presentation_SlideShowEnd);
                Presentation.SlideAdded += new EventHandler<SlideAddedEventArgs>(Presentation_SlideAdded);
            }
            
            new Action(() => Presentation.Start(SelectedSchedule)).BeginInvoke(SlideShowStarted, null);
            progress = new BuildProgress();
            progress.Owner = this;
            progress.ShowDialog();
        }

        protected void SlideShowStarted(IAsyncResult res)
        {
            if (progress == null)
                return;

            Dispatcher.Invoke(new Action(() =>
            {
                LiveList.SelectedIndex = 0;
                progress.Close();
                progress = null;
                fullscreen.Topmost = false;
            }));

            var worker = new System.ComponentModel.BackgroundWorker();
            worker.DoWork += (sen, ev) => {
                Presentation.Slides.Where(s => s.Type == SlideType.PowerPoint).ForEach(s =>
                {
                    string path = SlideShow.ExportToImage(s, s.SlideIndex, "-preview", 333, 250);
                    if (path != "")
                        Dispatcher.BeginInvoke(new Action(() => { s.Preview = new BitmapImage(new Uri(path)); }));
                });
            };
            worker.RunWorkerAsync();
        }

        protected void instance_SlidePreviewBottomChanged(object sender, EventArgs e)
        {
            SetPreviewPosition();
        }

        protected void SetPreviewPosition()
        {
            if (!Config.SlidePreviewBottom)
            {
                col3.SetValue(ColumnDefinition.WidthProperty, new GridLength(350, GridUnitType.Pixel));
                Grid.SetRowSpan(LiveList, 2);
                Grid.SetColumn(PreviewPanel, 2);
                Grid.SetRow(PreviewPanel, 0);
                Grid.SetRowSpan(PreviewPanel, 2);
                PreviewPanel.VerticalAlignment = VerticalAlignment.Top;
                PreviewPanel.HorizontalAlignment = HorizontalAlignment.Right;
                PreviewPanel.Orientation = Orientation.Vertical;
                PreviewPanel.MaxHeight = Double.PositiveInfinity;
                PreviewPanel.MaxWidth = 350;
                PreviewPanel.Height = Double.NaN;
                PreviewPanel.Margin = new Thickness(0, 128, 10, 0);
                PreviewImage.Margin = new Thickness(0, 0, 0, 20);
                LiveList.Margin = new Thickness(12, 128, 12, 46);
                GridSplitter1.Visibility = Visibility.Hidden;
                GridSplitter2.Visibility = Visibility.Visible;
            }
            else
            {
                col3.SetValue(ColumnDefinition.WidthProperty, new GridLength(0, GridUnitType.Pixel));
                Grid.SetRowSpan(LiveList, 1);
                Grid.SetColumn(PreviewPanel, 1);
                Grid.SetRow(PreviewPanel, 1);
                Grid.SetRowSpan(PreviewPanel, 1);
                PreviewPanel.VerticalAlignment = VerticalAlignment.Bottom;
                PreviewPanel.HorizontalAlignment = HorizontalAlignment.Left;
                PreviewPanel.Orientation = Orientation.Horizontal;
                PreviewPanel.MaxHeight = 250;
                PreviewPanel.MaxWidth = Double.PositiveInfinity;
                PreviewPanel.Width = Double.NaN;
                PreviewPanel.Margin = new Thickness(12, 10, 0, 45);
                PreviewImage.Margin = new Thickness(0, 0, 20, 0);
                LiveList.Margin = new Thickness(12, 128, 12, 10);
                GridSplitter1.Visibility = Visibility.Visible;
                GridSplitter2.Visibility = Visibility.Hidden;
            }

            LiveList.UpdateLayout();
            var adjustment = Config.SlidePreviewBottom ? 105 : 120;
            ((GridView)LiveList.View).Columns[1].Width = (col2.ActualWidth - adjustment) * 0.74;
            ((GridView)LiveList.View).Columns[2].Width = (col2.ActualWidth - adjustment) * 0.18; 
        }

        protected void SlideListViewItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var sl = ((sender as ListViewItem).DataContext as Slide);
            SetPreview(PreviewImage, sl.Preview);
        }

        protected void Stop_Click(object sender, RoutedEventArgs e)
        {
            StartBtn.Visibility = Visibility.Visible;
            StopBtn.Visibility = Visibility.Hidden;
            Interval.Visibility = Visibility.Hidden;
            UseSlideTimings.Visibility = Visibility.Hidden;
            TimerBtn.Visibility = Visibility.Hidden;
            Expander1.Visibility = Visibility.Hidden;
            PreviewPanel.Visibility = Visibility.Hidden;
            GridSplitter1.Visibility = Visibility.Hidden;
            LibraryGrid.Visibility = Visibility.Visible;
            Expander1.Content = "<";
            col3.SetValue(ColumnDefinition.WidthProperty, new GridLength(0, GridUnitType.Pixel));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength((this.ActualWidth - 20) / 2, GridUnitType.Pixel));
            ScheduleList.SetValue(ListBox.VisibilityProperty, Visibility.Visible);
            LiveList.SetValue(ListView.VisibilityProperty, Visibility.Collapsed);
            PrevBtn.Content = Labels.MainBtnMoveUp;
            NextBtn.Content = Labels.MainBtnMoveDown;
            if (ScheduleList.SelectedIndex == -1)
            {
                PrevBtn.IsEnabled = false;
                NextBtn.IsEnabled = false;
            }
            Config.TimerInterval = Util.Parse<int>(Interval.Text);
            RefreshBtn.Visibility = Visibility.Visible;
            RemoveBtn.Visibility = Visibility.Visible;
            LocationList.Margin = new Thickness(81, 94, 80, 0);
            PreviewImage.Background = new SolidColorBrush(Colors.Black);
            CurrentImage.Background = new SolidColorBrush(Colors.Black);
            Config.instance.SlidePreviewBottomChanged -= new EventHandler(instance_SlidePreviewBottomChanged);
            HideMedia();
            if (fullscreen != null)
            {
                fullscreen.Close();
                fullscreen = null;
            }
            if (Presentation != null)
                Presentation.Stop();
        }

        protected void Presentation_SlideAdded(object sender, SlideAddedEventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (progress != null)
                {
                    if (progress.Cancelled)
                    {
                        Stop_Click(null, null);
                        progress = null;
                        return;
                    }

                    if (e.Progress < 0)
                    {
                        LiveList.Items.Clear();
                        return;
                    }

                    progress.UpdateProgress(e.Progress);
                }

                if (e.NewSlide == null)
                    return;

                int idx = LiveList.Items.Add(e.NewSlide);

                //add new listitem happens asyncronously so look for listitem asyncronously as well with low priority to ensure listitem has been added before running
                if (e.NewSlide.ScheduleItem.EntityState != System.Data.EntityState.Detached && !e.NewSlide.ScheduleItem.Flags.IsLoaded)
                    e.NewSlide.ScheduleItem.Flags.Load();
                if (e.NewSlide.ScheduleItem.Flags.Any(f => f.Index == e.NewSlide.ItemIndex))
                {
                    EventHandler handler = null;
                    handler = (sen, ev) => {
                        var item = LiveList.ItemContainerGenerator.ContainerFromIndex(idx) as ListViewItem;
                        if (item != null)
                        {
                            HightlightRow(item);
                            LiveList.ItemContainerGenerator.StatusChanged -= handler;
                        }
                    };
                    LiveList.ItemContainerGenerator.StatusChanged += handler;
                }
            }));
        }

        protected void Presentation_SlideShowEnd(object sender, EventArgs e)
        {
            //if triggered by powerpoint slideshow being closed not thru presenter, execute stop down on background thread to prevent freeze
            if (Presentation != null && Presentation.IsRunning)
                Dispatcher.BeginInvoke(new Action(() => Stop_Click(null, null)), DispatcherPriority.Background);
        }

        private int previdx = 0;
        protected void Presentation_SlideIndexChanged(object sender, SlideShowEventArgs e)
        {
            int idx = e.NewIndex - 1;

            //if oldindex is -1, triggered by powerpoint change slide event so don't proceed unless new index does not match
            //current livelist selected index to only run when slideshow is using timings to automatically advance
            if (e.OldIndex == -1 && LiveList.SelectedIndex == idx)
                return;

            if (e.OldIndex != e.NewIndex)
            {
                LiveList.SelectionChanged -= new SelectionChangedEventHandler(LiveList_SelectionChanged);
                LiveList.SelectedIndex = idx;
                LiveList.SelectionChanged += new SelectionChangedEventHandler(LiveList_SelectionChanged);
            }

            if (Presentation.Slides[idx].Presentation != Presentation.Slides[previdx].Presentation)
            {
                if (Presentation.Slides[previdx].Presentation != null)
                    Presentation.Slides[previdx].GotoSlide(1);
            }

            if (Presentation.Slides.Length > idx && idx >= 0 && Presentation.Slides[idx].Type != SlideType.PowerPoint)
            {
                ShowMedia(Presentation.Slides[idx]);
                User32.SetWindowPos(fullscreen.HWND, User32.HWND_TOP, Config.ProjectorScreen.WorkingArea.Left, Config.ProjectorScreen.WorkingArea.Top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
            }
            else
            {
                HideMedia();
                fullscreen.HideWindow();
                Presentation.Slides[idx].SetTop();

                if (idx == -1)
                    return;

                if (Presentation.Slides.Length > idx)
                    SetPreview(CurrentImage, Presentation.Slides[idx].Preview);
            }

            //autoscroll
            if (idx > previdx)
                LiveList.ScrollIntoView(LiveList.Items[Math.Min(LiveList.Items.Count - 1, idx + 5)]);
            else
                LiveList.ScrollIntoView(LiveList.Items[Math.Max(0, idx - 5)]);
            previdx = idx;
        }

        protected void SetPreview(Border preview, BitmapSource image)
        {
            if (image == null)
            {
                preview.Background = new SolidColorBrush(Config.ScreenBlankColour);
                return;
            }
            preview.Background = new ImageBrush(image);

            var widthRatio = image.PixelWidth / preview.ActualWidth;
            var heightRatio = image.PixelHeight / preview.ActualHeight;
            if (widthRatio > heightRatio)
            {
                var y = (preview.ActualHeight - image.PixelHeight / widthRatio) / 2.0;
                preview.BorderThickness = new Thickness(0, y, 0, y);
            }
            else
            {
                var x = (preview.ActualWidth - image.PixelWidth / heightRatio) / 2.0;
                preview.BorderThickness = new Thickness(x, 0, x, 0);
            }
        }

        protected void LiveList_KeyDown(object sender, KeyEventArgs e)
        {
            //map all PowerPoint SlideShow shortcut keys

            if (e.Key == Key.Down || e.Key == Key.Right || e.Key == Key.PageDown || e.Key == Key.N || e.Key == Key.Space)
            {
                Presentation.Next(LiveList.SelectedItem as Slide);
                e.Handled = true;
            }

            if (e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.PageUp || e.Key == Key.P || e.Key == Key.Back)
            {
                Presentation.Previous(LiveList.SelectedItem as Slide);
                e.Handled = true;
            }

            if (e.Key == Key.B || e.Key == Key.OemPeriod)
            {
                if (fullscreen.Visibility == System.Windows.Visibility.Visible)
                    fullscreen.HideWindow();
                else
                    fullscreen.ShowBlank();
                this.Focus();
                e.Handled = true;
            }

            /*if (e.Key == Key.W || e.Key == Key.OemComma)
            {
                fullscreen.ShowBlank();
                this.Focus();
                e.Handled = true;
            }*/

            if (e.Key == Key.End || e.Key == Key.Escape || e.Key == Key.Cancel)
            {
                Stop_Click(null, null);
                e.Handled = true;
            }
        }

        protected void TimerStart_Click(object sender, RoutedEventArgs e)
        {
            if (TimerBtn.Content.ToString() != "Timer End")
            {
                TimerBtn.Content = "Timer End";
                int interval = Util.Parse<int?>(Interval.Text) ?? 8;
                timer.Interval = TimeSpan.FromSeconds(interval);
                timer.Start();
                _mediaAdvanceOnComplete = true;
            }
            else
            {
                TimerBtn.Content = "Timer";
                timer.Stop();
                _mediaAdvanceOnComplete = false;
            }
        }

        void timer_Tick(object sender, EventArgs e)
        {
            if (Presentation == null || !Presentation.IsRunning)
                return;

            Slide slide = Presentation.Slides[LiveList.SelectedIndex];

            if (slide.Type == SlideType.Video || slide.Type == SlideType.Audio)
                return;

            if (slide.JumpIndex.HasValue && slide.JumpIndex > 0 && slide.JumpIndex <= Presentation.Slides.Length)
                LiveList.SelectedIndex = slide.JumpIndex.Value - 1;
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
            if (LiveList.SelectedItem == null)
                return;

            Presentation_SlideIndexChanged(Presentation, new SlideShowEventArgs(LiveList.SelectedIndex + 1, LiveList.SelectedIndex + 1));

            if ((LiveList.SelectedItem as Slide).Type == SlideType.PowerPoint)
            {
                var slide = LiveList.SelectedItem as Slide;
                //place in background thread otherwise otherwise if this method takes longer due to slide animations the next item the mouse is over once finished is selected
                new Action(() => Presentation.GoTo(slide)).BeginInvoke(null, null);
            }
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
            ((GridView)LiveList.View).Columns[1].Width = (col2.ActualWidth + widthIncrease - 105) * 0.74;
            ((GridView)LiveList.View).Columns[2].Width = (col2.ActualWidth + widthIncrease - 105) * 0.18;
        }

        private void HightlightRow(object sender, RoutedEventArgs e)
        {
            ListViewItem row = (sender as Button).GetAncestorByType<ListViewItem>();
            Slide slide = row.DataContext as Slide;
            if (slide.Type == SlideType.Blank)
                return;

            Flag flag = slide.ScheduleItem.Flags.FirstOrDefault(f => f.Index == slide.ItemIndex);

            if (flag == null)
                slide.ScheduleItem.Flags.Add(new Flag() { Index = (short)slide.ItemIndex, Colour = "Red" });
            else
                slide.ScheduleItem.Flags.Remove(flag);
            slide.ScheduleItem.Save();

            HightlightRow(row);
        }

        private void HightlightRow(ListViewItem row)
        {
            Slide slide = row.DataContext as Slide;

            Style style = new Style();
            style.Setters.Add(new EventSetter(ListViewItem.MouseEnterEvent, new MouseEventHandler(SlideListViewItem_MouseEnter)));
            style.Setters.Add(new EventSetter(ListViewItem.PreviewMouseRightButtonDownEvent, new MouseButtonEventHandler(SlideListViewItem_MouseRightButtonDown)));
            if (Environment.OSVersion.Version.Major < 6)
                style.Triggers.Add(GetLiveListStyle());

            if (!slide.ScheduleItem.Flags.IsLoaded)
                slide.ScheduleItem.Flags.Load();
            Flag flag = slide.ScheduleItem.Flags.FirstOrDefault(f => f.Index == slide.ItemIndex);
            if (flag != null)
                style.Setters.Add(new Setter(ListViewItem.ForegroundProperty, new SolidColorBrush(flag.SystemColor)));

            row.Style = style;
        }

        /// <summary>
        /// listviewitem on winxp does not highlight on hover by default so we add a hover colour below
        /// </summary>
        public Trigger GetLiveListStyle()
        {
            Setter setter = new Setter();
            setter.Property = ListViewItem.BackgroundProperty;
            setter.Value = new SolidColorBrush(new Color() { A = 255, R = 180, G = 230, B = 253 });
            Trigger trigger = new Trigger();
            trigger.Property = ListViewItem.IsMouseOverProperty;
            trigger.Value = true;
            trigger.Setters.Add(setter);
            return trigger;
        }

        private Slide _selectedSlide = null;
        protected void EditPres(object sender, RoutedEventArgs e)
        {
            if (_selectedSlide == null || _selectedSlide.Type != SlideType.PowerPoint)
            {
                MessageBox.Show(Labels.MainContextEditError, "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _selectedSlide.Presentation.Application.Visible = Microsoft.Office.Core.MsoTriState.msoTrue;
            if (_selectedSlide.Presentation.Windows.Count == 0)
                _selectedSlide.Presentation.NewWindow();
            User32.SetWindowPos(_selectedSlide.Presentation.Application.HWND, User32.HWND_TOP, Config.PrimaryScreen.WorkingArea.Left + (int)(Config.PrimaryScreen.WorkingArea.Width * 0.05), Config.PrimaryScreen.WorkingArea.Top + +(int)(Config.PrimaryScreen.WorkingArea.Height * 0.05), (int)(Config.PrimaryScreen.WorkingArea.Width * 0.9), +(int)(Config.PrimaryScreen.WorkingArea.Height * 0.9), 0);
            _selectedSlide.Presentation.Windows[1].Activate();
            _selectedSlide.PSlide.Select();
        }

        protected void SlideListViewItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _selectedSlide = (sender as ListViewItem).DataContext as Slide;
            e.Handled = true; //prevent right click from selecting
        }

        protected void LiveList_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; //prevent double click from selecting two different slides in quick succession if list auto scrolls in between
        }

        protected void UseSlideTimingsChanged(object sender, RoutedEventArgs e)
        {
            Config.UseSlideTimings = UseSlideTimings.IsChecked ?? false;
            if (Presentation != null)
                Presentation.UpdateSlideTimings();
        }

        #endregion

        #region message_box
        Window messageBox = null;
        protected void ShowMessage(object sender, RoutedEventArgs e)
        {
            MethodInfo setIsPressed = ShowMessageBtn.GetType().GetMethod("set_IsPressed", BindingFlags.Instance | BindingFlags.NonPublic);

            if (messageBox != null)
            {
                messageBox.Close();
                messageBox = null;
                setIsPressed.Invoke(ShowMessageBtn, new object[] { false });
                return;
            }

            setIsPressed.Invoke(ShowMessageBtn, new object[] { true });

            ScreenMessage prompt = new ScreenMessage();
            prompt.Owner = this;
            prompt.ShowInTaskbar = false;
            prompt.Closed += (sen, args) =>
            {
                if ((sen as ScreenMessage).MessageBox == null)
                {
                    messageBox = null;
                    setIsPressed.Invoke(ShowMessageBtn, new object[] { false });
                }
            };
            prompt.ShowDialog();
            messageBox = prompt.MessageBox;
        }
        #endregion

        #region remote_ctrl
        private void RemoteMode_Click(object sender, RoutedEventArgs e)
        {
            if (Presentation == null || !Presentation.IsRunning)
            {
                MessageBox.Show(Labels.MainRemoteNotStart, "", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RemotePanel.Visibility = Visibility.Visible;
            LiveList.Focus(); //cause livelist to focus so that up or down arrow keys changes slides
            AdornerLayer.GetAdornerLayer(LiveList).Visibility = Visibility.Collapsed; //hide selection border which appears above remote panel

            Point abs = Mouse.GetPosition(this);
            Point remote = Mouse.GetPosition(RemotePanel);
            Point dpi = Util.GetResolution(RemotePanel);
            abs.X -= remote.X;
            abs.Y -= remote.Y;
            abs = PointToScreen(abs);

            //1px border to prevent mouse from being able to select a slide as it can on some pcs
            CaptureCursor((int)abs.X + 1, (int)abs.Y + 1, (int)(RemotePanel.ActualWidth * dpi.X / 96) - 2, (int)(RemotePanel.ActualHeight * dpi.Y / 96) - 2);
        }

        User32.Rect BoundRect;
        User32.Rect OldRect;

        private void ReleaseCursor()
        {
            User32.ClipCursor(ref OldRect);
        }

        private void CaptureCursor(int x, int y, int w, int h)
        {
            User32.GetClipCursor(ref OldRect);
            BoundRect = new User32.Rect() { Left = x, Top = y, Right = x + w, Bottom = y + h };
            User32.ClipCursor(ref BoundRect);
        }
        #endregion

        #region video_player
        DispatcherTimer mediaPosTimer;
        bool _timeDragging = false;
        bool _editingTime = false;
        TimeSpan? _initEditTime = null;
        FullscreenWindow fullscreen = null;
        bool _mediaAdvanceOnComplete = false;
        protected void ShowMedia(Slide slide)
        {
            HideMedia();
            if (slide.Type == SlideType.Blank)
            {
                SetPreview(CurrentImage, null);
                fullscreen.ShowBlank();
                this.Focus();
            }
            else if (slide.Type == SlideType.Image)
            {
                SetPreview(CurrentImage, slide.Preview);
                fullscreen.Show(slide.Image);
                this.Focus();
            }
            else
            {
                if (slide.Type == SlideType.Audio)
                {
                    fullscreen.ShowBlank();
                    this.Focus();
                }

                CurrentImage.Visibility = Visibility.Collapsed;
                VideoPanel.Visibility = Visibility.Visible;
                mediaPosTimer = new DispatcherTimer();
                mediaPosTimer.Interval = TimeSpan.FromMilliseconds(100);
                mediaPosTimer.Tick += new EventHandler(mediaPosTimer_Tick);
                VideoPlayer.Open(new Uri(slide.Filename, UriKind.Absolute));
                PlayMedia(null, null);
            }
        }

        protected void VideoPlayer_MediaEnded(object sender, EventArgs e)
        {
            if (!_mediaAdvanceOnComplete)
                return;

            Slide slide = Presentation.Slides[LiveList.SelectedIndex];
            if (slide.JumpIndex.HasValue && slide.JumpIndex > 0 && slide.JumpIndex <= Presentation.Slides.Length)
                LiveList.SelectedIndex = slide.JumpIndex.Value - 1;
            else if (LiveList.Items.Count > LiveList.SelectedIndex)
                LiveList.SelectedIndex++;
        }

        protected void HideMedia()
        {
            if (VideoPanel.Visibility != Visibility.Collapsed)
            {
                CurrentImage.Visibility = Visibility.Visible;
                VideoPanel.Visibility = Visibility.Collapsed;
                VideoDisplay.Visibility = Visibility.Hidden;
                StopMedia(null, null);
                VideoPlayer.Close();
                mediaPosTimer = null;
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
            totalTime.Text = (int)VideoPlayer.NaturalDuration.TimeSpan.TotalMinutes + ":" + VideoPlayer.NaturalDuration.TimeSpan.Seconds.ToString("d2");

            if (VideoPlayer.HasVideo)
            {
                fullscreen.Show(VideoPlayer);

                VideoDisplay.Visibility = Visibility.Visible;

                double ratio;
                if (Config.SlidePreviewBottom)
                {
                    ratio = VideoDisplay.Height / VideoPlayer.NaturalVideoHeight;
                    VideoDisplay.Width = VideoPlayer.NaturalVideoWidth * ratio;
                }
                else
                {
                    ratio = VideoDisplay.Width / VideoPlayer.NaturalVideoWidth;
                    VideoDisplay.Height = VideoPlayer.NaturalVideoHeight * ratio;
                }

                fullscreen.Show();
                this.Focus(); //retain focus in Main window and not in shown fullscreen

                ratio = Math.Min(fullscreen.ActualHeight / VideoPlayer.NaturalVideoHeight, fullscreen.ActualWidth / VideoPlayer.NaturalVideoWidth);
                fullscreen.VideoPanel.Height = VideoPlayer.NaturalVideoHeight * ratio;
                fullscreen.VideoPanel.Width = VideoPlayer.NaturalVideoWidth * ratio;
            }
            else
            {
                VideoDisplay.Visibility = Visibility.Hidden;
            }
        }

        protected void SeekToMediaPosition(object sender, EventArgs args)
        {
            VideoPlayer.Position = TimeSpan.FromMilliseconds(timelineSlider.Value);
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

        private void timelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_editingTime)
            {
                TimeSpan draggedVal = TimeSpan.FromMilliseconds(timelineSlider.Value);
                currentTime.Text = (int)draggedVal.TotalMinutes + ":" + draggedVal.Seconds.ToString("d2");
            }
        }

        private void currentTime_GotFocus(object sender, RoutedEventArgs e)
        {
            _editingTime = true;
            _initEditTime = getCurrentTime();
        }

        private void currentTime_LostFocus(object sender, RoutedEventArgs e)
        {
            _editingTime = false;

            TimeSpan? value = getCurrentTime();

            //if entered value is valid and the time has changed (presume they would have entered a new value if the user wanted to jump to a time)
            if (value.HasValue && value != _initEditTime)
                VideoPlayer.Position = value.Value;
        }

        private TimeSpan? getCurrentTime()
        {
            var val = currentTime.Text.Split(new char[] { ':', '.' }, StringSplitOptions.RemoveEmptyEntries).ToArray();

            if (val.Length == 1)
                return TimeSpan.FromSeconds(Util.Parse<int>(val[0]));

            if (val.Length == 2)
                return new TimeSpan(0, Util.Parse<int>(val[0]), Util.Parse<int>(val[1]));

            if (val.Length == 3)
                return new TimeSpan(Util.Parse<int>(val[0]), Util.Parse<int>(val[1]), Util.Parse<int>(val[2]));

            return null;
        }

        private void currentTime_KeyDown(object sender, KeyEventArgs e)
        {
            //cause the textbox to loose focus on enter to update media time
            if (e.Key == Key.Enter)
                LiveList.Focus();

            //cause the textbox to loose focus on esc but blank out value so media time is not updated
            if (e.Key == Key.Escape)
            {
                currentTime.Text = "";
                LiveList.Focus();
            }
        }

        private void GridSplitter_LayoutUpdated(object sender, EventArgs e)
        {
            if (Presentation == null || !Presentation.IsRunning)
                return;

            if (!Config.SlidePreviewBottom)
            {
                if (Grid1.ColumnDefinitions[2].ActualWidth == 0)
                    return;

                PreviewPanel.Width = Math.Max(0, Grid1.ColumnDefinitions[2].ActualWidth - 20);

                //if height is zero, then control height will be set to zero and can never be multiplied by a ratio to increase in height
                if (PreviewPanel.ActualWidth <= 0)
                    return;

                double ratio = PreviewPanel.ActualWidth / PreviewImage.ActualWidth;

                //if height is allowed to be set close to zero precision is lost resulting in loss of fixed aspect ratio
                if (PreviewImage.Width * ratio < 1.0)
                    return;

                PreviewImage.Height *= ratio;
                PreviewImage.Width *= ratio;

                CurrentImage.Height *= ratio;
                CurrentImage.Width *= ratio;

                if (VideoDisplay.ActualWidth <= 0 || MediaControls.ActualWidth > PreviewPanel.ActualWidth)
                    return;

                ratio = PreviewPanel.ActualWidth / VideoDisplay.ActualWidth;
                VideoDisplay.Height *= ratio;
                VideoDisplay.Width *= ratio;
            }
            else
            {
                PreviewPanel.Height = Math.Max(0, Grid1.RowDefinitions[1].ActualHeight - 60);

                //if height is zero, then control height will be set to zero and can never be multiplied by a ratio to increase in height
                if (PreviewPanel.ActualHeight <= 0)
                    return;

                double ratio = PreviewPanel.ActualHeight / PreviewImage.ActualHeight;

                //if height is allowed to be set close to zero precision is lost resulting in loss of fixed aspect ratio
                if (PreviewImage.Height * ratio < 1.0)
                    return;
                
                PreviewImage.Height *= ratio;
                PreviewImage.Width *= ratio;

                CurrentImage.Height *= ratio;
                CurrentImage.Width *= ratio;

                if (VideoDisplay.ActualHeight <= 0 || MediaControls.ActualHeight > PreviewPanel.ActualHeight)
                    return;

                ratio = (PreviewPanel.ActualHeight - MediaControls.ActualHeight) / VideoDisplay.ActualHeight;
                VideoDisplay.Height *= ratio;
                VideoDisplay.Width *= ratio;
            }
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
        public Schedule SelectedSchedule { get; set; }
        protected SlideShow Presentation { get; set; }
    }
}
