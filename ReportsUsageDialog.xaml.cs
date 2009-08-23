using System;
using System.Collections.Generic;
using System.Data;
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
using SongPresenter.App_Code;
using SongPresenter.Resources;
using System.IO;
using System.Windows.Controls.DataVisualization.Charting;

namespace SongPresenter
{
    public partial class ReportsUsageDialog : Window
    {
        public ReportsUsageDialog()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            LibraryList.ItemsSource = new string[] { Labels.ReportsUsageOptionAll }.Union(Directory.GetDirectories(Config.LibraryPath).Select(p => System.IO.Path.GetFileName(p))).Union(new string[] { Labels.ReportsUsageOptionOther });

            FromDate.SelectedDate = DateTime.Today.AddYears(-1);
            ToDate.SelectedDate = DateTime.Today;
            Generate(null, null);
        }

        private void Generate(object sender, RoutedEventArgs e)
        {
            DateTime fromD = FromDate.SelectedDate ?? DateTime.Today.AddYears(-1);
            DateTime toD = ToDate.SelectedDate ?? DateTime.Today;
            string[] libraries = options.Where(c => c.IsChecked ?? false).Select(c => c.Content.ToString().ToLower()).ToArray();
            var list = Item.GetUsageStats(fromD, toD, libraries);

            //http://stackoverflow.com/questions/992241/what-does-cannot-modify-the-logical-children-for-this-node-at-this-time-because-a
            (mainChart.Series[0] as Series).DataContext = list;
            mainChart.Height = Math.Max(450, 22 * list.Length);
            if ((mainChart.Series[0] as BarSeries).ActualDependentRangeAxis != null)
            {
                LinearAxis axis = (mainChart.Series[0] as BarSeries).ActualDependentRangeAxis as LinearAxis;
                axis.Minimum = 0;
                axis.Maximum = list.Max(i => (int?)i.Count); //occasionally the default maximum is insanely high
                axis.Interval = Math.Max(1, Math.Round(axis.Interval ?? 0));
            }
        }

        private void Download(object sender, RoutedEventArgs e)
        {
            DateTime fromD = FromDate.SelectedDate ?? DateTime.Today.AddYears(-1);
            DateTime toD = ToDate.SelectedDate ?? DateTime.Today;
            string[] libraries = options.Where(c => c.IsChecked ?? false).Select(c => c.Content.ToString().ToLower()).ToArray();
            var list = Item.GetUsageStats(fromD, toD, libraries);

            StringBuilder output = new StringBuilder();

            output.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\deflang5129{\fonttbl{\f0\fnil\fcharset0 Arial;}}");
            output.AppendLine(@"\viewkind4\uc1\pard\sa200\sl276\slmult1\lang9\b\f0\fs22 " + String.Format(Labels.ReportsUsageDocTitle, fromD.ToShortDateString(), toD.ToShortDateString()) + @"\b0\par ");

            foreach (ItemUsage item in list)
            {
                output.AppendLine(item.Name + @"\line");
                item.Dates.ForEach(d => output.AppendLine(@"\tab " + d.ToLongDateString() + @"\line"));
                output.AppendLine(@"\tab (" + item.Count + @")\line\line ");
            }

            output.AppendLine("}");
            output.Append((char)0);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string origFilename = desktop + "\\" + Labels.ReportsUsageDocFilename + " - " + DateTime.Today.ToLongDateString() + ".rtf";
            string filename = origFilename;

            for (int i = 1; File.Exists(filename); i++)
                filename = origFilename.Insert(origFilename.LastIndexOf('.'), " (" + i + ")");

            StreamWriter report = new StreamWriter(File.OpenWrite(filename));
            report.Write(output.ToString());
            report.Close();

            System.Diagnostics.Process.Start(filename);
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
