using System;
using System.Configuration;
using System.Windows;
using SongPresenter.App_Code;
using SongPresenter.Resources;
using Screen = System.Windows.Forms.Screen;

namespace SongPresenter
{
    public partial class OptionsDialog : Window
    {
        public OptionsDialog()
        {
            InitializeComponent();

            LibraryPath.Text = ConfigurationManager.AppSettings["LibraryPath"];

            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                MonitorSelection.Items.Add(Labels.OptionsMonitorTitle + " " + (i + 1) + (Screen.AllScreens[i].Primary ? " [" + Labels.OptionsMonitorPrimary + "]" : ""));

                if (Screen.AllScreens[i] == Config.ProjectorScreen)
                    MonitorSelection.SelectedIndex = i;
            }
            
            /*
            //enable presenter view
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + Config.PowerpointVersion + @"\PowerPoint\Options", true);
            if (Util.Parse<int>(key.GetValue("UseMonMgr")) != 1)
                key.SetValue("UseMonMgr", 1);
            */
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Config.LibraryPath = LibraryPath.Text;

            if (MonitorSelection.SelectedIndex != -1)
                Config.ProjectorScreen = Screen.AllScreens[MonitorSelection.SelectedIndex];

            (Owner as Main).BindLocationList();
            this.Close();
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dirDialog = new System.Windows.Forms.FolderBrowserDialog();
            var res = dirDialog.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
                LibraryPath.Text = dirDialog.SelectedPath;
        }
    }
}
