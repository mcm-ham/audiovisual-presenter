namespace Presenter.Core.Abstractions;

/// <summary>Screen rectangle in device pixels.</summary>
public record ScreenBounds(int Left, int Top, int Width, int Height);

/// <summary>
/// Supplies monitor geometry to presentation engines without coupling them to
/// System.Windows.Forms (implemented in the app layer over Screen.AllScreens).
/// </summary>
public interface IScreenInfoProvider
{
    /// <summary>Full bounds of the projector (output) screen, in device pixels.</summary>
    ScreenBounds ProjectorBounds { get; }

    /// <summary>
    /// Projector bounds in logical points (device pixels divided by the screen's
    /// scale factor). LibreOffice's macOS windowed show positions the document
    /// window in logical points, so a HiDPI projector needs the descaled rectangle;
    /// defaults to <see cref="ProjectorBounds"/> where scale is always 1.
    /// </summary>
    ScreenBounds ProjectorLogicalBounds => ProjectorBounds;

    /// <summary>Working area of the primary (operator) screen.</summary>
    ScreenBounds PrimaryWorkingArea { get; }

    /// <summary>
    /// 1-based number of the projector screen in the OS monitor order (what
    /// LibreOffice's presentation Display property expects).
    /// </summary>
    int ProjectorScreenNumber => 1;
}
