using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Presenter.App_Code;
using Presenter.Core.Settings;
using Presenter.Data.Legacy;
using Presenter.Resources;

namespace Presenter
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
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
        /// True when a PowerPoint installation was detected (COM engine usable).
        /// Always false off Windows.
        /// </summary>
        public static bool OfficeAvailable { get; private set; }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                AppServices.Initialize();
                OfficeAvailable = DetectPowerPoint();

                var main = new Main();
                desktop.MainWindow = main;
                ScreenService.Attach(main);

                AppServices.SelectEngine(OfficeAvailable,
                    new AvaloniaSynchronizationContext(),
                    () =>
                    {
                        //a show window of another process holds the foreground; plain
                        //Activate() is denied by Windows and only flashes the taskbar
                        if (OperatingSystem.IsWindows() && main.TryGetPlatformHandle() is { } handle)
                            User32.ForceForeground(handle.Handle);
                        main.Activate();
                    });

                Config.FontSize = AppServices.SettingsStore.Current.FontSize ?? 12;

                // surface unhandled UI-thread exceptions like the WPF DispatcherUnhandledException did
                Dispatcher.UIThread.UnhandledException += (s, e) =>
                {
                    e.Handled = true;
                    LogError(e.Exception);
                    _ = App_Code.MessageBox.Show(desktop.MainWindow,
                        Labels.AppError + ":" + Environment.NewLine + e.Exception.GetBaseException().Message, "Presenter");
                    if (desktop.MainWindow == null || !desktop.MainWindow.IsVisible)
                        Environment.Exit(1);
                };

                // database work off the UI thread; also performs the one-time legacy import
                Task.Run(() =>
                {
                    try
                    {
                        var migration = AppServices.MigrateLegacyDatabase();
                        if (migration.Status == MigrationStatus.Imported)
                            Dispatcher.UIThread.Invoke(() => App_Code.MessageBox.Show(desktop.MainWindow,
                                $"Imported {migration.Counts.Schedules} schedules from the previous version's database.", "Presenter"));
                        else if (migration.Status == MigrationStatus.Failed)
                            LogError(new Exception("Legacy database migration failed: " + migration.Error));
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                });
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static bool DetectPowerPoint()
        {
#if WINDOWS
            return Presenter.Engine.Com.ComPresentationEngine.PowerPointInstalled();
#else
            return false;
#endif
        }
    }
}
