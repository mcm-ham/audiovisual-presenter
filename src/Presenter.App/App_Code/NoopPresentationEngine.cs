using Presenter.Core.Abstractions;
using Presenter.Core.Models;
using Presenter.Resources;

namespace Presenter.App_Code
{
    /// <summary>
    /// Placeholder engine used until the COM / render engines are wired up (steps 5-6).
    /// Keeps the shell functional (schedules, library, options, reports) without playback.
    /// </summary>
    public class NoopPresentationEngine : IPresentationEngine
    {
        public bool IsAvailable => false;
        public bool IsRunning => false;
        public IReadOnlyList<Slide> Slides { get; } = [];

#pragma warning disable CS0067 // events never raised by the placeholder
        public event EventHandler SlideShowEnd;
        public event EventHandler<SlideAddedEventArgs> SlideAdded;
        public event EventHandler<SlideIndexChangedEventArgs> SlideIndexChanged;
#pragma warning restore CS0067

        public void Start(Schedule schedule) =>
            throw new InvalidOperationException(Labels.AppRequiresOffice);

        public void Stop() { }
        public void Quit() { }
        public void AddSlides(Item scheduleItem) { }
        public void Next(Slide slide) { }
        public void Previous(Slide slide) { }
        public void GoTo(Slide slide) { }
        public void UpdateSlideTimings() { }
        public string ExportSlideImage(Slide slide, int idx, string suffix, int width, int height) => null;
        public int? GetSlideWindowHandle(Slide slide) => null;
        public void Reset(Slide slide) { }
        public void BringToFront(Slide slide) { }
        public void HideSlideWindows() { }
        public bool TryEditSlide(Slide slide) => false;
    }
}
