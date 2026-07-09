using Presenter.Core;
using Presenter.Core.Abstractions;

namespace Presenter.App_Code
{
    /// <summary>
    /// Exposes the Config screen selection to presentation engines as plain rectangles
    /// (engines cannot reference Avalonia or Config).
    /// </summary>
    public class ScreenInfoProvider : IScreenInfoProvider
    {
        public ScreenBounds ProjectorBounds
        {
            get
            {
                var b = Config.ProjectorScreen.Bounds;
                return new ScreenBounds(b.X, b.Y, b.Width, b.Height);
            }
        }

        public ScreenBounds ProjectorLogicalBounds
        {
            get
            {
                //Avalonia reports Bounds (and origin) in physical pixels; LibreOffice's
                //windowed show positions the document window in logical points, so
                //descale by the projector screen's scale factor (2 on a Retina panel).
                var screen = Config.ProjectorScreen;
                var b = screen.Bounds;
                double scale = screen.Scaling;
                return new ScreenBounds(
                    (int)Math.Round(b.X / scale), (int)Math.Round(b.Y / scale),
                    (int)Math.Round(b.Width / scale), (int)Math.Round(b.Height / scale));
            }
        }

        public ScreenBounds PrimaryWorkingArea
        {
            get
            {
                var wa = Config.PrimaryScreen.WorkingArea;
                return new ScreenBounds(wa.X, wa.Y, wa.Width, wa.Height);
            }
        }

        public int ProjectorScreenNumber
        {
            get
            {
                var b = Config.ProjectorScreen.Bounds;

                //Impress numbers displays in the OS EnumDisplayMonitors order; ask Win32
                //directly so we match the working WPF app (WinForms uses the same order)
                //rather than Avalonia's screen-list order, which need not agree.
                if (OperatingSystem.IsWindows())
                {
                    int number = User32.MonitorNumberAt(b.X, b.Y);
                    if (number > 0)
                        return number;
                }

                //non-Windows fallback: position within Avalonia's screen list. Match on
                //bounds (unique) instead of the display name (identical monitors collide).
                var all = ScreenService.AllScreens;
                for (int i = 0; i < all.Count; i++)
                    if (all[i].Bounds.X == b.X && all[i].Bounds.Y == b.Y)
                        return i + 1;
                return 1;
            }
        }
    }
}
