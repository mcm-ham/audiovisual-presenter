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
using Presenter.App_Code;
using System.Windows.Navigation;
using System.Net;
using System.Xml.Linq;

namespace Presenter
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            Background = new SolidColorBrush(Config.BackgroundColour);

            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            BuildNo.Text = " " + ver.Major + "." + ver.Minor + "." + ver.Build;

            var client = new WebClient();
            client.DownloadStringCompleted += (sen, ev) =>
            {
                if (ev.Error != null || ev.Cancelled)
                    return;

                var update = XDocument.Parse(ev.Result).Root.Element("update");
                var version = update.Element("version").Value;
                if (BuildNo.Text.Trim() != version)
                {
                    UpdateLink.NavigateUri = new Uri(update.Element("url").Value);
                    UpdateText.Text = string.Format(Presenter.Resources.Labels.AboutUpdateText, version);
                }
                else
                {
                    UpdateStatus.Visibility = System.Windows.Visibility.Visible;
                }
            };
            client.DownloadStringAsync(new Uri("http://www.minsoft.org/updates.xml"));
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }
    }
}
