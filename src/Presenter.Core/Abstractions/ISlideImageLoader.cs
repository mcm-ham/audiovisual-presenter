namespace Presenter.Core.Abstractions;

/// <summary>
/// Loads image files into the UI framework's bitmap type for image slides
/// (<see cref="Models.Slide.Image"/>/<see cref="Models.Slide.Preview"/> are
/// object-typed so engines stay decoupled from WPF/Avalonia).
/// </summary>
public interface ISlideImageLoader
{
    /// <summary>
    /// Loads an image scaled down to fit within maxWidth x maxHeight, in a form
    /// that is safe to hand across threads.
    /// </summary>
    object Load(string filename, int maxWidth, int maxHeight);
}
