using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Presenter.App_Code;
using Presenter.Resources;

namespace Presenter
{
    public partial class App : Application
    {
        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Prevent default unhandled exception processing
            e.Handled = true;

            // Process unhandled exception
            Exception ex = e.Exception.GetBaseException();
            string stacktrace = ex.StackTrace;
            if (stacktrace.Length > 500 && stacktrace.IndexOf(" at ", 500) != -1)
                stacktrace = stacktrace.Substring(0, stacktrace.IndexOf("at", 500));

            string path = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath) + "\\error.log";
            StreamWriter log = new StreamWriter(File.Open(path, FileMode.Append));
            log.WriteLine("Date: " + DateTime.Now);
            log.WriteLine("Type: " + ex.GetType().Name);
            log.WriteLine("Error: " + ex.Message + Environment.NewLine + "StackTrace:" + Environment.NewLine + stacktrace + Environment.NewLine);
            log.Flush();
            log.Close();
            
            MessageBox.Show(Labels.AppError + ":" + Environment.NewLine + ex.Message, "Presenter", MessageBoxButton.OK, MessageBoxImage.Error);

            //if main window is not open when error is thrown, close application otherwise only way to close it will be via task manager
            if (this.MainWindow == null || this.MainWindow.Visibility != Visibility.Visible)
                Environment.Exit(1);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server Compact Edition\v3.5", false);
            if (key == null || Util.Parse<int>(key.GetValue("ServicePackLevel")) < 1)
                throw new Exception(Labels.AppRequiresSql);

            key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\MediaPlayer\PlayerUpgrade", false);
            if (key == null || Util.Parse<int>((key.GetValue("PlayerVersion") ?? "").ToString().Split(',').FirstOrDefault()) < 10)
                MessageBox.Show(Labels.AppRequiresWMP, "", MessageBoxButton.OK, MessageBoxImage.Exclamation);

            key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Office");
            bool found = false;
            if (key != null)
            {
                string[] versions = key.GetSubKeyNames().Where(v => Util.Parse<double>(v) >= 10).ToArray();
                foreach (string v in versions)
                {
                    key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Office\" + v + @"\PowerPoint\InstallRoot", false);
                    if (key != null && !String.IsNullOrEmpty(key.GetValue("Path") as string))
                        found = true;
                }
            }

            if (!found)
            {
                key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Office");
                if (key == null)
                    throw new Exception(Labels.AppRequiresOffice);
                string[] versions = key.GetSubKeyNames().Where(v => Util.Parse<double>(v) >= 10).ToArray();
                foreach (string v in versions)
                {
                    key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Office\" + v + @"\PowerPoint\InstallRoot", false);
                    if (key != null && !String.IsNullOrEmpty(key.GetValue("Path") as string))
                        found = true;
                }
            }

            if (!found)
                throw new Exception(Labels.AppRequiresOffice);

            Config.FontSize = SystemFonts.MessageFontSize;

            base.OnStartup(e);
        }
    }
}
