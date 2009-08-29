using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SongPresenter.App_Code;
using SongPresenter.Resources;

namespace SongPresenter
{
    public partial class Main : Window
    {
        DispatcherTimer timer = new DispatcherTimer();
        DispatcherTimer selectionDelay = new DispatcherTimer();
        DispatcherTimer searchDelay = new DispatcherTimer();

        public Main()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            ScheduleList.IsEnabled = false;
            BindLocationList();

            timer.Tick += new EventHandler(timer_Tick);
            selectionDelay.Tick += new EventHandler(selectionDelay_Tick);
            selectionDelay.Interval = TimeSpan.FromMilliseconds(100);
            searchDelay.Tick += new EventHandler(searchDelay_Tick);
            searchDelay.Interval = TimeSpan.FromMilliseconds(500);

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
                SlideShow.RemoveOldPres();
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
                foreach (var path in Directory.GetDirectories(Config.LibraryPath).Select(p => p.Substring(p.LastIndexOf('\\') + 1)))
                    LocationList.Items.Add(path);
                LocationList.SelectedValue = Config.SelectedLibrary;
                if (LocationList.SelectedIndex == -1)
                    LocationList.SelectedIndex = 0;
            }

            BindFileList();
        }

        
        protected void BindFileList()
        {
            if (!Directory.Exists(Config.LibraryPath + LocationList.SelectedValue))
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
            files.AddRange(Directory.GetFiles(Config.LibraryPath + LocationList.SelectedValue, "*" + SearchTerms.Text.Replace(" ", "*") + "*").Select(f => Path.GetFileName(f)));

            if (SearchTerms.Text != "")
            {
                try
                {
                    using (var connection = new System.Data.OleDb.OleDbConnection("Provider=Search.CollatorDSO;Extended Properties=\"Application=Windows\""))
                    {
                        connection.Open();
                        var command = new System.Data.OleDb.OleDbCommand("SELECT System.FileName FROM SystemIndex WHERE contains(System.Search.Contents, '\"" + SearchTerms.Text.Replace("\"", "*") + "*\"') AND SCOPE='file:" + Config.LibraryPath + LocationList.SelectedValue + "'", connection);
                        var reader = command.ExecuteReader();
                        while (reader.Read())
                            files.Add(reader.GetString(0));
                    }
                }
                catch (Exception) { } //windows search 4 not installed
            }

            FileList.ItemsSource = files.Where(f => Config.SupportedFileTypes.Contains(Path.GetExtension(f).TrimStart('.').ToLower())).Distinct().OrderBy(f => f);

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


            bool running = (Presentation != null && Presentation.IsRunning);
            foreach (string file in FileList.SelectedItems)
                SelectedSchedule.AddItem(Config.LibraryPath + LocationList.SelectedValue + "\\" + file, !running);
            BindScheduleList();

            if (running)
            {
                Presentation.AddSlides(SelectedSchedule.Items.OrderBy(i => i.Ordinal).Last());
                LiveList.ScrollIntoView(LiveList.Items[LiveList.Items.Count - 1]);
            }
        }

        protected void OpenFile2(object sender, RoutedEventArgs e)
        {
            if (LocationList.SelectedIndex == -1)
                return;

            string filename = Config.LibraryPath + LocationList.SelectedValue + "\\" + FileList.SelectedValue;
            if (File.Exists(filename))
                System.Diagnostics.Process.Start(filename);
        }

        protected void OpenLocation2(object sender, RoutedEventArgs e)
        {
            if (LocationList.SelectedIndex == -1)
                return;

            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = Config.LibraryPath + LocationList.SelectedValue;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }

        protected void FileList_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                FileSelected();
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
                        int res = SelectedSchedule.AddItem(file);
                        if (res == 0) //successful
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
                    SelectedSchedule.Items.OrderBy(i => i.Ordinal).Skip(SelectedSchedule.Items.Count - added).ForEach(i => Presentation.AddSlides(i));
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
            ListBoxItem item = (source.InputHitTest(point) as UIElement).GetAncestorByType<ListBoxItem>();
            return (item == null) ? null : source.ItemContainerGenerator.ItemFromContainer(item);
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

            Item item = ScheduleList.SelectedItem as Item;
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

            StartBtn.Visibility = Visibility.Collapsed;
            StopBtn.Visibility = Visibility.Visible;
            Interval.Visibility = Visibility.Visible;
            TimerBtn.Visibility = Visibility.Visible;
            Expander1.Visibility = Visibility.Visible;
            PreviewPanel.Visibility = Visibility.Visible;
            GridSplitter1.Visibility = Visibility.Visible;
            ScheduleList.SetValue(ListBox.VisibilityProperty, Visibility.Hidden); //needs to be hidden and not collapsed because FileList binds to it
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

            double widthIncrease = col1.ActualWidth - 220;
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(220, GridUnitType.Pixel));
            ((GridView)LiveList.View).Columns[1].Width = (col2.ActualWidth + widthIncrease - 105) * 0.74;
            ((GridView)LiveList.View).Columns[2].Width = (col2.ActualWidth + widthIncrease - 105) * 0.18;

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

            Dispatcher.Invoke(new Action(() => {
                progress.Close();
                progress = null;
            }));
        }

        protected void SlideListViewItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var sl = ((sender as ListViewItem).DataContext as Slide);
            if (System.IO.File.Exists(sl.Preview))
                PreviewImage.SetValue(Image.SourceProperty, new System.Windows.Media.Imaging.BitmapImage(new Uri(sl.Preview)));
        }

        protected void Stop_Click(object sender, RoutedEventArgs e)
        {
            StartBtn.Visibility = Visibility.Visible;
            StopBtn.Visibility = Visibility.Hidden;
            Interval.Visibility = Visibility.Hidden;
            TimerBtn.Visibility = Visibility.Hidden;
            Expander1.Visibility = Visibility.Hidden;
            PreviewPanel.Visibility = Visibility.Hidden;
            GridSplitter1.Visibility = Visibility.Hidden;
            LibraryGrid.Visibility = Visibility.Visible;
            Expander1.Content = "<";
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
            PreviewImage.Source = null;
            CurrentImage.Source = null;
            HideMedia();
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

                    progress.UpdateProgress(e.Progress);
                    if (e.NewSlide == null)
                    {
                        LiveList.Items.Clear();
                        return;
                    }
                }

                int idx = LiveList.Items.Add(e.NewSlide);

                //add new listitem happens asyncronously so look for lisitem asyncronously as well with low priority to ensure listitem has been added before running
                if (e.NewSlide.ScheduleItem.EntityState != System.Data.EntityState.Detached && !e.NewSlide.ScheduleItem.Flags.IsLoaded)
                    e.NewSlide.ScheduleItem.Flags.Load();
                if (e.NewSlide.ScheduleItem.Flags.Any(f => f.Index == e.NewSlide.ItemIndex))
                    Dispatcher.BeginInvoke(new Action(() => {
                        HightlightRow(LiveList.ItemContainerGenerator.ContainerFromIndex(idx) as ListViewItem);
                    }), DispatcherPriority.ApplicationIdle);
            }));
        }

        protected void Presentation_SlideShowEnd(object sender, EventArgs e)
        {
            if (Presentation != null && Presentation.IsRunning)
                Dispatcher.Invoke(new Action(() => Stop_Click(null, null)));
        }

        private int previdx = 0;
        protected void Presentation_SlideIndexChanged(object sender, SlideShowEventArgs e)
        {
            int idx = e.NewIndex - 1;

            if (e.OldIndex != e.NewIndex)
            {
                LiveList.SelectionChanged -= new SelectionChangedEventHandler(LiveList_SelectionChanged);
                LiveList.SelectedIndex = idx;
                LiveList.SelectionChanged += new SelectionChangedEventHandler(LiveList_SelectionChanged);
            }

            if (Presentation.Slides.Length > idx && idx >= 0 && Presentation.Slides[idx].Type != SlideType.PowerPoint)
                ShowMedia(Presentation.Slides[idx].Filename);
            else
            {
                HideMedia();

                if (idx == -1)
                    return;

                if (Presentation.Slides.Length > idx && !String.IsNullOrEmpty(Presentation.Slides[idx].Preview))
                    CurrentImage.SetValue(Image.SourceProperty, new System.Windows.Media.Imaging.BitmapImage(new Uri(Presentation.Slides[idx].Preview)));

                //autoscroll
                if (idx > previdx)
                    LiveList.ScrollIntoView(LiveList.Items[Math.Min(LiveList.Items.Count - 1, idx + 5)]);
                else
                    LiveList.ScrollIntoView(LiveList.Items[Math.Max(0, idx - 5)]);
                previdx = idx;
            }
        }

        protected void LiveList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Right)
            {
                Presentation.Next();
                e.Handled = true;
            }

            if (e.Key == Key.Up || e.Key == Key.Left)
            {
                Presentation.Previous();
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
            selectionDelay.Start();
        }

        protected void selectionDelay_Tick(object sender, EventArgs e)
        {
            selectionDelay.Stop();

            Presentation.GoTo(LiveList.SelectedItem as Slide);
            Presentation_SlideIndexChanged(Presentation, new SlideShowEventArgs(LiveList.SelectedIndex + 1, LiveList.SelectedIndex + 1));
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
        bool _editingTime = false;
        TimeSpan? _initEditTime = null;
        FullscreenVideo fullscreen;
        protected void ShowMedia(string path)
        {
            HideMedia();
            CurrentImage.Visibility = Visibility.Collapsed;
            VideoPanel.Visibility = Visibility.Visible;
            mediaPosTimer = new DispatcherTimer();
            mediaPosTimer.Interval = TimeSpan.FromMilliseconds(100);
            mediaPosTimer.Tick += new EventHandler(mediaPosTimer_Tick);
            VideoPlayer.Open(new Uri(path, UriKind.Absolute));
            PlayMedia(null, null);
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
            totalTime.Text = (int)VideoPlayer.NaturalDuration.TimeSpan.TotalMinutes + ":" + VideoPlayer.NaturalDuration.TimeSpan.Seconds.ToString("d2");

            if (VideoPlayer.HasVideo)
            {
                if (fullscreen == null)
                    fullscreen = new FullscreenVideo(VideoPlayer);

                VideoDisplay.Visibility = Visibility.Visible;

                double ratio = VideoDisplay.Height / VideoPlayer.NaturalVideoHeight;
                VideoDisplay.Width = VideoPlayer.NaturalVideoWidth * ratio;

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
            //on startup videodisplay is zero which we con't want to set otherwise it will never be bigger than zero
            //previewpanel is set to collapse so height can be zero while mediacontrols will still have a height
            if (VideoDisplay.ActualHeight <= 0 || MediaControls.ActualHeight > PreviewPanel.ActualHeight)
                return;

            double ratio = (PreviewPanel.ActualHeight - MediaControls.ActualHeight) / VideoDisplay.ActualHeight;
            VideoDisplay.Height *= ratio;
            VideoDisplay.Width *= ratio;
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
