﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.Diagnostics;
using Presenter.App_Code;
using System.Windows.Media;
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
            log.WriteLine("Error: " + ex.Message + Environment.NewLine + "StackTrace:" + Environment.NewLine + stacktrace);
            log.Flush();
            log.Close();
            
            MessageBox.Show(Labels.AppError + ":" + Environment.NewLine + ex.Message, "Presenter", MessageBoxButton.OK, MessageBoxImage.Error);

            //if main window is not open when error is thrown, close application otherwise only way to close it will be via task manager
            if (this.MainWindow == null)
                Environment.Exit(1);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server Compact Edition\v3.5", false);
            if (key == null || Util.Parse<int>(key.GetValue("ServicePackLevel")) < 1)
                throw new Exception(Labels.AppRequiresSql);

            base.OnStartup(e);
        }
    }
}
