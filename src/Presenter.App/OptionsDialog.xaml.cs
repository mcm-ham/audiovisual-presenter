using System.Windows;
using System.Windows.Media;
using Presenter.App_Code;
using Presenter.Resources;
using Screen = System.Windows.Forms.Screen;

namespace Presenter
{
    public partial class OptionsDialog : Window
    {
        public OptionsDialog()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            LibraryPath.Text = Config.LibraryPathSetting;

            MonitorSelection.Items.Add(Labels.OptionsMonitorAuto);

            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                MonitorSelection.Items.Add(Labels.OptionsMonitorTitle + " " + (i + 1) + (Screen.AllScreens[i].Primary ? " [" + Labels.OptionsMonitorPrimary + "]" : ""));

                if (Screen.AllScreens[i].DeviceName == Config.ProjectorScreen.DeviceName)
                    MonitorSelection.SelectedIndex = i + 1;
            }

            if (Config.UseNonPrimaryScreen)
                MonitorSelection.SelectedIndex = 0;

            EngineSelection.Items.Add(Labels.OptionsEngineCom + (App.OfficeAvailable ? "" : " [" + Labels.OptionsEngineMissing + "]"));
            EngineSelection.Items.Add(Labels.OptionsEngineRender + (Engine.Render.SofficeLocator.Find() != null ? "" : " [" + Labels.OptionsEngineMissing + "]"));
            EngineSelection.SelectedIndex = Config.PreferredEngine == "render" ? 1 : 0;

            InsertPresBlanks.IsChecked = Config.InsertBlankAfterPres;
            InsertVideoBlanks.IsChecked = Config.InsertBlankAfterVideo;
            ShowPreviewBottom.IsChecked = Config.SlidePreviewBottom;

            for (double i = 10; i <= 15; i++)
                FontSizeList.Items.Add(i);
            FontSizeList.SelectedValue = Config.FontSize;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Config.LibraryPath = LibraryPath.Text;

            if (MonitorSelection.SelectedIndex == 0)
                Config.UseNonPrimaryScreen = true;
            else if (MonitorSelection.SelectedIndex != -1)
            {
                Config.UseNonPrimaryScreen = false;
                Config.ProjectorScreen = Screen.AllScreens[MonitorSelection.SelectedIndex - 1];
            }

            string engine = EngineSelection.SelectedIndex == 1 ? "render" : "com";
            if (engine != Config.PreferredEngine)
            {
                Config.PreferredEngine = engine;
                if (!AppServices.Engine.IsRunning)
                    AppServices.SelectEngine(App.OfficeAvailable);
            }

            Config.InsertBlankAfterPres = InsertPresBlanks.IsChecked ?? false;
            Config.InsertBlankAfterVideo = InsertVideoBlanks.IsChecked ?? false;
            Config.SlidePreviewBottom = ShowPreviewBottom.IsChecked ?? false;

            if (FontSizeList.SelectedValue != null)
                Config.FontSize = (double)FontSizeList.SelectedValue;

            (Owner as Main).BindLocationList();
            this.Close();
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            //WPF's built-in folder picker (.NET 8+) replaces the WindowsAPICodePack dialog
            var dirDialog = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = Config.LibraryPath,
                Title = Labels.OptionsLibraryBrowseDesc,
            };
            if (dirDialog.ShowDialog() == true)
                LibraryPath.Text = dirDialog.FolderName;
        }
    }
}
