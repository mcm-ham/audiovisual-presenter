using System.IO;
using LibVLCSharp.Shared;
using VlcCore = LibVLCSharp.Shared.Core; // "Core" alone resolves to the Presenter.Core namespace

namespace Presenter.App_Code
{
    /// <summary>
    /// Locates and initializes the native libvlc used for video/audio playback
    /// (replaces WPF's MediaElement/MediaPlayer). On Windows the natives come from
    /// the VideoLAN.LibVLC.Windows package next to the app; on macOS the NuGet
    /// natives are x64-only, so an installed VLC.app (universal binary) is used
    /// instead. When no libvlc is found media playback is disabled but the rest
    /// of the app keeps working.
    /// </summary>
    public static class Vlc
    {
        private static bool _initialized;

        public static LibVLC Instance { get; private set; }

        public static bool IsAvailable => TryInitialize();

        public static bool TryInitialize()
        {
            if (_initialized)
                return Instance != null;
            _initialized = true;

            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    string lib = FindMacLibDir();
                    if (lib == null)
                        return false;
                    VlcCore.Initialize(lib);
                }
                else
                {
                    VlcCore.Initialize(); // finds the packaged natives next to the app
                }

                Instance = new LibVLC();
                return true;
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                return false;
            }
        }

        private static string FindMacLibDir()
        {
            string[] candidates =
            [
                "/Applications/VLC.app/Contents/MacOS/lib",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications/VLC.app/Contents/MacOS/lib"),
            ];
            return candidates.FirstOrDefault(Directory.Exists);
        }
    }
}
