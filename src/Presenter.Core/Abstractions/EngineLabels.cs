namespace Presenter.Core.Abstractions;

/// <summary>
/// Localized display strings used by presentation engines when building the slide
/// list (filled from the Labels resx by the app layer — engines cannot see it).
/// </summary>
public record EngineLabels(string VideoLabel, string AudioLabel, string ImageLabel, string SlideLabel);
