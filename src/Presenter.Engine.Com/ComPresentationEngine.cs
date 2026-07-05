using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Presenter.Core;
using Presenter.Core.Abstractions;
using Presenter.Core.Models;
using Presenter.Engine.Com.Interop;
using Office = Microsoft.Office.Core;
using PP = Microsoft.Office.Interop.PowerPoint;

namespace Presenter.Engine.Com;

/// <summary>
/// Drives the real PowerPoint application via COM for full-fidelity playback (live
/// animations, transitions, slide timings). Port of App_Code/SlideShow.cs plus the COM
/// members of Slide.cs behind <see cref="IPresentationEngine"/>. The engine stores the
/// running PP.Presentation of each slide in <see cref="Slide.EngineData"/> (slides of
/// the same presentation share the reference, which the UI relies on to detect
/// presentation boundaries).
/// </summary>
public class ComPresentationEngine : IPresentationEngine
{
    private readonly ISettingsStore _settings;
    private readonly IScreenInfoProvider _screens;
    private readonly EngineLabels _labels;
    private readonly SynchronizationContext? _sync;
    private readonly Action? _activateMainWindow;

    private PP.Application? app;
    private PP.Presentation? template;
    private readonly List<Slide> _slides = new();
    private CancellationTokenSource? _pollCancel;

    /// <param name="syncContext">UI thread context; engine events triggered by PowerPoint
    /// (slide changes from timings/clicks in the slideshow window) are marshalled to it,
    /// matching the original Dispatcher.Invoke behaviour.</param>
    /// <param name="activateMainWindow">Re-activates the operator window after each
    /// slideshow starts (scrollwheel-focus workaround); invoked on the UI thread.</param>
    public ComPresentationEngine(ISettingsStore settings, IScreenInfoProvider screens, EngineLabels labels,
        SynchronizationContext? syncContext = null, Action? activateMainWindow = null)
    {
        _settings = settings;
        _screens = screens;
        _labels = labels;
        _sync = syncContext;
        _activateMainWindow = activateMainWindow;
    }

    public bool IsAvailable => PowerPointInstalled();

    public bool IsRunning { get; private set; }

    public IReadOnlyList<Slide> Slides => _slides;

    public event EventHandler? SlideShowEnd;
    public event EventHandler<SlideAddedEventArgs>? SlideAdded;
    public event EventHandler<SlideIndexChangedEventArgs>? SlideIndexChanged;

    /// <summary>Registry probe for an installed PowerPoint (same keys App.xaml.cs checks).</summary>
    public static bool PowerPointInstalled()
    {
        foreach (string root in new[] { @"SOFTWARE\Microsoft\Office", @"SOFTWARE\Wow6432Node\Microsoft\Office" })
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(root);
            if (key == null)
                continue;

            foreach (string v in key.GetSubKeyNames().Where(v => Util.Parse<double>(v) >= 10))
            {
                using var pp = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(root + @"\" + v + @"\PowerPoint\InstallRoot", false);
                if (pp != null && !string.IsNullOrEmpty(pp.GetValue("Path") as string))
                    return true;
            }
        }
        return false;
    }

    // engine-data accessors (COM members of the original Slide.cs)

    private static PP.Presentation? Pres(Slide s) => s.EngineData as PP.Presentation;

    /// <summary>
    /// The PowerPoint slide behind a schedule slide. ItemIndex + 1 because a blank slide
    /// is inserted at position 1 of every presentation (see AddSlides), shifting the real
    /// slides by one. Null when the presentation has exited.
    /// </summary>
    private static PP.Slide? PSlide(Slide s)
    {
        try { return (s.EngineData as PP.Presentation)?.Slides[s.ItemIndex + 1]; }
        catch (Exception) { return null; }
    }

    public void Start(Schedule schedule)
    {
        try
        {
            Item[] items = schedule.Items.OrderBy(i => i.Ordinal).ToArray();
            IsRunning = true;

            app = new PP.Application();
            if (Util.Parse<double>(app.Version) < 14)
                app.ShowWindowsInTaskbar = Office.MsoTriState.msoFalse;
            app.SlideShowEnd += app_SlideShowEnd;

            _slides.Clear();
            SlideAdded?.Invoke(this, new SlideAddedEventArgs(null, -1));

            AddSlide("", "Blank", null, SlideType.Blank, "", 0, new Item(), 1);

            template = null;
            foreach (Item item in items)
                AddSlides(item);

            AddSlide("", "Blank", null, SlideType.Blank, "", 0, new Item(), 1);

            //don't enable SlideShowNextSlide until after all slideshows have started to prevent
            //the slideshow window popping up on top during start; enabled in AddSlides

            //background poll: a timings-driven show that reaches its end reports ppSlideShowDone
            //without raising SlideShowNextSlide, so watch for it and advance to the next show
            _pollCancel = new CancellationTokenSource();
            var token = _pollCancel.Token;
            Task.Run(() =>
            {
                while (IsRunning && !token.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                    foreach (var p in app.Presentations.Cast<PP.Presentation>())
                    {
                        try
                        {
                            if (p.SlideShowWindow().View.State == PP.PpSlideShowState.ppSlideShowDone)
                                app_SlideShowNextSlide(p.SlideShowWindow());
                        }
                        catch (Exception) { }
                    }
                }
            }, token);
        }
        catch (Exception)
        {
            Stop();
            throw;
        }
    }

    /// <summary>Update the UseSlideTimings setting for running slideshows.</summary>
    public void UpdateSlideTimings()
    {
        if (app == null || !IsRunning)
            return;

        app.SlideShowNextSlide -= app_SlideShowNextSlide;
        foreach (var p in GetPresentations())
        {
            if (!_settings.Current.UseSlideTimings)
                p.Slides.Range().SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoFalse;
            else
                p.Slides.Cast<PP.Slide>().ToList().ForEach(ss => ss.SlideShowTransition.AdvanceOnTime =
                    _slides.Where(s => PSlide(s) == ss)
                           .Select(s => s.AdvanceOnTime.HasValue ? (Office.MsoTriState?)(s.AdvanceOnTime.Value ? Office.MsoTriState.msoTrue : Office.MsoTriState.msoFalse) : null)
                           .FirstOrDefault() ?? Office.MsoTriState.msoTrue);
        }
        app.SlideShowNextSlide += app_SlideShowNextSlide;
    }

    private void app_SlideShowNextSlide(PP.SlideShowWindow Wn)
    {
        if (SlideIndexChanged == null)
            return;

        //logic to handle slideshow using timings reaching end, go to first slide of next slideshow
        if (Wn.View.State == PP.PpSlideShowState.ppSlideShowDone)
        {
            Slide? slide = _slides.FirstOrDefault(s => PSlide(s) == Wn.Presentation.Slides[Wn.Presentation.Slides.Count]);
            if (slide != null)
            {
                app!.SlideShowNextSlide -= app_SlideShowNextSlide;
                //to change slideshow state from ppSlideShowDone so this method does not continuously run
                Wn.View.Last();
                //if first slide of next slideshow has timings, go to slide in order to reset timings to allow it to advance
                if (PSlide(_slides[slide.SlideIndex]) != null)
                    GoTo(_slides[slide.SlideIndex]);
                app.SlideShowNextSlide += app_SlideShowNextSlide;
                OnUi(() => SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(-1, slide.SlideIndex + 1)));
            }
        }
        else
        {
            Slide? slide = _slides.FirstOrDefault(s => PSlide(s) == Wn.View.Slide);
            if (slide != null)
                OnUi(() => SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(-1, slide.SlideIndex)));
        }
    }

    private void app_SlideShowEnd(PP.Presentation Pres)
    {
        SlideShowEnd?.Invoke(this, EventArgs.Empty);
    }

    internal PP.Presentation[] GetPresentations()
    {
        return _slides.Where(s => Pres(s) != null).Select(s => Pres(s)!).Distinct().ToArray();
    }

    private Slide AddSlide(string text, string comments, PP.Presentation? pres, SlideType type, string filename, double progress, Item scheduleItem, int itemIndex)
    {
        Slide s = new Slide(type, filename) { Text = text, Comment = comments, EngineData = pres, SlideIndex = _slides.Count + 1, ScheduleItem = scheduleItem, ItemIndex = itemIndex };
        _slides.Add(s);

        SlideAdded?.Invoke(this, new SlideAddedEventArgs(s, progress));

        return s;
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        _pollCancel?.Cancel();

        foreach (PP.Presentation pres in GetPresentations())
        {
            try { pres.SlideShowWindow().View.Exit(); }
            catch (InvalidCastException) { }
            catch (COMException) { }
        }

        _slides.ForEach(s => s.EngineData = null);
        ComUtil.SlideShowWindows.Clear();

        //needs to be set before the slideshow windows fully close, otherwise the SlideShowEnd
        //event fired by closing them re-enters Stop leading to an eternal loop
        IsRunning = false;
    }

    public void Quit()
    {
        var quitApp = new PP.Application();
        if (quitApp.Presentations.Count == 0)
            quitApp.Quit();
    }

    public void GoTo(Slide slide)
    {
        if (slide == null)
            return;

        try
        {
            var pres = Pres(slide);
            var pslide = PSlide(slide);
            if (pres == null || pslide == null)
                return;

            pres.GotoSlide(pslide.SlideIndex);
            var b = _screens.ProjectorBounds;
            User32.SetWindowPos(pres.SlideShowWindow().HWND, User32.HWND_TOP, b.Left, b.Top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
        }
        catch (COMException ex)
        {
            if (!ex.Message.Contains("There is currently no slide show"))
                throw;
        }
        catch (InvalidCastException)
        {
            //the presentation has exited and the presentation reference is now old
            app_SlideShowEnd(null!);
        }
    }

    public void Next(Slide slide)
    {
        int pos = slide.SlideIndex;
        //fix issue in office 2013 where clicking next quickly thru slides CurrentShowPosition reports 0 on last slide
        int pcurpos = slide.ItemIndex + 1;
        var pres = Pres(slide);

        //GetClickIndex only available on office 2007 or higher so allow click to proceed (shows end of slideshow message) then switch to next presentation
        bool finishedCurrent = false;
        if (slide.Type == SlideType.PowerPoint && Util.Parse<double>(app!.Version) >= 12)
            finishedCurrent = pres!.SlideShowWindow().View.GetClickIndex() >= pres!.SlideShowWindow().View.GetClickCount();

        //minus one from pres.Slides.Count due to extra blank slide (allows click animation on first slide of the next show)
        if (slide.Type != SlideType.PowerPoint || pcurpos >= (pres!.Slides.Count - 1) && finishedCurrent)
        {
            MoveToNext(pos);
            return;
        }

        if (pcurpos > pres!.Slides.Count)
        {
            MoveToNext(pos);
            return;
        }

        pres.Next();

        int pnewpos = pres.SlideShowWindow().View.CurrentShowPosition;

        SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos + (pnewpos - pcurpos)));
    }

    private void MoveToNext(int pos)
    {
        if (pos == _slides.Count)
            return;

        SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos + 1));

        //call GotoSlide if next slide is the start of a new presentation so that calling Next works with onclick animations
        Slide nextSlide = _slides[pos];
        if (nextSlide.Type == SlideType.PowerPoint)
            Pres(nextSlide)!.GotoSlide(PSlide(nextSlide)!.SlideIndex);
    }

    public void Previous(Slide slide)
    {
        int pos = slide.SlideIndex;
        var pres = Pres(slide);

        //GetClickIndex only available on office 2007 or higher, so clicking to previous animations on first slide is not supported
        int clickIndex = 0;
        if (slide.Type == SlideType.PowerPoint && Util.Parse<double>(app!.Version) >= 12)
            clickIndex = pres!.SlideShowWindow().View.GetClickIndex();

        if (slide.Type != SlideType.PowerPoint || pres!.SlideShowWindow().View.CurrentShowPosition == 1 && clickIndex <= 0)
        {
            if (pos == 1)
                return;

            SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos - 1));

            Slide prevSlide = _slides[pos - 2];
            if (prevSlide.Type == SlideType.PowerPoint)
                Pres(prevSlide)!.GotoSlide(PSlide(prevSlide)!.SlideIndex);
            return;
        }

        int pcurpos = pres!.SlideShowWindow().View.CurrentShowPosition;
        pres!.Previous();
        int pnewpos = pres!.SlideShowWindow().View.CurrentShowPosition;

        SlideIndexChanged?.Invoke(this, new SlideIndexChangedEventArgs(pos, pos + (pnewpos - pcurpos)));
    }

    public void AddSlides(Item scheduleItem)
    {
        if (!scheduleItem.IsFound || !IsRunning)
            return;

        var S = _settings.Current;
        int itemCount = scheduleItem.Schedule?.Items.Count ?? 1;
        double progressEnd = (scheduleItem.Ordinal + 1) / (double)itemCount;
        //keep original casing: shared folders (e.g. Parallels \\Mac) can be case-sensitive
        string filename = Path.GetFullPath(scheduleItem.Filename);
        string filetype = Path.GetExtension(filename).TrimStart('.').ToLower();

        if (S.VideoFormats.Contains(filetype))
        {
            AddSlide(scheduleItem.Name, _labels.VideoLabel, null, SlideType.Video, filename, progressEnd, scheduleItem, 1);
        }
        else if (S.AudioFormats.Contains(filetype))
        {
            AddSlide(scheduleItem.Name, _labels.AudioLabel, null, SlideType.Audio, filename, progressEnd, scheduleItem, 1);
        }
        else if (S.ImageFormats.Contains(filetype))
        {
            var s = AddSlide(scheduleItem.Name, _labels.ImageLabel, null, SlideType.Image, filename, progressEnd, scheduleItem, 1);
            var b = _screens.ProjectorBounds;
            PostUi(() =>
            {
                s.Image = RetrieveImage(s.Filename, b.Width, b.Height);
                s.Preview = RetrieveImage(s.Filename, 333, 250);
            });
        }
        else if (S.PowerPointTemplates.Contains(filetype))
        {
            if (template != null)
            {
                template.Close();
                template = null;
            }

            if (!scheduleItem.IsTemplateNone)
                template = OpenPresentation(filename);

            SlideAdded?.Invoke(this, new SlideAddedEventArgs(null, progressEnd));
        }
        else if (S.PowerPointFormats.Contains(filetype))
        {
            var pres = OpenPresentation(filename);

            if (template != null)
                pres.Slides.Range().Design = template.Designs[1];

            for (int i = 1; i <= pres.Slides.Count; i++)
            {
                var defaultText = pres.Slides[i].Layout == PP.PpSlideLayout.ppLayoutBlank && pres.Slides[i].Shapes.Count == 0 ? "" : _labels.SlideLabel + pres.Slides[i].SlideIndex;
                var _slide = AddSlide(GetStringSummary(pres.Slides[i].Shapes).ToNullIfEmpty() ?? defaultText, GetStringSummary(pres.Slides[i].NotesPage.Shapes), pres, SlideType.PowerPoint, "", progressEnd, scheduleItem, i);
                _slide.AnimationCount = pres.Slides[i].TimeLine.MainSequence.Cast<PP.Effect>().Sum(e => e.Timing.TriggerType == PP.MsoAnimTriggerType.msoAnimTriggerOnPageClick ? 1 : 0);
                _slide.AdvanceOnTime = pres.Slides[i].SlideShowTransition.AdvanceOnTime == Office.MsoTriState.msoTrue;
            }

            pres.SlideShowSettings.AdvanceMode = PP.PpSlideShowAdvanceMode.ppSlideShowUseSlideTimings;
            if (!S.UseSlideTimings)
                pres.Slides.Range().SlideShowTransition.AdvanceOnTime = Office.MsoTriState.msoFalse;

            if (!IsRunning)
            {
                pres.Close();
                return;
            }

            //detach while starting the show to avoid an event storm from programmatic navigation
            app!.SlideShowNextSlide -= app_SlideShowNextSlide;

            //blank slide at position 1: enables click-animation on the first real slide when
            //arriving from the previous presentation (PSlide accounts for the +1 shift)
            var slide = pres.Slides.Add(1, PP.PpSlideLayout.ppLayoutBlank);
            slide.FollowMasterBackground = Office.MsoTriState.msoFalse;
            slide.Background.Fill.ForeColor.RGB = BlankColourOle();
            slide.Background.Fill.Solid();
            slide.SlideShowTransition.EntryEffect = PP.PpEntryEffect.ppEffectNone;

            //suppress Presenter View (on by default with a second monitor since PowerPoint 2013)
            //without changing the user's global PowerPoint preference
            pres.SlideShowSettings.ShowPresenterView = Office.MsoTriState.msoFalse;
            pres.SlideShowSettings.Run();
            ComUtil.SlideShowWindows[pres] = pres.SlideShowWindow;

            //ensure presenter has focus otherwise if ppt has focus and user moves scrollwheel over presenter
            //expecting the listview to scroll it will actually change slides and can end the slideshow unexpectedly
            OnUi(() => _activateMainWindow?.Invoke());

            var taskbarList = (ITaskbarList2)new CTaskbarList();
            taskbarList.HrInit();
            var showHwnd = new IntPtr(pres.SlideShowWindow().HWND);
            taskbarList.DeleteTab(showHwnd);
            //keep the taskbar beneath the show even though the operator window has focus
            taskbarList.MarkFullscreenWindow(showHwnd, true);

            app.SlideShowNextSlide += app_SlideShowNextSlide;
        }

        if (S.InsertBlankAfterPres && S.PowerPointFormats.Contains(filetype) || S.InsertBlankAfterVideo && S.VideoFormats.Concat(S.AudioFormats).Contains(filetype))
        {
            AddSlide("", "Blank", null, SlideType.Blank, "", progressEnd, new Item(), 1);
        }
    }

    public static BitmapSource RetrieveImage(string filename, int width, int height)
    {
        var photo = BitmapDecoder.Create(new Uri(filename), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None).Frames[0];
        if (photo.Width < width && photo.Height < height)
            return photo;
        double scale = Math.Min(width / photo.Width, height / photo.Height);
        return BitmapFrame.Create(new TransformedBitmap(photo, new ScaleTransform(scale * 96 / photo.DpiX, scale * 96 / photo.DpiY, 0, 0)));
    }

    private PP.Presentation OpenPresentation(string filename)
    {
        //the original shelled out for Office <= 2003 (version < 12) to convert pptx files;
        //that path is dropped — the engine requires Office 2007+
        return app!.Presentations.Open(filename, WithWindow: Office.MsoTriState.msoFalse);
    }

    private string GetStringSummary(PP.Shapes shapes)
    {
        string text = "";
        foreach (PP.Shape shape in shapes)
        {
            if (shape.Name.Contains("Slide Number Placeholder") || shape.Name == "source" || shape.HasTextFrame != Office.MsoTriState.msoTrue)
                continue;

            try
            {
                //remove slide numbers
                if (text == "" && Util.Parse<int?>(shape.TextFrame.TextRange.Text).HasValue)
                    continue;

                text += " " + shape.TextFrame.TextRange.Text.Replace("\r", " ").Replace("\n", " ").Replace("\v", " ").Trim();
            }
            catch (Exception) { }
        }
        return text.Trim();
    }

    public string? ExportSlideImage(Slide slide, int idx, string suffix, int width, int height)
    {
        var pslide = PSlide(slide);
        if (pslide == null)
            return null;

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

        //if powerpoint closed, export throws
        try { pslide.Export(temp, "PNG", width, height); }
        catch (Exception) { return null; }

        return temp;
    }

    public int? GetSlideWindowHandle(Slide slide)
    {
        try { return Pres(slide)?.SlideShowWindow().HWND; }
        catch (Exception) { return null; }
    }

    public void Reset(Slide slide)
    {
        var pres = Pres(slide);
        if (pres == null)
            return;
        pres.GotoSlide(1);
    }

    public void BringToFront(Slide slide)
    {
        var pres = Pres(slide);
        if (pres == null)
            return;
        var b = _screens.ProjectorBounds;
        User32.SetWindowPos(pres.SlideShowWindow().HWND, User32.HWND_TOP, b.Left, b.Top, 0, 0, User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
    }

    public bool TryEditSlide(Slide slide)
    {
        var pres = Pres(slide);
        if (pres == null)
            return false;

        try
        {
            pres.Application.Visible = Office.MsoTriState.msoTrue;
            if (pres.Windows.Count == 0)
                pres.NewWindow();
            var wa = _screens.PrimaryWorkingArea;
            User32.SetWindowPos(pres.Application.HWND, User32.HWND_TOP,
                wa.Left + (int)(wa.Width * 0.05), wa.Top + (int)(wa.Height * 0.05),
                (int)(wa.Width * 0.9), (int)(wa.Height * 0.9), 0);
            pres.Windows[1].Activate();
            PSlide(slide)?.Select();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private int BlankColourOle()
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(_settings.Current.ScreenBlankColour);
            return ComUtil.ToOle(c);
        }
        catch (Exception)
        {
            return 0; //black
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

    /// <summary>Runs on the UI thread synchronously (Dispatcher.Invoke equivalent).</summary>
    private void OnUi(Action action)
    {
        if (_sync != null)
            _sync.Send(_ => action(), null);
        else
            action();
    }

    /// <summary>Queues onto the UI thread (Dispatcher.BeginInvoke equivalent).</summary>
    private void PostUi(Action action)
    {
        if (_sync != null)
            _sync.Post(_ => action(), null);
        else
            action();
    }
}
