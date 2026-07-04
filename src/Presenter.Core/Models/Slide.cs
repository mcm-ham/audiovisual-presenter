namespace Presenter.Core.Models;

/// <summary>
/// A single position in the running slideshow: a PowerPoint slide, a video, an audio
/// track, an image, or a blank screen. Ported from App_Code/Slide.cs with all engine
/// (COM) and UI (WPF) types removed — engines stash their payload in
/// <see cref="EngineData"/>, and the UI owns preview imagery.
/// </summary>
public class Slide
{
    public Slide(SlideType type, string filename)
    {
        Type = type;
        Filename = filename;
    }

    public SlideType Type { get; set; }
    public string Filename { get; set; }

    /// <summary>Summary text extracted from the slide (or media name).</summary>
    public string? Text { get; set; }

    /// <summary>Presenter notes / comment shown in the operator UI.</summary>
    public string? Comment { get; set; }

    public int? JumpIndex { get; set; }

    public Item? ScheduleItem { get; set; }

    /// <summary>
    /// Engine-specific payload. The COM engine stores its PowerPoint presentation
    /// reference here; the render engine stores rendered image paths.
    /// </summary>
    public object? EngineData { get; set; }

    /// <summary>
    /// Full-size image for image slides (UI image type, object-typed so Core stays
    /// UI-framework free; WPF bindings accept object sources).
    /// </summary>
    public object? Image { get; set; }

    /// <summary>Preview thumbnail of the slide.</summary>
    public object? Preview { get; set; }

    /// <summary>Total number of clicks that trigger animations on the slide.</summary>
    public int AnimationCount { get; set; }

    /// <summary>Number of animations triggered so far by clicking.</summary>
    public int CurrentAnimationCount { get; set; }

    /// <summary>
    /// Original AdvanceOnTime setting of the PowerPoint slide, kept to allow toggling
    /// UseSlideTimings during a slideshow. Null for non-PowerPoint slides.
    /// </summary>
    public bool? AdvanceOnTime { get; set; }

    /// <summary>One-based index of the slide position within the whole schedule.</summary>
    public int SlideIndex { get; set; }

    /// <summary>One-based index of the slide within its schedule item.</summary>
    public int ItemIndex { get; set; }
}
