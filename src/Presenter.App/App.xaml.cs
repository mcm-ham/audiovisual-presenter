using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Presenter.App_Code;
using Presenter.Core.Settings;
using Presenter.Data.Legacy;
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
            LogError(e.Exception);

            MessageBox.Show(Labels.AppError + ":" + Environment.NewLine + e.Exception.GetBaseException().Message, "Presenter", MessageBoxButton.OK, MessageBoxImage.Error);

            //if main window is not open when error is thrown, close application otherwise only way to close it will be via task manager
            if (this.MainWindow == null || this.MainWindow.Visibility != Visibility.Visible)
                Environment.Exit(1);
        }

        public static void LogError(Exception ex)
        {
            ex = ex.GetBaseException();
            string stacktrace = ex.StackTrace ?? "";
            if (stacktrace.Length > 500 && stacktrace.IndexOf(" at ", 500) != -1)
                stacktrace = stacktrace.Substring(0, stacktrace.IndexOf("at", 500));

            try
            {
                Directory.CreateDirectory(JsonSettingsStore.DefaultDirectory);
                string path = Path.Combine(JsonSettingsStore.DefaultDirectory, "error.log");
                using StreamWriter log = new(File.Open(path, FileMode.Append));
                log.WriteLine("Date: " + DateTime.Now);
                log.WriteLine("Type: " + ex.GetType().Name);
                log.WriteLine("Error: " + ex.Message + Environment.NewLine + "StackTrace:" + Environment.NewLine + stacktrace + Environment.NewLine);
            }
            catch { /* never fail while logging a failure */ }
        }

        /// <summary>
        /// True when a PowerPoint installation was detected (COM engine usable). Unlike
        /// the original app this is a soft check — without Office the app still runs and
        /// can use the render engine.
        /// </summary>
        public static bool OfficeAvailable { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            AppServices.Initialize();
            OfficeAvailable = DetectPowerPoint();
            AppServices.SelectEngine(OfficeAvailable,
                new System.Windows.Threading.DispatcherSynchronizationContext(Dispatcher),
                () => Current.MainWindow?.Activate());

            Config.FontSize = AppServices.SettingsStore.Current.FontSize ?? SystemFonts.MessageFontSize;

            base.OnStartup(e);

            // database work off the UI thread; also performs the one-time legacy import
            Task.Run(() =>
            {
                try
                {
                    var migration = AppServices.MigrateLegacyDatabase();
                    if (migration.Status == MigrationStatus.Imported)
                        Dispatcher.Invoke(() => MessageBox.Show(
                            $"Imported {migration.Counts.Schedules} schedules from the previous version's database.",
                            "Presenter", MessageBoxButton.OK, MessageBoxImage.Information));
                    else if (migration.Status == MigrationStatus.Failed)
                        LogError(new Exception("Legacy database migration failed: " + migration.Error));
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
            });
        }

        private static bool DetectPowerPoint()
        {
            return Presenter.Engine.Com.ComPresentationEngine.PowerPointInstalled();
        }
    }
}
