using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Presenter.App_Code;
using Presenter.Core;
using Presenter.Core.Abstractions;
using Presenter.Core.Models;
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

            timer.Tick += new EventHandler(timer_Tick);
            searchDelay.Tick += new EventHandler(searchDelay_Tick);
            searchDelay.Interval = TimeSpan.FromMilliseconds(300);

            //drop targets (AllowDrop + Drop were XAML attributes in WPF)
            foreach (var target in new Control[] { FileList, ScheduleList, LiveList })
            {
                DragDrop.SetAllowDrop(target, true);
                target.AddHandler(DragDrop.DropEvent, DragDrop_DropHandler);
            }

            //PreviewKeyDown / PreviewMouseLeftButton equivalents (tunnelling handlers)
            LiveList.AddHandler(KeyDownEvent, LiveList_KeyDown, RoutingStrategies.Tunnel);
            timelineSlider.AddHandler(PointerPressedEvent, timelineSlider_PointerPressed, RoutingStrategies.Tunnel);
            timelineSlider.AddHandler(PointerReleasedEvent, SeekToMediaPosition, RoutingStrategies.Tunnel);

            //row foreground for flagged slides is applied whenever a row container is
            //created or recycled (replaces the WPF ItemContainerGenerator.StatusChanged hook)
            LiveList.ContainerPrepared += (s, e) =>
            {
                if (e.Container is ListBoxItem row && e.Index >= 0 && e.Index < LiveList.Items.Count && LiveList.Items[e.Index] is Slide)
                    HightlightRow(row);
            };

            GridSplitter1.LayoutUpdated += GridSplitter_LayoutUpdated;
            GridSplitter2.LayoutUpdated += GridSplitter_LayoutUpdated;

            //screens are only known once the window is open
            Opened += (s, e) => BindLocationList();
        }

        #region menu
        protected async void Planner_Click(object sender, RoutedEventArgs e)
        {
            await OpenPlanner();
        }

        /// <summary>Opens the schedule planner dialog (dialogs are async in Avalonia).</summary>
        protected async Task OpenPlanner()
        {
            OpenDialog dialog = new OpenDialog();
            dialog.ScheduleDeleted += new EventHandler<OpenDialog.DeletedScheduleArgs>(dialog_ScheduleDeleted);
            await dialog.ShowDialog(this);

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

        protected async void Options_Click(object sender, RoutedEventArgs e)
        {
            OptionsDialog dialog = new OptionsDialog();
            await dialog.ShowDialog(this);
        }

        protected async void About_Click(object sender, RoutedEventArgs e)
        {
            AboutDialog dialog = new AboutDialog();
            await dialog.ShowDialog(this);
        }

        protected async void ReportsList_Click(object sender, RoutedEventArgs e)
        {
            ReportsListDialog dialog = new ReportsListDialog();
            await dialog.ShowDialog(this);
        }

        protected async void ReportsUsage_Click(object sender, RoutedEventArgs e)
        {
            ReportsUsageDialog dialog = new ReportsUsageDialog();
            await dialog.ShowDialog(this);
        }

        protected void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //Cmd on macOS, Ctrl elsewhere
            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
            bool ctrlr = e.Key == Key.R && ctrl;
            bool ctrlo = e.Key == Key.O && ctrl;
            bool ctrlm = e.Key == Key.M && ctrl;
            bool ctrlA = (e.Key == Key.Add || e.Key == Key.OemPlus) && ctrl;
            bool ctrlS = (e.Key == Key.Subtract || e.Key == Key.OemMinus) && ctrl;
            bool esc = e.Key == Key.Escape;

            //remote control
            if (ctrlr && !RemotePanel.IsVisible)
                RemoteMode_Click(null, null);
            else if ((ctrlr || esc) && RemotePanel.IsVisible)
            {
                RemotePanel.IsVisible = false;
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
                LocationList.SelectedItem = Config.SelectedLibrary;
                if (LocationList.SelectedIndex == -1 && LocationList.ItemCount > 0)
                    LocationList.SelectedIndex = 0;
            }

            BindFileList();
        }

        protected string GetSelectedPath()
        {
            if (LocationList.SelectedItem as string == Labels.MainRootDirName)
                return Config.LibraryPath.TrimEnd(Path.DirectorySeparatorChar);
            return Config.LibraryPath + LocationList.SelectedItem;
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
            files.AddRange(Directory.GetFiles(GetSelectedPath(), "*" + SearchTerms.Text?.Replace(" ", "*") + "*").Select(f => Path.GetFileName(f)).OrderBy(n => n));
            if (Directory.GetFiles(GetSelectedPath(), "*.pot").Any() && "none".Contains((SearchTerms.Text ?? "").ToLower()))
                files.Add("None.pot");

            if (!string.IsNullOrEmpty(SearchTerms.Text))
            {
#if WINDOWS
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
                catch (Exception) { } //windows search not available
#else
                //no Windows Search on macOS: plain recursive filename search instead
                //(relative paths so adding a match from a sub-folder still resolves)
                try
                {
                    files.AddRange(Directory
                        .GetFiles(GetSelectedPath(), "*" + SearchTerms.Text.Replace(" ", "*") + "*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(GetSelectedPath(), f)));
                }
                catch (Exception) { }
#endif
            }

            FileList.ItemsSource = files.Where(f => Config.SupportedFileTypes.Contains(Path.GetExtension(f).TrimStart('.').ToLower())).Distinct().ToArray();

            if (FileList.ItemCount > 0)
                FileList.ScrollIntoView(0);

            searchDelay.Stop();
        }

        protected void SearchTerms_TextChanged(object sender, TextChangedEventArgs e)
        {
            BindFileList();
        }

        protected void LocationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LocationList.SelectedItem != null)
                Config.SelectedLibrary = LocationList.SelectedItem.ToString();
            BindFileList();
        }

        protected async void FileSelected()
        {
            if (SelectedSchedule == null)
            {
                await OpenPlanner();
                if (SelectedSchedule == null)
                    return;
            }

            foreach (string file in FileList.SelectedItems.Cast<string>().ToArray())
                SelectedSchedule.AddItem(Path.Combine(GetSelectedPath(), file), Config.SupportedFileTypes);
            AppServices.Repository.Save(SelectedSchedule);
            BindScheduleList();

            if (Presentation != null && Presentation.IsRunning)
            {
                if (OperatingSystem.IsWindows())
                {
                    Slide s = LiveList.SelectedItem as Slide;
                    int? engineHwnd = s?.Type == SlideType.PowerPoint ? Presentation.GetSlideWindowHandle(s) : null;
                    IntPtr hwnd = engineHwnd.HasValue ? new IntPtr(engineHwnd.Value) : fullscreen.HWND;
                    User32.SetWindowPos(hwnd, new IntPtr(User32.HWND_TOPMOST), Config.ProjectorScreen.Bounds.X, Config.ProjectorScreen.Bounds.Y, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
                    var num = Presentation.Slides.Count;
                    Presentation.AddSlides(SelectedSchedule.Items.OrderBy(i => i.Ordinal).Last());
                    LiveList.ScrollIntoView(LiveList.Items.Count - 1);
                    User32.SetWindowPos(hwnd, new IntPtr(User32.HWND_NOTOPMOST), Config.ProjectorScreen.Bounds.X, Config.ProjectorScreen.Bounds.Y, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
                    ExportPreviews(num);
                }
                else
                {
                    Presentation.AddSlides(SelectedSchedule.Items.OrderBy(i => i.Ordinal).Last());
                    LiveList.ScrollIntoView(LiveList.Items.Count - 1);
                    ExportPreviews(Presentation.Slides.Count);
                }
            }
        }

        /// <summary>Exports preview thumbnails for slides from the given index on (background thread).</summary>
        private void ExportPreviews(int fromIndex)
        {
            Task.Run(() =>
            {
                Presentation.Slides.Skip(fromIndex).Where(sl => sl.Type == SlideType.PowerPoint).ForEach(sl =>
                {
                    string path = Presentation.ExportSlideImage(sl, sl.SlideIndex, "-preview", 333, 250);
                    if (!string.IsNullOrEmpty(path))
                        Dispatcher.UIThread.Post(() => { sl.Preview = new Bitmap(path); });
                });
            });
        }

        protected void OpenFile2(object sender, RoutedEventArgs e)
        {
            if (LocationList.SelectedIndex == -1)
                return;

            string filename = Path.Combine(GetSelectedPath(), FileList.SelectedItem as string ?? "");
            if (File.Exists(filename))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filename) { UseShellExecute = true });
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
            else if (e.Key == Key.Delete || e.Key == Key.Back)
                DeleteFile(null, null);
        }

        protected void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool enabled = (FileList.SelectedIndex != -1);
            AddBtn.IsEnabled = enabled;
        }

        protected void AddSelected(object sender, RoutedEventArgs e)
        {
            FileSelected();
        }

        protected void FileList_DoubleTapped(object sender, TappedEventArgs e)
        {
            //checks if a listboxitem is clicked, otherwise something like the scrollbar was clicked so don't add selected file
            var item = (e.Source as Visual).GetAncestorByType<ListBoxItem>();
            if (item != null)
                FileSelected();
        }

        protected void RefreshLocations(object sender, RoutedEventArgs e)
        {
            object value = LocationList.SelectedItem;
            BindLocationList();
            try { LocationList.SelectedItem = value; }
            catch (Exception) { }
            BindFileList();
        }

        protected async void DeleteFile(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = await App_Code.MessageBox.Show(this, Labels.MainContextDeleteConfirm, "", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                foreach (string file in FileList.SelectedItems.Cast<string>().ToArray())
                    File.Delete(Path.Combine(GetSelectedPath(), file));

                BindFileList();
            }
        }
        #endregion

        #region dragdrop
        // Application-scoped drag formats. The DataTransfer API only carries strings/bytes/files,
        // so schedule items travel as their Guid ID (resolved back to an Item on drop) and library
        // entries travel as their filename string.
        private static readonly DataFormat<string> ScheduleItemFormat = DataFormat.CreateStringApplicationFormat("ScheduleItem");
        private static readonly DataFormat<string> LibraryFileFormat = DataFormat.CreateStringApplicationFormat("LibraryFile");

        bool _dragging = false;
        private void DragDrop_PointerMoved(object sender, PointerEventArgs e)
        {
            var pressed = e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed;
            if (pressed && !_dragging)
            {
                _dragging = true;
                ListBox parent = (ListBox)sender;
                object data = parent.GetItemAtPoint(e.GetPosition(parent));
                if (data != null && ScheduleList.IsEnabled)
                {
                    var dataTransfer = new DataTransfer();
                    dataTransfer.Add(data is Item item
                        ? DataTransferItem.Create(ScheduleItemFormat, item.ID.ToString())
                        : DataTransferItem.Create(LibraryFileFormat, data.ToString()));
                    DragDrop.DoDragDropAsync(e, dataTransfer, DragDropEffects.Move);
                }
            }
            else if (!pressed && _dragging)
                _dragging = false;
        }

        /// <summary>Resolves the schedule item referenced by a drag operation (carried as its Guid ID).</summary>
        private Item DragScheduleItem(DragEventArgs e)
        {
            string id = e.DataTransfer.TryGetValue(ScheduleItemFormat);
            return Guid.TryParse(id, out Guid guid) ? SelectedSchedule.Items.FirstOrDefault(i => i.ID == guid) : null;
        }

        private void DragDrop_DropHandler(object sender, DragEventArgs e)
        {
            ListBox parent = (ListBox)sender;
            int added = 0;

            if (parent.Name == "ScheduleList" || parent.Name == "LiveList")
            {
                string data = e.DataTransfer.TryGetValue(LibraryFileFormat);
                if (!String.IsNullOrEmpty(data)) //adding new from filelist
                {
                    SelectedSchedule.AddItem(Path.Combine(GetSelectedPath(), data), Config.SupportedFileTypes);
                    AppServices.Repository.Save(SelectedSchedule);
                    added++;

                    //support insertion of presentation where mouse cursor is when dragging from filelist
                    Item source = SelectedSchedule.Items.Last();
                    Item dest = parent.GetItemAtPoint(e.GetPosition(parent)) as Item;
                    SelectedSchedule.MoveItem(source, dest);
                    AppServices.Repository.Save(SelectedSchedule);
                }
                else if (e.DataTransfer.Contains(DataFormat.File)) //add new from explorer
                {
                    string[] files = GetDroppedFiles(e);

                    foreach (string file in files)
                    {
                        if (SelectedSchedule.AddItem(file, Config.SupportedFileTypes))
                            added++;
                    }
                    AppServices.Repository.Save(SelectedSchedule);
                }
                else if (parent.Name != "LiveList") //reordering
                {
                    Item source = DragScheduleItem(e);
                    Item dest = parent.GetItemAtPoint(e.GetPosition(parent)) as Item;
                    if (source != null)
                    {
                        SelectedSchedule.MoveItem(source, dest);
                        AppServices.Repository.Save(SelectedSchedule);
                    }
                }

                if (parent.Name == "LiveList")
                    SelectedSchedule.Items.OrderBy(i => i.Ordinal).Skip(SelectedSchedule.Items.Count - added).ForEach(i => Presentation.AddSlides(i));

                BindScheduleList();
            }
            else //if (parent.Name == "FileList")
            {
                Item data = DragScheduleItem(e);
                if (data != null) //removing item from schedule
                {
                    SelectedSchedule.RemoveItem(data);
                    AppServices.Repository.Save(SelectedSchedule);
                    BindScheduleList();
                }
                else if (e.DataTransfer.Contains(DataFormat.File)) //add files from explorer to library
                {
                    string[] files = GetDroppedFiles(e);

                    foreach (string file in files)
                        File.Copy(file, Path.Combine(GetSelectedPath(), Path.GetFileName(file)));

                    BindFileList();
                }
            }
        }

        /// <summary>Files dropped from the OS shell, directories expanded, filtered to supported types.</summary>
        private static string[] GetDroppedFiles(DragEventArgs e)
        {
            string[] files = e.DataTransfer.TryGetFiles()?.Select(f => f.Path.LocalPath).ToArray() ?? [];

            //expand directories to include all files within
            files = files.Union(files.Where(f => Directory.Exists(f)).SelectMany(d => Directory.GetFiles(d))).ToArray();

            //filter out invalid files
            return files.Where(f => Config.SupportedFileTypes.Contains(Path.GetExtension(f).ToLower().TrimStart('.'))).ToArray();
        }
        #endregion

        #region order_of_presentions
        protected void BindScheduleList()
        {
            ScheduleList.ItemsSource = SelectedSchedule.Items.OrderBy(i => i.Ordinal).ToArray();
        }

        protected async void OpenFile(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedIndex == -1)
                return;

            Item item = ScheduleList.SelectedItem as Item;

            if (item.IsTemplateNone)
                return;

            if (!item.IsFound)
            {
                await App_Code.MessageBox.Show(this, Labels.MainMessageFileNotFound);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.Filename) { UseShellExecute = true });
        }

        protected async void OpenLocation(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedIndex == -1)
                return;

            string path = System.IO.Path.GetDirectoryName(Util.NormalizeSeparators((ScheduleList.SelectedItem as Item).Filename));
            if (!Directory.Exists(path))
            {
                await App_Code.MessageBox.Show(this, Labels.MainMessageFolderNotFound);
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
            SelectedSchedule.RemoveItems(ScheduleList.SelectedItems.Cast<Item>().ToArray());
            AppServices.Repository.Save(SelectedSchedule);
            BindScheduleList();
            idx = Math.Min(idx, ScheduleList.ItemCount - 1);
            ScheduleList.SelectedIndex = idx;
        }

        protected void DuplicateFile(object sender, RoutedEventArgs e)
        {
            var selected = ScheduleList.SelectedItem;
            foreach (Item item in ScheduleList.SelectedItems.Cast<Item>().ToArray())
            {
                SelectedSchedule.AddItem(item.Filename, Config.SupportedFileTypes);
                SelectedSchedule.MoveItem(SelectedSchedule.Items.Last(), item);
            }
            AppServices.Repository.Save(SelectedSchedule);
            BindScheduleList();
            ScheduleList.SelectedItem = selected;
        }

        private void ScheduleList_DoubleTapped(object sender, TappedEventArgs e)
        {
            OpenFile(null, null);
        }

        protected void ScheduleList_KeyUp(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Delete || e.Key == Key.Back) && ScheduleList.SelectedItem != null)
                RemoveFile(null, null);
        }

        private void Up_Click(object sender, RoutedEventArgs e)
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
            (ScheduleList.Items[item.Ordinal] as Item).Ordinal++;
            AppServices.Repository.Save(SelectedSchedule);

            BindScheduleList();
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            if (Presentation != null && Presentation.IsRunning)
            {
                Presentation.Next(LiveList.SelectedItem as Slide);
                return;
            }

            Item item = ScheduleList.SelectedItem as Item;
            if (item == null || item.Ordinal == ScheduleList.ItemCount - 1)
                return;

            item.Ordinal++;
            (ScheduleList.Items[item.Ordinal] as Item).Ordinal--;
            AppServices.Repository.Save(SelectedSchedule);

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
        protected async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSchedule == null)
            {
                await OpenPlanner();
                if (SelectedSchedule == null)
                    return;
            }

            if (SelectedSchedule.Items.Count == 0)
            {
                await App_Code.MessageBox.Show(this, Labels.MainNoAddedItems);
                return;
            }

            if (ScreenService.AllScreens.Count == 1)
            {
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        new System.Diagnostics.Process { StartInfo = { FileName = "DisplaySwitch.exe", Arguments = "/extend", UseShellExecute = true } }.Start();
                    }
                    catch { }
                }

                if (ScreenService.AllScreens.Count == 1)
                    await App_Code.MessageBox.Show(this, Labels.AppRequiresExtendedDesktop);
            }

            if (Config.UseNonPrimaryScreen && ScreenService.DeviceName(Config.ProjectorScreen) == ScreenService.DeviceName(Config.PrimaryScreen) && ScreenService.AllScreens.Count > 1)
                Config.ProjectorScreen = ScreenService.AllScreens.First(s => !s.IsPrimary);

            fullscreen = new FullscreenWindow();
            StartBtn.IsVisible = false;
            StopBtn.IsVisible = true;
            Interval.IsVisible = true;
            UseSlideTimings.IsVisible = true;
            TimerBtn.IsVisible = true;
            Expander1.IsVisible = true;
            ScheduleList.IsVisible = false;
            PreviewPanel.IsVisible = true;
            LivePanel.IsVisible = true;
            LiveList.SelectedIndex = 0;
            PrevBtn.Content = Labels.MainBtnPrev;
            NextBtn.Content = Labels.MainBtnNext;
            RefreshBtn.IsVisible = false;
            RemoveBtn.IsVisible = false;
            LocationList.Margin = new Thickness(81, 46, 17, 0);
            PrevBtn.IsEnabled = true;
            NextBtn.IsEnabled = true;
            Interval.Text = Config.TimerInterval.ToString();
            UseSlideTimings.IsChecked = Config.UseSlideTimings;
            SetPreview(PreviewImage, null); //set preview to blank slide, otherwise intitally it will be white
            col1.Width = new GridLength(220, GridUnitType.Pixel);

            SetPreviewPosition();
            Config.instance.SlidePreviewBottomChanged += new EventHandler(instance_SlidePreviewBottomChanged);

            if (Presentation != AppServices.Engine) //engine can be swapped in the options dialog
            {
                if (Presentation != null)
                {
                    Presentation.SlideIndexChanged -= new EventHandler<SlideIndexChangedEventArgs>(Presentation_SlideIndexChanged);
                    Presentation.SlideShowEnd -= new EventHandler(Presentation_SlideShowEnd);
                    Presentation.SlideAdded -= new EventHandler<SlideAddedEventArgs>(Presentation_SlideAdded);
                }
                Presentation = AppServices.Engine;
                Presentation.SlideIndexChanged += new EventHandler<SlideIndexChangedEventArgs>(Presentation_SlideIndexChanged);
                Presentation.SlideShowEnd += new EventHandler(Presentation_SlideShowEnd);
                Presentation.SlideAdded += new EventHandler<SlideAddedEventArgs>(Presentation_SlideAdded);
            }

            var schedule = SelectedSchedule;
            _ = Task.Run(() => Presentation.Start(schedule))
                .ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            Stop_Click(null, null);
                            if (progress != null) { progress.Close(); progress = null; }
                            App.LogError(t.Exception);
                            _ = App_Code.MessageBox.Show(this, t.Exception.GetBaseException().Message, "Presenter");
                        });
                        return;
                    }
                    SlideShowStarted();
                });
            progress = new BuildProgress();
            await progress.ShowDialog(this);
        }

        protected void SlideShowStarted()
        {
            if (progress == null)
                return;

            Dispatcher.UIThread.Invoke(() =>
            {
                LiveList.SelectedIndex = 0;
                if (progress != null)
                {
                    progress.Close();
                    progress = null;
                }
                fullscreen.Topmost = false;
            });

            ExportPreviews(0);
        }

        protected void instance_SlidePreviewBottomChanged(object sender, EventArgs e)
        {
            SetPreviewPosition();
        }

        protected void SetPreviewPosition()
        {
            if (!Config.SlidePreviewBottom)
            {
                col3.Width = new GridLength(350, GridUnitType.Pixel);
                Grid.SetRowSpan(LivePanel, 2);
                Grid.SetColumn(PreviewPanel, 2);
                Grid.SetRow(PreviewPanel, 1);
                Grid.SetRowSpan(PreviewPanel, 2);
                PreviewPanel.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                PreviewPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
                PreviewPanel.Orientation = Avalonia.Layout.Orientation.Vertical;
                PreviewPanel.MaxHeight = Double.PositiveInfinity;
                //no fixed width cap: the side preview grows with the column and is bounded by
                //the column height instead (see GridSplitter_LayoutUpdated)
                PreviewPanel.MaxWidth = Double.PositiveInfinity;
                PreviewPanel.Height = Double.NaN;
                PreviewPanel.Margin = new Thickness(0, 80, 10, 0);
                PreviewImage.Margin = new Thickness(0, 0, 0, 20);
                LivePanel.Margin = new Thickness(12, 80, 12, 46);
                GridSplitter1.IsVisible = false;
                GridSplitter2.IsVisible = true;
            }
            else
            {
                col3.Width = new GridLength(0, GridUnitType.Pixel);
                Grid.SetRowSpan(LivePanel, 1);
                Grid.SetColumn(PreviewPanel, 1);
                Grid.SetRow(PreviewPanel, 2);
                Grid.SetRowSpan(PreviewPanel, 1);
                PreviewPanel.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
                PreviewPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                PreviewPanel.Orientation = Avalonia.Layout.Orientation.Horizontal;
                //no fixed height cap: the bottom preview grows with the divider and is bounded by
                //the column width instead (see GridSplitter_LayoutUpdated)
                PreviewPanel.MaxHeight = Double.PositiveInfinity;
                PreviewPanel.MaxWidth = Double.PositiveInfinity;
                PreviewPanel.Width = Double.NaN;
                PreviewPanel.Margin = new Thickness(12, 10, 0, 45);
                PreviewImage.Margin = new Thickness(0, 0, 20, 0);
                LivePanel.Margin = new Thickness(12, 80, 12, 10);
                GridSplitter1.IsVisible = true;
                GridSplitter2.IsVisible = false;
            }
        }

        protected void SlideRow_PointerEntered(object sender, PointerEventArgs e)
        {
            if ((sender as Control)?.DataContext is Slide sl)
                SetPreview(PreviewImage, sl.Preview as Bitmap);
        }

        protected void SlideRow_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            //track the row under the pointer for the Edit context menu (WPF did this in
            //PreviewMouseRightButtonDown to keep right-click from changing the selection)
            if (e.GetCurrentPoint(sender as Visual).Properties.IsRightButtonPressed)
                _selectedSlide = (sender as Control)?.DataContext as Slide;
        }

        protected void Stop_Click(object sender, RoutedEventArgs e)
        {
            StartBtn.IsVisible = true;
            StopBtn.IsVisible = false;
            Interval.IsVisible = false;
            UseSlideTimings.IsVisible = false;
            TimerBtn.IsVisible = false;
            Expander1.IsVisible = false;
            PreviewPanel.IsVisible = false;
            GridSplitter1.IsVisible = false;
            GridSplitter2.IsVisible = false;
            LibraryGrid.IsVisible = true;
            Expander1.Content = "<";
            col3.Width = new GridLength(0, GridUnitType.Pixel);
            col1.Width = new GridLength((this.Bounds.Width - 20) / 2, GridUnitType.Pixel);
            ScheduleList.IsVisible = true;
            LivePanel.IsVisible = false;
            PrevBtn.Content = Labels.MainBtnMoveUp;
            NextBtn.Content = Labels.MainBtnMoveDown;
            if (ScheduleList.SelectedIndex == -1)
            {
                PrevBtn.IsEnabled = false;
                NextBtn.IsEnabled = false;
            }
            Config.TimerInterval = Util.Parse<int>(Interval.Text);
            RefreshBtn.IsVisible = true;
            RemoveBtn.IsVisible = true;
            LocationList.Margin = new Thickness(81, 46, 80, 0);
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
            Dispatcher.UIThread.Invoke(() =>
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

                LiveList.Items.Add(e.NewSlide);
                //flag highlighting is applied by the ContainerPrepared handler once the row materializes
            });
        }

        protected void Presentation_SlideShowEnd(object sender, EventArgs e)
        {
            //if triggered by powerpoint slideshow being closed not thru presenter, execute stop down on background thread to prevent freeze
            if (Presentation != null && Presentation.IsRunning)
                Dispatcher.UIThread.Post(() => Stop_Click(null, null), DispatcherPriority.Background);
        }

        private int previdx = 0;
        protected void Presentation_SlideIndexChanged(object sender, SlideIndexChangedEventArgs e)
        {
            int idx = e.NewIndex - 1;

            //if oldindex is -1, triggered by powerpoint change slide event so don't proceed unless new index does not match
            //current livelist selected index to only run when slideshow is using timings to automatically advance
            if (e.OldIndex == -1 && LiveList.SelectedIndex == idx)
                return;

            if (e.OldIndex != e.NewIndex)
            {
                LiveList.SelectionChanged -= LiveList_SelectionChanged;
                LiveList.SelectedIndex = idx;
                LiveList.SelectionChanged += LiveList_SelectionChanged;
            }

            if (Presentation.Slides[idx].EngineData != Presentation.Slides[previdx].EngineData)
            {
                if (Presentation.Slides[previdx].EngineData != null)
                    Presentation.Reset(Presentation.Slides[previdx]);
            }

            if (Presentation.Slides.Count > idx && idx >= 0 && Presentation.Slides[idx].Type != SlideType.PowerPoint)
            {
                ShowMedia(Presentation.Slides[idx]);
                if (OperatingSystem.IsWindows())
                    User32.SetWindowPos(fullscreen.HWND, new IntPtr(User32.HWND_TOP), Config.ProjectorScreen.Bounds.X, Config.ProjectorScreen.Bounds.Y, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
            }
            else
            {
                HideMedia();
                fullscreen.HideWindow();
                Presentation.BringToFront(Presentation.Slides[idx]);

                if (idx == -1)
                    return;

                if (Presentation.Slides.Count > idx)
                    SetPreview(CurrentImage, Presentation.Slides[idx].Preview as Bitmap);
            }

            //autoscroll
            if (idx > previdx)
                LiveList.ScrollIntoView(Math.Min(LiveList.Items.Count - 1, idx + 5));
            else
                LiveList.ScrollIntoView(Math.Max(0, idx - 5));
            previdx = idx;
        }

        protected void SetPreview(Border preview, Bitmap image)
        {
            if (image == null)
            {
                preview.Background = new SolidColorBrush(Config.ScreenBlankColour);
                preview.BorderThickness = new Thickness(0);
                return;
            }
            preview.Background = new ImageBrush(image) { Stretch = Stretch.Fill };

            //letterbox by border thickness (background brush fills within the border)
            var widthRatio = image.PixelSize.Width / Math.Max(1.0, preview.Bounds.Width);
            var heightRatio = image.PixelSize.Height / Math.Max(1.0, preview.Bounds.Height);
            if (widthRatio > heightRatio)
            {
                var y = (preview.Bounds.Height - image.PixelSize.Height / widthRatio) / 2.0;
                preview.BorderThickness = new Thickness(0, y, 0, y);
            }
            else
            {
                var x = (preview.Bounds.Width - image.PixelSize.Width / heightRatio) / 2.0;
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
                if (fullscreen.IsVisible)
                    fullscreen.HideWindow();
                else
                    fullscreen.ShowBlank();
                this.Activate();
                e.Handled = true;
            }

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

            if (slide.JumpIndex.HasValue && slide.JumpIndex > 0 && slide.JumpIndex <= Presentation.Slides.Count)
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

            Presentation_SlideIndexChanged(Presentation, new SlideIndexChangedEventArgs(LiveList.SelectedIndex + 1, LiveList.SelectedIndex + 1));

            if ((LiveList.SelectedItem as Slide).Type == SlideType.PowerPoint)
            {
                var slide = LiveList.SelectedItem as Slide;
                //place in background thread otherwise if this method takes longer due to slide animations the next item the mouse is over once finished is selected
                Task.Run(() => Presentation.GoTo(slide));
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
                LibraryGrid.IsVisible = false;
            }
            else
            {
                newWidth = 220;
                btn.Content = "<";
                LibraryGrid.IsVisible = true;
            }

            col1.Width = new GridLength(newWidth, GridUnitType.Pixel);
        }

        private void HightlightRow(object sender, RoutedEventArgs e)
        {
            ListBoxItem row = (sender as Button).GetAncestorByType<ListBoxItem>();
            Slide slide = row.DataContext as Slide;
            if (slide.Type == SlideType.Blank)
                return;

            Flag flag = slide.ScheduleItem.Flags.FirstOrDefault(f => f.Index == slide.ItemIndex);

            if (flag == null)
                slide.ScheduleItem.Flags.Add(new Flag() { ItemID = slide.ScheduleItem.ID, Index = (short)slide.ItemIndex, Colour = "Red" });
            else
                slide.ScheduleItem.Flags.Remove(flag);
            AppServices.Repository.SaveChanges();

            HightlightRow(row);
        }

        private void HightlightRow(ListBoxItem row)
        {
            if (row.DataContext is not Slide slide || slide.ScheduleItem == null)
                return;

            Flag flag = slide.ScheduleItem.Flags.FirstOrDefault(f => f.Index == slide.ItemIndex);
            if (flag != null)
                row.Foreground = new SolidColorBrush(flag.SystemColor());
            else
                row.ClearValue(ForegroundProperty);
        }

        private Slide _selectedSlide = null;
        protected async void EditPres(object sender, RoutedEventArgs e)
        {
            if (_selectedSlide == null || _selectedSlide.Type != SlideType.PowerPoint || !Presentation.TryEditSlide(_selectedSlide))
            {
                await App_Code.MessageBox.Show(this, Labels.MainContextEditError);
                return;
            }
        }

        protected void LiveList_DoubleTapped(object sender, TappedEventArgs e)
        {
            e.Handled = true; //prevent double click from selecting two different slides in quick succession if list auto scrolls in between
        }

        protected void UseSlideTimingsChanged(object sender, RoutedEventArgs e)
        {
            if (!UseSlideTimings.IsVisible)
                return;
            Config.UseSlideTimings = UseSlideTimings.IsChecked ?? false;
            if (Presentation != null)
                Presentation.UpdateSlideTimings();
        }

        #endregion

        #region message_box
        Window messageBox = null;
        protected async void ShowMessage(object sender, RoutedEventArgs e)
        {
            if (messageBox != null)
            {
                messageBox.Close();
                messageBox = null;
                return;
            }

            ScreenMessage prompt = new ScreenMessage();
            prompt.ShowInTaskbar = false;
            prompt.Closed += (sen, args) =>
            {
                if ((sen as ScreenMessage).MessageBox == null)
                    messageBox = null;
            };
            await prompt.ShowDialog(this);
            messageBox = prompt.MessageBox;
        }
        #endregion

        #region remote_ctrl
        private async void RemoteMode_Click(object sender, RoutedEventArgs e)
        {
            if (Presentation == null || !Presentation.IsRunning)
            {
                await App_Code.MessageBox.Show(this, Labels.MainRemoteNotStart);
                return;
            }

            RemotePanel.IsVisible = true;
            LiveList.Focus(); //cause livelist to focus so that up or down arrow keys changes slides

            //confine the cursor to the remote panel (Windows only; macOS has no
            //equivalent of ClipCursor, so the panel relies on its click handlers alone)
            if (OperatingSystem.IsWindows())
            {
                var topLeft = RemotePanel.TranslatePoint(new Point(0, 0), this) ?? default;
                var origin = this.PointToScreen(topLeft);
                double scale = RenderScaling;

                //1px border to prevent mouse from being able to select a slide as it can on some pcs
                CaptureCursor(origin.X + 1, origin.Y + 1, (int)(RemotePanel.Bounds.Width * scale) - 2, (int)(RemotePanel.Bounds.Height * scale) - 2);
            }
        }

        private void RemotePanel_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var props = e.GetCurrentPoint(RemotePanel).Properties;
            if (props.IsRightButtonPressed)
                Up_Click(null, null);
            else if (props.IsLeftButtonPressed)
                Down_Click(null, null);
            e.Handled = true;
        }

        User32.Rect BoundRect;
        User32.Rect OldRect;

        private void ReleaseCursor()
        {
            if (OperatingSystem.IsWindows())
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
        LibVLCSharp.Shared.MediaPlayer VideoPlayer;
        //muted second player mirroring the projector video in the operator window
        //(a libvlc player has a single video output, so the projector's cannot be shared)
        LibVLCSharp.Shared.MediaPlayer PreviewPlayer;
        const long PreviewDriftToleranceMs = 300;

        /// <summary>Creates the libvlc media players on first use; false when libvlc is unavailable.</summary>
        private bool EnsureMediaPlayer()
        {
            if (VideoPlayer != null)
                return true;
            if (!Vlc.TryInitialize())
                return false;

            VideoPlayer = new LibVLCSharp.Shared.MediaPlayer(Vlc.Instance);
            //libvlc events fire on libvlc threads: marshal, and never call back into
            //the player from inside the event handler itself
            VideoPlayer.EndReached += (s, e) => Dispatcher.UIThread.Post(() => VideoPlayer_MediaEnded(s, EventArgs.Empty));
            VideoPlayer.LengthChanged += (s, e) => Dispatcher.UIThread.Post(() => Media_LengthChanged(e.Length));

            PreviewPlayer = new LibVLCSharp.Shared.MediaPlayer(Vlc.Instance) { Mute = true };
            PreviewVideo.MediaPlayer = PreviewPlayer;
            return true;
        }

        protected async void ShowMedia(Slide slide)
        {
            HideMedia();
            if (slide.Type == SlideType.Blank)
            {
                SetPreview(CurrentImage, null);
                fullscreen.ShowBlank();
                this.Activate();
            }
            else if (slide.Type == SlideType.Image)
            {
                SetPreview(CurrentImage, slide.Preview as Bitmap);
                fullscreen.Show(slide.Image as Bitmap);
                this.Activate();
            }
            else
            {
                if (!EnsureMediaPlayer())
                {
                    //no libvlc found: leave the projector blank rather than failing the show
                    fullscreen.ShowBlank();
                    this.Activate();
                    await App_Code.MessageBox.Show(this, "Video/audio playback requires VLC (libvlc). Install VLC from videolan.org and restart Presenter.");
                    return;
                }

                if (slide.Type == SlideType.Audio)
                {
                    fullscreen.ShowBlank();
                    this.Activate();
                }
                else
                {
                    fullscreen.ShowVideo(VideoPlayer);
                    this.Activate(); //retain focus in Main window and not in shown fullscreen
                }

                CurrentImage.IsVisible = false;
                VideoPanel.IsVisible = true;
                VideoDisplay.IsVisible = (slide.Type == SlideType.Video);
                mediaPosTimer = new DispatcherTimer();
                mediaPosTimer.Interval = TimeSpan.FromMilliseconds(100);
                mediaPosTimer.Tick += new EventHandler(mediaPosTimer_Tick);
                using var media = new LibVLCSharp.Shared.Media(Vlc.Instance, new Uri(Path.GetFullPath(slide.Filename)));
                VideoPlayer.Media = media;
                if (slide.Type == SlideType.Video)
                {
                    //no-audio skips decoding the preview's audio track entirely
                    using var previewMedia = new LibVLCSharp.Shared.Media(Vlc.Instance, new Uri(Path.GetFullPath(slide.Filename)));
                    previewMedia.AddOption(":no-audio");
                    PreviewPlayer.Media = previewMedia;
                }
                PlayMedia();
            }
        }

        protected void VideoPlayer_MediaEnded(object sender, EventArgs e)
        {
            SetPlayPauseIcon(false);
            mediaPosTimer?.Stop();

            if (!_mediaAdvanceOnComplete)
                return;

            Slide slide = Presentation.Slides[LiveList.SelectedIndex];
            if (slide.JumpIndex.HasValue && slide.JumpIndex > 0 && slide.JumpIndex <= Presentation.Slides.Count)
                LiveList.SelectedIndex = slide.JumpIndex.Value - 1;
            else if (LiveList.Items.Count > LiveList.SelectedIndex)
                LiveList.SelectedIndex++;
        }

        protected void HideMedia()
        {
            if (VideoPanel.IsVisible)
            {
                CurrentImage.IsVisible = true;
                VideoPanel.IsVisible = false;
                VideoDisplay.IsVisible = false;
                StopMedia(null, null);
                mediaPosTimer = null;
            }
        }

        protected void PlayMedia()
        {
            //Play/Stop must not run on the UI thread while a libvlc callback is in
            //flight; ThreadPool keeps it deadlock-free (recommended LibVLCSharp usage)
            var player = VideoPlayer;
            var preview = VideoDisplay.IsVisible ? PreviewPlayer : null;
            Task.Run(() =>
            {
                player.Play();
                preview?.Play();
            });
            VideoPlayer.Volume = (int)(volumeSlider.Value * 100);
            SetPlayPauseIcon(true);
            mediaPosTimer?.Start();
        }

        private void SetPlayPauseIcon(bool playing)
        {
            PlayIcon.IsVisible = !playing;
            PauseIcon.IsVisible = playing;
            ToolTip.SetTip(PlayPauseBtn, playing ? Labels.MainBtnVideoPause : Labels.MainBtnVideoPlay);
        }

        protected void PlayPauseMedia(object sender, RoutedEventArgs args)
        {
            if (VideoPlayer == null)
                return;

            if (VideoPlayer.IsPlaying)
            {
                VideoPlayer.SetPause(true);
                PreviewPlayer?.SetPause(true);
                SetPlayPauseIcon(false);
                mediaPosTimer?.Stop();
            }
            else
            {
                PlayMedia();
            }
        }

        protected void StopMedia(object sender, RoutedEventArgs args)
        {
            if (VideoPlayer == null)
                return;

            var player = VideoPlayer;
            var preview = PreviewPlayer;
            Task.Run(() =>
            {
                player.Stop();
                preview?.Stop();
            });
            SetPlayPauseIcon(false);
            mediaPosTimer?.Stop();
            //Stop rewinds the media, so return the transport to the start (the
            //ValueChanged handler mirrors 0 into the currentTime box)
            timelineSlider.Value = 0;
        }

        protected void ChangeMediaVolume(object sender, RangeBaseValueChangedEventArgs args)
        {
            if (VideoPlayer != null)
                VideoPlayer.Volume = (int)(volumeSlider.Value * 100);
        }

        protected void Media_LengthChanged(long lengthMs)
        {
            var duration = TimeSpan.FromMilliseconds(lengthMs);
            timelineSlider.Maximum = Math.Max(1, lengthMs);
            totalTime.Text = (int)duration.TotalMinutes + ":" + duration.Seconds.ToString("d2");
        }

        protected void SeekToMediaPosition(object sender, PointerReleasedEventArgs args)
        {
            if (VideoPlayer != null)
            {
                VideoPlayer.Time = (long)timelineSlider.Value;
                SyncPreviewTime();
            }
            _timeDragging = false;
        }

        private void timelineSlider_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _timeDragging = true;
        }

        protected void mediaPosTimer_Tick(object sender, EventArgs e)
        {
            if (!_timeDragging && VideoPlayer != null)
                timelineSlider.Value = VideoPlayer.Time;

            //re-align the operator-window mirror when it drifts from the projector player
            if (VideoPlayer != null && PreviewPlayer != null && VideoPlayer.IsPlaying && PreviewPlayer.IsPlaying
                && Math.Abs(PreviewPlayer.Time - VideoPlayer.Time) > PreviewDriftToleranceMs)
                PreviewPlayer.Time = VideoPlayer.Time;
        }

        private void timelineSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_editingTime)
            {
                TimeSpan draggedVal = TimeSpan.FromMilliseconds(timelineSlider.Value);
                currentTime.Text = (int)draggedVal.TotalMinutes + ":" + draggedVal.Seconds.ToString("d2");
            }
        }

        private void currentTime_GotFocus(object sender, GotFocusEventArgs e)
        {
            _editingTime = true;
            _initEditTime = getCurrentTime();
        }

        private void currentTime_LostFocus(object sender, RoutedEventArgs e)
        {
            _editingTime = false;

            TimeSpan? value = getCurrentTime();

            //if entered value is valid and the time has changed (presume they would have entered a new value if the user wanted to jump to a time)
            if (value.HasValue && value != _initEditTime && VideoPlayer != null)
            {
                VideoPlayer.Time = (long)value.Value.TotalMilliseconds;
                SyncPreviewTime();
            }
        }

        //seeks must sync the mirror directly: while paused the drift-correcting timer isn't running
        private void SyncPreviewTime()
        {
            if (PreviewPlayer != null && VideoDisplay.IsVisible)
                PreviewPlayer.Time = VideoPlayer.Time;
        }

        private TimeSpan? getCurrentTime()
        {
            var val = (currentTime.Text ?? "").Split(new char[] { ':', '.' }, StringSplitOptions.RemoveEmptyEntries).ToArray();

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
                SizeSidePreview();
                return;
            }

            //Bottom layout: the two previews sit side by side (image left, video right), so the
            //preview is *width*-bounded, not height-capped: it grows with the divider dragged up
            //until the two boxes plus the gap between them fill the column width, then it stops.
            //(Sizes are compared against the last-set Width/Height, not the lagging Bounds, so
            //the feedback loop settles rather than aborting with "Infinite layout loop".)

            //the side layout pins VideoPanel to the box width; let it size to content here
            if (!double.IsNaN(VideoPanel.Width))
                VideoPanel.Width = double.NaN;

            if (PreviewImage.Width < 1.0 || PreviewImage.Height < 1.0)
                return;

            //vertical room freed up by dragging the divider (row 2 height, less the panel's
            //10px top and 45px bottom margins with a little slack)
            double dividerH = Math.Max(0, Grid1.RowDefinitions[2].ActualHeight - 60);

            //cap the box height so the two side-by-side boxes still fit the column width: each
            //box's width is boxHeight / aspect, so their combined width is boxHeight*(1/imgAspect
            //+ 1/secondAspect). The right box is the video when one is showing, else the image.
            double imgAspect = PreviewImage.Height / PreviewImage.Width;
            bool video = VideoDisplay.IsVisible && VideoDisplay.Width >= 1.0 && VideoDisplay.Height >= 1.0;
            double secondAspect = video ? VideoDisplay.Height / VideoDisplay.Width : imgAspect;
            double widthForBoxes = Grid1.ColumnDefinitions[1].ActualWidth - 12 - 12 - 20;
            if (widthForBoxes < 1.0 || dividerH < 1.0)
                return;
            double widthCap = widthForBoxes / (1.0 / imgAspect + 1.0 / secondAspect);

            double availH = Math.Min(dividerH, widthCap);
            double availW = double.PositiveInfinity;
            PreviewPanel.Height = availH;

            if (availH < 1.0)
                return;

            //fit the slide previews within (availW, availH) keeping their aspect ratio
            double aspect = PreviewImage.Height / PreviewImage.Width;
            double imgW = availW;
            double imgH = imgW * aspect;
            if (imgH > availH)
            {
                imgH = availH;
                imgW = imgH / aspect;
            }
            if (imgW >= 1.0 && Math.Abs(PreviewImage.Width - imgW) >= 1.0)
            {
                PreviewImage.Width = imgW;
                PreviewImage.Height = imgW * aspect;

                CurrentImage.Width = imgW;
                CurrentImage.Height = imgW * aspect;
            }

            //scale the video into the same box, less the controls' height so they are never
            //clipped (the fixed-size video would otherwise push them past the viewing bound)
            FitVideoDisplay(availW, availH - MediaControls.Bounds.Height - 4);
        }

        /// <summary>
        /// Sizes the side (column 2) preview, where the two previews stack vertically and share
        /// the column's height. Both boxes — and the media controls under the video — are given
        /// one common width so their left/right edges line up; that width grows with the dragged
        /// column until the two boxes (plus the gap between them and the controls) fill the
        /// divider's vertical span, then it stops. The span runs from the panel's 80px top margin
        /// to GridSplitter2's 45px bottom margin. Sizes are compared against the last-set
        /// Width/Height rather than the lagging Bounds so the layout loop settles.
        /// </summary>
        private void SizeSidePreview()
        {
            if (Grid1.ColumnDefinitions[2].ActualWidth == 0)
                return;

            double columnW = Math.Max(0, Grid1.ColumnDefinitions[2].ActualWidth - 20);
            PreviewPanel.Width = columnW;

            if (columnW < 1.0 || PreviewImage.Width < 1.0 || PreviewImage.Height < 1.0)
                return;

            //aspect (height / width) of each stacked box; the lower box is the video when one is
            //showing, otherwise the "current slide" image
            double imgAspect = PreviewImage.Height / PreviewImage.Width;
            bool video = VideoDisplay.IsVisible && VideoDisplay.Width >= 1.0 && VideoDisplay.Height >= 1.0;
            double lowerAspect = video ? VideoDisplay.Height / VideoDisplay.Width : imgAspect;
            double controls = video ? MediaControls.Bounds.Height + 4 : 0;

            double span = Grid1.RowDefinitions[1].ActualHeight + Grid1.RowDefinitions[2].ActualHeight - 80 - 45;
            double budget = span - 20 - controls;
            if (budget < 1.0)
                return;

            //one width shared by both boxes, capped so their combined height fits the span
            double w = Math.Min(columnW, budget / (imgAspect + lowerAspect));
            if (w < 1.0)
                return;

            if (Math.Abs(PreviewImage.Width - w) >= 1.0)
            {
                PreviewImage.Width = w;
                PreviewImage.Height = w * imgAspect;

                CurrentImage.Width = w;
                CurrentImage.Height = w * imgAspect;
            }
            if (video)
            {
                if (Math.Abs(VideoDisplay.Width - w) >= 1.0)
                {
                    VideoDisplay.Width = w;
                    VideoDisplay.Height = w * lowerAspect;
                }
                //VideoPanel wraps the video box and the transport controls; pin it to the same
                //width so the controls line up with the box instead of stretching to the column
                if (double.IsNaN(VideoPanel.Width) || Math.Abs(VideoPanel.Width - w) >= 1.0)
                    VideoPanel.Width = w;
            }
        }

        /// <summary>
        /// Scales VideoDisplay to fit within the given width/height while keeping its
        /// current aspect ratio (libvlc letterboxes the picture inside it). Like the image
        /// resize above, it compares against the last-set Width/Height rather than the lagging
        /// Bounds so the layout loop settles instead of aborting with "Infinite layout loop".
        /// </summary>
        private void FitVideoDisplay(double availWidth, double availHeight)
        {
            if (!VideoDisplay.IsVisible || VideoDisplay.Width < 1.0 || VideoDisplay.Height < 1.0
                || availWidth < 1.0 || availHeight < 1.0)
                return;

            double scale = Math.Min(availWidth / VideoDisplay.Width, availHeight / VideoDisplay.Height);
            double newW = VideoDisplay.Width * scale;
            double newH = VideoDisplay.Height * scale;
            if (newW < 1.0 || newH < 1.0)
                return;
            if (Math.Abs(VideoDisplay.Width - newW) < 1.0 && Math.Abs(VideoDisplay.Height - newH) < 1.0)
                return;

            VideoDisplay.Width = newW;
            VideoDisplay.Height = newH;
        }
        #endregion

        protected void Main_Closed(object sender, EventArgs e)
        {
            if (fullscreen != null)
                fullscreen.Close();

            if (VideoPlayer != null)
            {
                var player = VideoPlayer;
                VideoPlayer = null;
                player.Stop();
                player.Dispose();
            }

            if (PreviewPlayer != null)
            {
                var preview = PreviewPlayer;
                PreviewPlayer = null;
                preview.Stop();
                preview.Dispose();
            }

            if (Presentation != null)
            {
                Presentation.Stop();
                Presentation.Quit();
            }

            Environment.Exit(0);
        }

        //properties
        public Schedule SelectedSchedule { get; set; }
        protected IPresentationEngine Presentation { get; set; }

        //named ColumnDefinitions are not generated as fields by Avalonia (only controls are)
        private ColumnDefinition col1 => Grid1.ColumnDefinitions[0];
        private ColumnDefinition col3 => Grid1.ColumnDefinitions[2];
    }
}
