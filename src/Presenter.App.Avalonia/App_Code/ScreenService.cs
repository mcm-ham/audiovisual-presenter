using Avalonia.Controls;
using Avalonia.Platform;

namespace Presenter.App_Code
{
    /// <summary>
    /// Static access to monitor information (replaces System.Windows.Forms.Screen).
    /// Avalonia exposes screens through a TopLevel, so the main window registers
    /// itself here at startup.
    /// </summary>
    public static class ScreenService
    {
        private static Window _window;

        public static void Attach(Window window) => _window = window;

        public static IReadOnlyList<Screen> AllScreens =>
            _window?.Screens?.All ?? Array.Empty<Screen>();

        public static Screen PrimaryScreen =>
            _window?.Screens?.Primary ?? (AllScreens.Count > 0 ? AllScreens[0] : null);

        /// <summary>
        /// Stable identifier used to persist the projector screen selection.
        /// Prefers the platform display name; falls back to the screen position.
        /// </summary>
        public static string DeviceName(Screen screen) =>
            string.IsNullOrEmpty(screen.DisplayName)
                ? $"screen@{screen.Bounds.X},{screen.Bounds.Y}"
                : screen.DisplayName;
    }
}
