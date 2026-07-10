using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Presenter.App_Code;
using Presenter.Resources;

namespace Presenter
{
    public partial class OptionsDialog : Window
    {
        //on macOS only the UNO (LibreOffice) engine exists, so the selector is hidden
        private readonly bool _engineSelectable = OperatingSystem.IsWindows();

        public OptionsDialog()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            LibraryPath.Text = Config.LibraryPathSetting;

            MonitorSelection.Items.Add(Labels.OptionsMonitorAuto);

            var screens = ScreenService.AllScreens;
            for (int i = 0; i < screens.Count; i++)
            {
                MonitorSelection.Items.Add(Labels.OptionsMonitorTitle + " " + (i + 1) + (screens[i].IsPrimary ? " [" + Labels.OptionsMonitorPrimary + "]" : ""));

                if (ScreenService.DeviceName(screens[i]) == ScreenService.DeviceName(Config.ProjectorScreen))
                    MonitorSelection.SelectedIndex = i + 1;
            }

            if (Config.UseNonPrimaryScreen)
                MonitorSelection.SelectedIndex = 0;

            if (_engineSelectable)
            {
                string libreOfficeSuffix = Engine.Uno.SofficeLocator.Find() != null ? "" : " [" + Labels.OptionsEngineMissing + "]";
                EngineSelection.Items.Add(Labels.OptionsEngineCom + (App.OfficeAvailable ? "" : " [" + Labels.OptionsEngineMissing + "]"));
                EngineSelection.Items.Add(Labels.OptionsEngineUno + libreOfficeSuffix);
                EngineSelection.SelectedIndex = Config.PreferredEngine == "uno" ? 1 : 0;
            }
            else
            {
                EngineLabel.IsVisible = false;
                EngineSelection.IsVisible = false;
            }

            InsertPresBlanks.IsChecked = Config.InsertBlankAfterPres;
            InsertVideoBlanks.IsChecked = Config.InsertBlankAfterVideo;
            ShowPreviewBottom.IsChecked = Config.SlidePreviewBottom;

            for (double i = 10; i <= 15; i++)
                FontSizeList.Items.Add(i);
            FontSizeList.SelectedItem = Config.FontSize;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Config.LibraryPath = LibraryPath.Text;

            if (MonitorSelection.SelectedIndex == 0)
                Config.UseNonPrimaryScreen = true;
            else if (MonitorSelection.SelectedIndex != -1)
            {
                Config.UseNonPrimaryScreen = false;
                Config.ProjectorScreen = ScreenService.AllScreens[MonitorSelection.SelectedIndex - 1];
            }

            if (_engineSelectable)
            {
                string engine = EngineSelection.SelectedIndex == 1 ? "uno" : "com";
                if (engine != Config.PreferredEngine)
                {
                    Config.PreferredEngine = engine;
                    if (!AppServices.Engine.IsRunning)
                        AppServices.SelectEngine(App.OfficeAvailable);
                }
            }

            Config.InsertBlankAfterPres = InsertPresBlanks.IsChecked ?? false;
            Config.InsertBlankAfterVideo = InsertVideoBlanks.IsChecked ?? false;
            Config.SlidePreviewBottom = ShowPreviewBottom.IsChecked ?? false;

            if (FontSizeList.SelectedItem != null)
                Config.FontSize = (double)FontSizeList.SelectedItem;

            (Owner as Main)?.BindLocationList();
            this.Close();
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var start = await StorageProvider.TryGetFolderFromPathAsync(Config.LibraryPath);
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Labels.OptionsLibraryBrowseDesc,
                SuggestedStartLocation = start,
                AllowMultiple = false,
            });
            if (folders.Count > 0)
                LibraryPath.Text = folders[0].Path.LocalPath;
        }
    }
}
