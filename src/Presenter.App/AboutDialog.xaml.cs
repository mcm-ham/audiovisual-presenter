using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Xml.Linq;
using Presenter.App_Code;

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

            CheckForUpdate();
        }

        private async void CheckForUpdate()
        {
            try
            {
                using var client = new HttpClient();
                string result = await client.GetStringAsync("http://www.minsoft.org/updates.xml");

                var update = XDocument.Parse(result).Root.Element("update");
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
            }
            catch
            {
                //update check is best-effort
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
        }
    }
}
