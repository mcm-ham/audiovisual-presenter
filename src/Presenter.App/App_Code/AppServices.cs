using System.IO;
using System.Threading;
using Presenter.Core.Abstractions;
using Presenter.Core.Models;
using Presenter.Core.Settings;
using Presenter.Data;
using Presenter.Data.Legacy;
using Presenter.Engine.Com;
using Presenter.Engine.Uno;
using Presenter.Resources;

namespace Presenter.App_Code
{
    /// <summary>
    /// Composition root: owns the settings store, database context/repository and the
    /// presentation engine for the lifetime of the app.
    /// </summary>
    public static class AppServices
    {
        public static ISettingsStore SettingsStore { get; private set; }
        public static PresenterDbContext Db { get; private set; }
        public static ScheduleRepository Repository { get; private set; }
        public static IPresentationEngine Engine { get; set; }

        public static void Initialize()
        {
            SettingsStore = new JsonSettingsStore();
            Db = PresenterDb.Create();
            Repository = new ScheduleRepository(Db);
            Engine = new NoopPresentationEngine(); // replaced in SelectEngine once Office detection ran

            // wire the Item.IsFound binding property to library-path resolution,
            // persisting relocated filenames like the original Item.IsFound did
            Item.FoundResolver = item =>
            {
                bool found = item.TryResolveFile(Config.LibraryPath, out bool changed);
                if (changed)
                    Repository.SaveChanges();
                return found;
            };
        }

        private static SynchronizationContext _uiContext;
        private static Action _activateMainWindow;

        /// <summary>
        /// Picks the presentation engine by preference and availability: COM drives
        /// PowerPoint (full fidelity, needs Office), UNO drives live LibreOffice
        /// Impress slideshows (animations play, needs LibreOffice). Falls back:
        /// com → uno → no-playback stub. Re-invoked by the options dialog
        /// when the preference changes (context args stick from the first call at
        /// startup).
        /// </summary>
        public static void SelectEngine(bool officeAvailable, SynchronizationContext uiContext = null, Action activateMainWindow = null)
        {
            _uiContext = uiContext ?? _uiContext;
            _activateMainWindow = activateMainWindow ?? _activateMainWindow;

            var screens = new ScreenInfoProvider();
            var labels = new EngineLabels(Labels.SlideShowVideoLabel, Labels.SlideShowAudioLabel, Labels.SlideShowImageLabel, Labels.SlideShowSlideLabel);

            string preferred = SettingsStore.Current.PreferredEngine;
            var uno = new UnoPresentationEngine(SettingsStore, screens, labels, _uiContext, _activateMainWindow);

            if (preferred == "uno" && uno.IsAvailable)
                Engine = uno;
            else if (officeAvailable)
                Engine = new ComPresentationEngine(SettingsStore, screens, labels, _uiContext, _activateMainWindow);
            else if (uno.IsAvailable)
                Engine = uno;
            else
                Engine = new NoopPresentationEngine();
        }

        /// <summary>
        /// One-time import of the legacy SQL CE database. Looks for Database.sdf next to
        /// the app and in the data directory; runs the bundled net48 exporter.
        /// </summary>
        public static MigrationResult MigrateLegacyDatabase()
        {
            string exporter = Path.Combine(AppContext.BaseDirectory, "SdfExport", "Presenter.SdfExport.exe");
            string[] candidates =
            [
                Path.Combine(JsonSettingsStore.DefaultDirectory, "Database.sdf"),
                Path.Combine(AppContext.BaseDirectory, "Database.sdf"),
            ];

            foreach (string sdf in candidates)
            {
                var result = LegacySdfMigrator.TryMigrate(Db, sdf, exporter);
                if (result.Status != MigrationStatus.NotNeeded)
                    return result;
            }
            return new MigrationResult(MigrationStatus.NotNeeded);
        }
    }
}
