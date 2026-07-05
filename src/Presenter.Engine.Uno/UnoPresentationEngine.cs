using System.IO;
using System.Windows.Media.Imaging;
using Presenter.Core.Abstractions;
using Presenter.Core.Models;
using Presenter.Engine.Render;
using Presenter.Engine.Uno.Interop;

namespace Presenter.Engine.Uno;

/// <summary>
/// Animated LibreOffice playback: each PowerPoint schedule item gets its own soffice
/// instance running a live Impress slideshow, remote-controlled over UNO through a
/// helper script on LibreOffice's bundled Python (see slideshow_host.py). Animations
/// and transitions actually play, unlike the static render engine. Presentation
/// switching follows the COM engine's model: every show window stays alive and
/// selection just changes the z-order. <see cref="Slide.EngineData"/> holds the
/// presentation's <see cref="SlideshowHost"/> (shared reference marks presentation
/// boundaries for the UI). Next/Previous bookkeeping uses the per-slide click-effect
/// counts gathered from the animation trees at load, mirroring the COM engine's
/// GetClickIndex/GetClickCount logic.
/// </summary>
public class UnoPresentationEngine : IPresentationEngine
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

    private readonly ISettingsStore _settings;
    private readonly IScreenInfoProvider _screens;
    private readonly EngineLabels _labels;
    private readonly SynchronizationContext? _sync;
    private readonly Action? _activateMainWindow;

    private readonly List<Slide> _slides = new();
    private readonly List<SlideshowHost> _hosts = new();
    private int _profileSlot;

    /// <param name="syncContext">UI thread context; events triggered by the show window
    /// (timings, clicks, ESC) are marshalled to it like the COM engine does.</param>
    /// <param name="activateMainWindow">Re-activates the operator window after each
    /// slideshow starts (show windows steal focus); invoked on the UI thread.</param>
    public UnoPresentationEngine(ISettingsStore settings, IScreenInfoProvider screens, EngineLabels labels,
        SynchronizationContext? syncContext = null, Action? activateMainWindow = null)
    {
        _settings = settings;
        _screens = screens;
        _labels = labels;
        _sync = syncContext;
        _activateMainWindow = activateMainWindow;
    }

    public bool IsAvailable => SofficeLocator.Find() is string soffice
        && File.Exists(Path.Combine(Path.GetDirectoryName(soffice)!, "python.exe"));

    public bool IsRunning { get; private set; }

    public IReadOnlyList<Slide> Slides => _slides;

    public event EventHandler? SlideShowEnd;
    public event EventHandler<SlideAddedEventArgs>? SlideAdded;
    public event EventHandler<SlideIndexChangedEventArgs>? SlideIndexChanged;

    private static SlideshowHost? Host(Slide s) => s.EngineData as SlideshowHost;

    /// <summary>0-based Impress slide index behind a schedule slide.</summary>
    private static int ImpressIndex(Slide s) => s.ItemIndex - 1;

    public void Start(Schedule schedule)
    {
        try
        {
            Item[] items = schedule.Items.OrderBy(i => i.Ordinal).ToArray();
            IsRunning = true;
            _profileSlot = 0;

            _slides.Clear();
            SlideAdded?.Invoke(this, new SlideAddedEventArgs(null, -1));

            AddSlide(new Slide(SlideType.Blank, "") { Text = "", Comment = "Blank" }, 0, new Item(), 1);

            foreach (Item item in items)
                AddSlides(item);

            AddSlide(new Slide(SlideType.Blank, "") { Text = "", Comment = "Blank" }, 0, new Item(), 1);
        }
        catch (Exception)
        {
            Stop();
            throw;
        }
    }

    public void AddSlides(Item scheduleItem)
    {
        if (!scheduleItem.IsFound || !IsRunning)
            return;

        var S = _settings.Current;
        int itemCount = scheduleItem.Schedule?.Items.Count ?? 1;
        double progressEnd = (scheduleItem.Ordinal + 1) / (double)itemCount;
        string filename = Path.GetFullPath(scheduleItem.Filename).ToLower();
        string filetype = Path.GetExtension(filename).TrimStart('.').ToLower();

        if (S.VideoFormats.Contains(filetype))
        {
            AddSlide(new Slide(SlideType.Video, filename) { Text = scheduleItem.Name, Comment = _labels.VideoLabel }, progressEnd, scheduleItem, 1);
        }
        else if (S.AudioFormats.Contains(filetype))
        {
            AddSlide(new Slide(SlideType.Audio, filename) { Text = scheduleItem.Name, Comment = _labels.AudioLabel }, progressEnd, scheduleItem, 1);
        }
        else if (S.ImageFormats.Contains(filetype))
        {
            var b = _screens.ProjectorBounds;
            var s = new Slide(SlideType.Image, filename)
            {
                Text = scheduleItem.Name,
                Comment = _labels.ImageLabel,
                Image = RetrieveImage(filename, b.Width, b.Height),
                Preview = RetrieveImage(filename, 333, 250),
            };
            AddSlide(s, progressEnd, scheduleItem, 1);
        }
        else if (S.PowerPointTemplates.Contains(filetype))
        {
            //applying .pot template designs requires PowerPoint; skipped like the render engine
            SlideAdded?.Invoke(this, new SlideAddedEventArgs(null, progressEnd));
        }
        else if (S.PowerPointFormats.Contains(filetype))
        {
            SlideshowHost host = LaunchHost(filename);

            for (int i = 0; i < host.SlideInfo.Count; i++)
            {
                var info = host.SlideInfo[i];
                var s = new Slide(SlideType.PowerPoint, filename)
                {
                    Text = info.Text.Length > 0 ? info.Text : _labels.SlideLabel + (i + 1),
                    Comment = info.Notes,
                    EngineData = host,
                    AnimationCount = info.ClickEffects,
                    AdvanceOnTime = info.AdvanceOnTime,
                };
                AddSlide(s, progressEnd, scheduleItem, i + 1);
            }

            if (!IsRunning)
                return;

            host.Request(StartTimeout, "start",
                ("display", _screens.ProjectorScreenNumber),
                ("withTimings", S.UseSlideTimings)).Dispose();

            //show windows grab focus; give it back to the operator window
            OnUi(() => _activateMainWindow?.Invoke());
        }

        if (S.InsertBlankAfterPres && S.PowerPointFormats.Contains(filetype) || S.InsertBlankAfterVideo && S.VideoFormats.Concat(S.AudioFormats).Contains(filetype))
        {
            AddSlide(new Slide(SlideType.Blank, "") { Text = "", Comment = "Blank" }, progressEnd, new Item(), 1);
        }
    }

    private SlideshowHost LaunchHost(string filename)
    {
        string soffice = SofficeLocator.Find()
            ?? throw new InvalidOperationException("LibreOffice (soffice.exe) was not found. Install LibreOffice or select another engine in Options.");

        //profiles must live at a SHORT path: LibreOffice profile internals are deep
        //enough that a long base path exceeds MAX_PATH and crashes soffice on first run
        string profile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AudiovisualPresenter", "uno", "p" + _profileSlot++);

        var host = SlideshowHost.Launch(soffice, filename, profile, LoadTimeout);
        host.SlideChanged += Host_SlideChanged;
        host.ShowEnded += Host_ShowEnded;
        _hosts.Add(host);
        return host;
    }

    private Slide AddSlide(Slide s, double progress, Item scheduleItem, int itemIndex)
    {
        s.SlideIndex = _slides.Count + 1;
        s.ScheduleItem = scheduleItem;
        s.ItemIndex = itemIndex;
        _slides.Add(s);
        SlideAdded?.Invoke(this, new SlideAddedEventArgs(s, progress));
        return s;
    }

    public void Stop()
    {
        if (!IsRunning && _hosts.Count == 0)
            return;

        //set first: ShowEnded events raised while hosts shut down must not re-enter
        IsRunning = false;

        foreach (var host in _hosts)
        {
            host.SlideChanged -= Host_SlideChanged;
            host.ShowEnded -= Host_ShowEnded;
            host.Dispose();
        }
        _hosts.Clear();
        _slides.ForEach(s => s.EngineData = null);
    }

    public void Quit() { }

    // -- navigation ------------------------------------------------------------------

    public void Next(Slide slide)
    {
        int pos = slide.SlideIndex;
        var host = Host(slide);

        if (slide.Type == SlideType.PowerPoint && host != null)
        {
            if (slide.CurrentAnimationCount < slide.AnimationCount)
            {
                //a click animation is pending on this slide: play it, position unchanged
                slide.CurrentAnimationCount++;
                TryRequest(host, "next");
                SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos));
                return;
            }

            if (ImpressIndex(slide) < host.SlideInfo.Count - 1)
            {
                //advance within the presentation
                TryRequest(host, "next");
                host.LastKnownIndex = ImpressIndex(slide) + 1;
                if (pos < _slides.Count)
                {
                    _slides[pos].CurrentAnimationCount = 0;
                    SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos + 1));
                }
                return;
            }
        }

        MoveToNext(pos);
    }

    private void MoveToNext(int pos)
    {
        if (pos == _slides.Count)
            return;

        SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos + 1));

        //entering a presentation: place its show on the right slide with effects pending
        Slide next = _slides[pos];
        if (next.Type == SlideType.PowerPoint)
            GoTo(next);
    }

    public void Previous(Slide slide)
    {
        int pos = slide.SlideIndex;
        var host = Host(slide);

        if (slide.Type == SlideType.PowerPoint && host != null)
        {
            if (slide.CurrentAnimationCount > 0)
            {
                //undo the last click animation, position unchanged
                slide.CurrentAnimationCount--;
                TryRequest(host, "prev");
                SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos));
                return;
            }

            if (ImpressIndex(slide) > 0)
            {
                //back within the presentation; the previous slide shows fully played
                TryRequest(host, "prev");
                host.LastKnownIndex = ImpressIndex(slide) - 1;
                Slide prev = _slides[pos - 2];
                prev.CurrentAnimationCount = prev.AnimationCount;
                SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos - 1));
                return;
            }
        }

        if (pos == 1)
            return;

        SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos - 1));

        Slide prevSlide = _slides[pos - 2];
        if (prevSlide.Type == SlideType.PowerPoint)
            GoTo(prevSlide);
    }

    public void GoTo(Slide slide)
    {
        var host = Host(slide);
        if (host == null)
            return;

        try
        {
            host.Request(CommandTimeout, "goto", ("index", ImpressIndex(slide))).Dispose();
            slide.CurrentAnimationCount = 0;
            host.LastKnownIndex = ImpressIndex(slide);
            BringToFront(slide);
        }
        catch (Exception)
        {
            //presentation gone (soffice crashed or was closed): mirror the COM engine
            Host_ShowEnded(host);
        }
    }

    public void Reset(Slide slide)
    {
        var host = Host(slide);
        if (host == null)
            return;
        TryRequest(host, "goto", ("index", 0));
        host.LastKnownIndex = 0;
        foreach (var s in _slides.Where(s => Host(s) == host))
            s.CurrentAnimationCount = 0;
    }

    public void UpdateSlideTimings()
    {
        if (!IsRunning)
            return;
        foreach (var host in _hosts)
            TryRequest(host, "setTimings", ("enabled", _settings.Current.UseSlideTimings));
    }

    // -- events from the show windows --------------------------------------------------

    private void Host_SlideChanged(SlideshowHost host, int index)
    {
        if (!IsRunning)
            return;

        var hostSlides = _slides.Where(s => Host(s) == host).ToList();
        if (hostSlides.Count == 0)
            return;

        //an endless timed show wrapping from its last slide to 0 means the presentation
        //finished: move on to the next schedule item (COM's ppSlideShowDone equivalent)
        if (index == 0 && host.LastKnownIndex == host.SlideInfo.Count - 1 && host.SlideInfo.Count > 1
            && _settings.Current.UseSlideTimings)
        {
            host.LastKnownIndex = 0;
            Slide last = hostSlides[^1];
            if (last.SlideIndex < _slides.Count)
            {
                Slide next = _slides[last.SlideIndex];
                OnUi(() =>
                {
                    SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(-1, next.SlideIndex));
                    if (next.Type == SlideType.PowerPoint)
                        Task.Run(() => GoTo(next));
                });
            }
            return;
        }

        //events lag the commands by up to a second; one reporting the position we
        //already track is just the echo of our own navigation — bookkeeping is done
        if (index == host.LastKnownIndex)
            return;

        bool forward = index > host.LastKnownIndex;
        host.LastKnownIndex = index;

        Slide? current = hostSlides.FirstOrDefault(s => ImpressIndex(s) == index);
        if (current == null)
            return;
        current.CurrentAnimationCount = forward ? 0 : current.AnimationCount;

        //OldIndex -1 marks engine-originated changes; the UI ignores it when the
        //position already matches (echo of our own commands)
        OnUi(() => SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(-1, current.SlideIndex)));
    }

    private void Host_ShowEnded(SlideshowHost host)
    {
        host.InvalidateShowWindow();
        if (IsRunning)
            OnUi(() => SlideShowEnd?.Invoke(this, EventArgs.Empty));
    }

    // -- window management -------------------------------------------------------------

    public int? GetSlideWindowHandle(Slide slide) => Host(slide)?.GetShowWindowHandle();

    public void BringToFront(Slide slide)
    {
        int? hwnd = GetSlideWindowHandle(slide);
        if (hwnd == null)
            return;
        var b = _screens.ProjectorBounds;
        User32.SetWindowPos(hwnd.Value, User32.HWND_TOP, b.Left, b.Top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
    }

    // -- previews and editing ------------------------------------------------------------

    public string? ExportSlideImage(Slide slide, int idx, string suffix, int width, int height)
    {
        var host = Host(slide);
        if (host == null)
            return null;

        //counter dodges files the UI already loaded and still holds open (same as COM)
        string temp;
        for (int i = 0; true; i++)
        {
            temp = TempPath + idx + suffix + i + ".png";
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception) { }
            if (!File.Exists(temp))
                break;
        }

        try
        {
            host.Request(CommandTimeout, "export",
                ("index", ImpressIndex(slide)), ("path", temp), ("width", width), ("height", height)).Dispose();
            return temp;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool TryEditSlide(Slide slide)
    {
        if (Host(slide) == null || SofficeLocator.Find() is not string soffice)
            return false;
        try
        {
            //separate default-profile instance; our copy stays read-only and unaffected
            var psi = new System.Diagnostics.ProcessStartInfo(soffice);
            psi.ArgumentList.Add(slide.Filename);
            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Loads an image scaled to fit, frozen so it can cross threads.</summary>
    private static BitmapSource RetrieveImage(string filename, int width, int height)
    {
        BitmapSource photo = BitmapDecoder.Create(new Uri(filename), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        if (photo.Width >= width || photo.Height >= height)
        {
            double scale = Math.Min(width / photo.Width, height / photo.Height);
            photo = BitmapFrame.Create(new System.Windows.Media.Imaging.TransformedBitmap(photo,
                new System.Windows.Media.ScaleTransform(scale * 96 / photo.DpiX, scale * 96 / photo.DpiY, 0, 0)));
        }
        if (photo.CanFreeze)
            photo.Freeze();
        return photo;
    }

    /// <summary>Sends a command, treating failures as the presentation having gone away.</summary>
    private void TryRequest(SlideshowHost host, string cmd, params (string Key, object Value)[] args)
    {
        try
        {
            host.Request(CommandTimeout, cmd, args).Dispose();
        }
        catch (Exception)
        {
            Host_ShowEnded(host);
        }
    }

    private static string TempPath
    {
        get
        {
            string temp = Path.Combine(Path.GetTempPath(), "presenter") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(temp);
            return temp;
        }
    }

    /// <summary>Runs on the UI thread when a context was provided.</summary>
    private void OnUi(Action action)
    {
        if (_sync != null)
            _sync.Post(_ => action(), null);
        else
            action();
    }
}
