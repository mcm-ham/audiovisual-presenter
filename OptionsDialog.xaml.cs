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

            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                MonitorSelection.Items.Add(Labels.OptionsMonitorTitle + " " + (i + 1) + (Screen.AllScreens[i].Primary ? " [" + Labels.OptionsMonitorPrimary + "]" : ""));

                if (Screen.AllScreens[i] == Config.ProjectorScreen)
                    MonitorSelection.SelectedIndex = i;
            }

            InsertPresBlanks.IsChecked = Config.InsertBlankAfterPres;
            InsertVideoBlanks.IsChecked = Config.InsertBlankAfterVideo;
            /*
            //enable presenter view
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + Config.PowerpointVersion + @"\PowerPoint\Options", true);
            if (Util.Parse<int>(key.GetValue("UseMonMgr")) != 1)
                key.SetValue("UseMonMgr", 1);
            */

            for (double i = 10; i <= 15; i++)
                FontSize.Items.Add(i);
            FontSize.SelectedValue = Config.FontSize;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Config.LibraryPath = LibraryPath.Text;

            if (MonitorSelection.SelectedIndex != -1)
                Config.ProjectorScreen = Screen.AllScreens[MonitorSelection.SelectedIndex];

            Config.InsertBlankAfterPres = InsertPresBlanks.IsChecked ?? false;
            Config.InsertBlankAfterVideo = InsertVideoBlanks.IsChecked ?? false;
            
            Config.FontSize = (double)FontSize.SelectedValue;

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
                if (dirDialog.ShowDialog() == CommonFileDialogResult.OK)
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
