using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using SongPresenter.App_Code;
using SongPresenter.Resources;

namespace SongPresenter
{
    public partial class ReportsListDialog : Window
    {
        public ReportsListDialog()
        {
            InitializeComponent();

            LibraryList.ItemsSource = new string[] { Labels.ReportsListOptionAll }.Union(Directory.GetDirectories(Config.LibraryPath).Select(p => System.IO.Path.GetFileName(p)));
        }

        protected void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (options.All(b => !(b.IsChecked ?? false)))
            {
                this.Close();
                return;
            }

            StringBuilder output = new StringBuilder();
            output.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\deflang5129{\fonttbl{\f0\fnil\fcharset0 Arial;}}");
            output.AppendLine(@"\viewkind4\uc1\pard\sa200\sl276\slmult1\lang9\b\f0\fs22 " + Labels.ReportsListDocTitle + " " + DateTime.Today.ToLongDateString() + @"\b0\par");
            string[] filetypes = Config.SupportedFileTypes;

            foreach (CheckBox chkbx in options.Skip(1))
            {
                if (!(chkbx.IsChecked ?? false))
                    continue;

                output.Append(@"\ul\i " + chkbx.Content + @"\line\ulnone\i0 ");

                foreach (string file in Directory.GetFiles(Config.LibraryPath + chkbx.Content))
                {
                    if (filetypes.Contains(System.IO.Path.GetExtension(file).ToLower().TrimStart('.')))
                        output.Append(System.IO.Path.GetFileNameWithoutExtension(file) + "\\line ");
                }

                output.Append("\\line ");
            }

            output.AppendLine("}");
            output.Append((char)0);
            
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string filename = desktop + "\\" + Labels.ReportsListDocFilename + " - " + DateTime.Today.ToLongDateString() + ".rtf";
            StreamWriter report = new StreamWriter(File.OpenWrite(filename));
            report.Write(output.ToString());
            report.Close();

            System.Diagnostics.Process.Start(filename);
            this.Close();
        }

        protected void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox chkbx = sender as CheckBox;
            if (chkbx == options[0])
                options.ForEach(b => b.IsChecked = (chkbx.IsChecked ?? false));
            else
                options[0].IsChecked = false;
        }

        List<CheckBox> options = new List<CheckBox>();
        private void CheckBox_Loaded(object sender, RoutedEventArgs e)
        {
            options.Add(sender as CheckBox);
        }
    }
}
