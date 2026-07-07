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
                string device = ScreenService.DeviceName(Config.ProjectorScreen);
                int idx = ScreenService.AllScreens.FindIndex(s => ScreenService.DeviceName(s) == device);
                return idx >= 0 ? idx + 1 : 1;
            }
        }
    }
}
