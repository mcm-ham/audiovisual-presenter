using System;
using System.Configuration;
using System.Windows;
using SongPresenter.App_Code;
using SongPresenter.Resources;
using Screen = System.Windows.Forms.Screen;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Data;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell;
using System.Collections.Generic;
using System.Linq;

namespace SongPresenter
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

            for (double i = 20; i < 60; i += 2)
                FontSizeList.Items.Add(i.ToString());
            FontSizeList.SelectedValue = Config.MessengerFontSize.ToString();

            foreach (FontFamily fontFamily in Fonts.SystemFontFamilies)
                FontFamilyList.Items.Add(fontFamily.Source);
            FontFamilyList.SelectedValue = Config.MessengerFontFamily.Source;

            foreach (var color in typeof(Colors).GetProperties())
                FontColorList.Items.Add(color.Name);
            FontColorList.SelectedValue = Config.MessengerFontColourName;

            foreach (string align in Enum.GetNames(typeof(HorizontalAlignment)))
                if (align != "Stretch")
                    HorLocation.Items.Add(align);
            HorLocation.SelectedValue = Config.MessengerHorizontalPosition.ToString();

            foreach (string align in Enum.GetNames(typeof(VerticalAlignment)))
                if (align != "Stretch")
                    VerLocation.Items.Add(align);
            VerLocation.SelectedValue = Config.MessengerVerticalPosition.ToString();

            InsertBlanks.IsChecked = Config.InsertBlankSlides;
            /*
            //enable presenter view
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + Config.PowerpointVersion + @"\PowerPoint\Options", true);
            if (Util.Parse<int>(key.GetValue("UseMonMgr")) != 1)
                key.SetValue("UseMonMgr", 1);
            */
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            double size = Util.Parse<double>(FontSizeList.SelectedValue);
            FontFamily family = new FontFamily(FontFamilyList.SelectedValue.ToString());
            HorizontalAlignment posx = Util.Parse<HorizontalAlignment>(HorLocation.SelectedValue);
            VerticalAlignment posy = Util.Parse<VerticalAlignment>(VerLocation.SelectedValue);

            Config.LibraryPath = LibraryPath.Text;
            Config.SaveMessengerFont(size, family, (FontColorList.SelectedValue ?? "").ToString());
            Config.SaveMessengerLocation(posy, posx);

            if (MonitorSelection.SelectedIndex != -1)
                Config.ProjectorScreen = Screen.AllScreens[MonitorSelection.SelectedIndex];

            Config.InsertBlankSlides = InsertBlanks.IsChecked ?? false;

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
