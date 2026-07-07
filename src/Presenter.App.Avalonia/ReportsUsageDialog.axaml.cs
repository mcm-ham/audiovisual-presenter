using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Presenter.App_Code;
using Presenter.Core;
using Presenter.Core.Models;
using Presenter.Resources;

namespace Presenter
{
    public partial class ReportsUsageDialog : Window
    {
        public ReportsUsageDialog()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            LibraryList.ItemsSource = new string[] { Labels.ReportsUsageOptionAll }.Union(Directory.GetDirectories(Config.LibraryPath).Select(p => System.IO.Path.GetFileName(p))).Union(new string[] { Labels.ReportsUsageOptionOther }).ToArray();

            FromDate.SelectedDate = DateTime.Today.AddYears(-1);
            ToDate.SelectedDate = DateTime.Today;
            Generate(null, null);
        }

        private ItemUsage[] GetStats()
        {
            DateTime fromD = FromDate.SelectedDate ?? DateTime.Today.AddYears(-1);
            DateTime toD = ToDate.SelectedDate ?? DateTime.Today;
            string[] libraries = options.Where(c => c.IsChecked ?? false).Select(c => c.Content.ToString().ToLower()).ToArray();
            return AppServices.Repository.GetUsageStats(fromD, toD, libraries, Config.LibraryPath);
        }

        private void Generate(object sender, RoutedEventArgs e)
        {
            var list = GetStats();
            int max = Math.Max(1, list.Length == 0 ? 1 : list.Max(i => i.Count));
            const double maxBarWidth = 380;

            //most-used at the top (the old horizontal BarSeries rendered ascending from the bottom)
            mainChart.ItemsSource = list.Reverse().Select(i => new UsageRow
            {
                Name = i.Name,
                Count = i.Count,
                BarWidth = Math.Max(2, i.Count / (double)max * maxBarWidth),
                Tooltip = string.Join(Environment.NewLine, i.Presentations),
            }).ToArray();
        }

        private void Download(object sender, RoutedEventArgs e)
        {
            DateTime fromD = FromDate.SelectedDate ?? DateTime.Today.AddYears(-1);
            DateTime toD = ToDate.SelectedDate ?? DateTime.Today;
            var list = GetStats();

            StringBuilder output = new StringBuilder();

            output.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\deflang5129{\fonttbl{\f0\fnil\fcharset0 Arial;}}");
            output.AppendLine(@"\viewkind4\uc1\pard\sa200\sl276\slmult1\lang9\b\f0\fs22 " + String.Format(Labels.ReportsUsageDocTitle, fromD.ToShortDateString(), toD.ToShortDateString()) + @"\b0\par ");

            foreach (ItemUsage item in list)
            {
                output.AppendLine(item.Name + @"\line");
                item.Presentations.ForEach(d => output.AppendLine(@"\tab " + d + @"\line"));
                output.AppendLine(@"\tab (" + item.Count + @")\line\line ");
            }

            output.AppendLine("}");
            output.Append((char)0);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string origFilename = Path.Combine(desktop, Labels.ReportsUsageDocFilename + " - " + DateTime.Today.ToLongDateString() + ".rtf");
            string filename = origFilename;

            for (int i = 1; File.Exists(filename); i++)
                filename = origFilename.Insert(origFilename.LastIndexOf('.'), " (" + i + ")");

            StreamWriter report = new StreamWriter(File.OpenWrite(filename));
            report.Write(output.ToString());
            report.Close();

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filename) { UseShellExecute = true });
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

        private class UsageRow
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public double BarWidth { get; set; }
            public string Tooltip { get; set; }
        }
    }
}
