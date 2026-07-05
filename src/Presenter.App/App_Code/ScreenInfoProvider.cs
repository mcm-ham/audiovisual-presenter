using Presenter.Core.Abstractions;

namespace Presenter.App_Code
{
    /// <summary>
    /// Exposes the Config screen selection to presentation engines as plain rectangles
    /// (engines cannot reference System.Windows.Forms or Config).
    /// </summary>
    public class ScreenInfoProvider : IScreenInfoProvider
    {
        public ScreenBounds ProjectorBounds
        {
            get
            {
                var b = Config.ProjectorScreen.Bounds;
                return new ScreenBounds(b.Left, b.Top, b.Width, b.Height);
            }
        }

        public ScreenBounds PrimaryWorkingArea
        {
            get
            {
                var wa = Config.PrimaryScreen.WorkingArea;
                return new ScreenBounds(wa.Left, wa.Top, wa.Width, wa.Height);
            }
        }

        public int ProjectorScreenNumber
        {
            get
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                int idx = Array.FindIndex(screens, s => s.DeviceName == Config.ProjectorScreen.DeviceName);
                return idx >= 0 ? idx + 1 : 1;
            }
        }
    }
}
