using Presenter.Core.Models;

namespace Presenter.Core.Abstractions;

/// <summary>
/// Pluggable presentation playback engine. The COM implementation drives the real
/// PowerPoint application (full animation fidelity, requires Office); the render
/// implementation pages through pre-rendered slide images (no Office required).
/// The surface mirrors the original App_Code/SlideShow.cs so the UI port stays close
/// to the existing code.
/// </summary>
public interface IPresentationEngine
{
    /// <summary>Whether this engine can run on the current machine (e.g. Office installed).</summary>
    bool IsAvailable { get; }

    bool IsRunning { get; }

    IReadOnlyList<Slide> Slides { get; }

    /// <summary>
    /// Builds the slide list for the schedule and starts playback resources.
    /// Raises <see cref="SlideAdded"/> as each slide is added (drives the build
    /// progress dialog).
    /// </summary>
    void Start(Schedule schedule);

    void Stop();

    /// <summary>Releases any external application started by the engine, if idle.</summary>
    void Quit();

    /// <summary>Adds the slides of a schedule item to an already-running show.</summary>
    void AddSlides(Item scheduleItem);

    void Next(Slide slide);
    void Previous(Slide slide);
    void GoTo(Slide slide);

    /// <summary>Re-applies the UseSlideTimings setting to a running show.</summary>
    void UpdateSlideTimings();

    /// <summary>
    /// Exports a slide to a PNG image for previews. Returns the file path, or null
    /// when the engine cannot export the slide.
    /// </summary>
    string? ExportSlideImage(Slide slide, int idx, string suffix, int width, int height);

    /// <summary>
    /// Native window handle showing the given slide (the PowerPoint slideshow window
    /// for the COM engine), or null when the engine has no own window for it.
    /// </summary>
    int? GetSlideWindowHandle(Slide slide);

    /// <summary>Resets the presentation that owns the slide back to its first slide.</summary>
    void Reset(Slide slide);

    /// <summary>Brings the engine's window for this slide in front on the projector screen.</summary>
    void BringToFront(Slide slide);

    /// <summary>Hides native slideshow windows while Presenter supplies projector output.</summary>
    void HideSlideWindows();

    /// <summary>Opens the slide for editing in the authoring application, when supported.</summary>
    bool TryEditSlide(Slide slide);

    event EventHandler? SlideShowEnd;
    event EventHandler<SlideAddedEventArgs>? SlideAdded;
    event EventHandler<SlideIndexChangedEventArgs>? SlideIndexChanged;
}

public class SlideIndexChangedEventArgs(int oldIndex, int newIndex) : EventArgs
{
    public int OldIndex { get; } = oldIndex;
    public int NewIndex { get; } = newIndex;
}

public class SlideAddedEventArgs(Slide? slide, double progress) : EventArgs
{
    public Slide? NewSlide { get; } = slide;

    /// <summary>Build progress in the range 0..1, or -1 when the list was reset.</summary>
    public double Progress { get; } = progress;
}
