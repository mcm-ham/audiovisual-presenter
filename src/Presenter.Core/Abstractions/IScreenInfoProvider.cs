namespace Presenter.Core.Abstractions;

/// <summary>Screen rectangle in device pixels.</summary>
public record ScreenBounds(int Left, int Top, int Width, int Height);

/// <summary>
/// Supplies monitor geometry to presentation engines without coupling them to
/// System.Windows.Forms (implemented in the app layer over Screen.AllScreens).
/// </summary>
public interface IScreenInfoProvider
{
    /// <summary>Full bounds of the projector (output) screen.</summary>
    ScreenBounds ProjectorBounds { get; }

    /// <summary>Working area of the primary (operator) screen.</summary>
    ScreenBounds PrimaryWorkingArea { get; }
}
