using System;
using System.Configuration;
using System.Windows;
using System.Windows.Media;
using Microsoft.WindowsAPICodePack.Dialogs;
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
            
            LibraryPath.Text = ConfigurationManager.AppSettings["LibraryPath"] ?? "My Documents\\Library\\";

            MonitorSelection.Items.Add(Labels.OptionsMonitorAuto);

            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                MonitorSelection.Items.Add(Labels.OptionsMonitorTitle + " " + (i + 1) + (Screen.AllScreens[i].Primary ? " [" + Labels.OptionsMonitorPrimary + "]" : ""));

                if (Screen.AllScreens[i] == Config.ProjectorScreen)
                    MonitorSelection.SelectedIndex = i + 1;
            }

            if (Config.UseNonPrimaryScreen)
                MonitorSelection.SelectedIndex = 0;

            InsertPresBlanks.IsChecked = Config.InsertBlankAfterPres;
            InsertVideoBlanks.IsChecked = Config.InsertBlankAfterVideo;
            ShowPreviewBottom.IsChecked = Config.SlidePreviewBottom;

            /*
            //todo enable presenter view
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + Config.PowerpointVersion + @"\PowerPoint\Options", true);
            if (Util.Parse<int>(key.GetValue("UseMonMgr")) != 1)
                key.SetValue("UseMonMgr", 1);
            */

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

            Config.InsertBlankAfterPres = InsertPresBlanks.IsChecked ?? false;
            Config.InsertBlankAfterVideo = InsertVideoBlanks.IsChecked ?? false;
            Config.SlidePreviewBottom = ShowPreviewBottom.IsChecked ?? false;

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
            if (CommonOpenFileDialog.IsPlatformSupported)
            {
                var dirDialog = new CommonOpenFileDialog();
                dirDialog.InitialDirectory = Config.LibraryPath;
                dirDialog.Title = Labels.OptionsLibraryBrowseDesc;
                dirDialog.IsFolderPicker = true;
                if (dirDialog.ShowDialog() == CommonFileDialogResult.Ok)
                    LibraryPath.Text = dirDialog.FileName;
            }
            else
            {
                var dirDialog = new System.Windows.Forms.FolderBrowserDialog();
                dirDialog.Description = Labels.OptionsLibraryBrowseDesc;
                dirDialog.SelectedPath = Config.LibraryPath;
                var res = dirDialog.ShowDialog();
                if (res == System.Windows.Forms.DialogResult.OK)
                    LibraryPath.Text = dirDialog.SelectedPath;
            }
        }
    }
}
