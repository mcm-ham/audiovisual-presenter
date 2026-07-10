using System.IO;
using System.Runtime.InteropServices;
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
        public static LibVLC Instance { get; private set; }

        /// <summary>
        /// True when VLC/libvlc is present. On macOS an installed VLC.app is
        /// required; on Windows the natives ship next to the app, so it is always
        /// considered installed. This is a cheap presence check only — it does not
        /// initialize libvlc, so a corrupt install can still be "installed".
        /// </summary>
        public static bool IsInstalled => !OperatingSystem.IsMacOS() || FindMacLibDir() != null;

        /// <summary>
        /// Initializes libvlc on first use. Returns false only when VLC is not
        /// installed (the expected, benign case the caller reports to the user).
        /// A genuine initialization failure on an installed VLC is not swallowed:
        /// it throws so global error handling logs and surfaces it.
        /// </summary>
        public static bool TryInitialize()
        {
            if (Instance != null)
                return true;
            if (!IsInstalled)
                return false;

            if (OperatingSystem.IsMacOS())
            {
                string lib = FindMacLibDir();
                // libvlc.dylib loads from the app bundle, but its built-in plugin
                // path points at VideoLAN's build machine, so libvlc_new() can't
                // find the codecs and fails ("Failed to perform instanciation on
                // the native side"). Point it at the bundle's plugins dir. Must go
                // through the C runtime's setenv: libvlc reads it via getenv(), and
                // Environment.SetEnvironmentVariable does not update that on macOS.
                string plugins = Path.Combine(Path.GetDirectoryName(lib), "plugins");
                if (Directory.Exists(plugins))
                    setenv("VLC_PLUGIN_PATH", plugins, 1);
                VlcCore.Initialize(lib);
            }
            else
            {
                VlcCore.Initialize(); // finds the packaged natives next to the app
            }

            Instance = new LibVLC();
            return true;
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

        // Sets an environment variable in the C runtime so native libvlc's getenv()
        // sees it; the managed Environment.SetEnvironmentVariable does not on macOS.
        [DllImport("libc", SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);
    }
}
